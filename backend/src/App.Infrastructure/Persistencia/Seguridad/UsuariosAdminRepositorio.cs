using System.Data;
using App.Application.Comun;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;
using App.Domain.Seguridad;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistencia.Seguridad;

/// <summary>
/// TAREA-25-1: lectura y escritura de usuarios y de sus departamentos (Admin). El alta y las asignaciones pasan por el
/// AuditoriaInterceptor (entidades con seguimiento); la edición del usuario es un compare-and-set con ExecuteUpdate (sin
/// RowVer en Usuario, sin migración), así que escribe ModificadoPorId y FechaModificacion aquí, como la TAREA-62.
/// </summary>
internal sealed class UsuariosAdminRepositorio(ProfesiogramaDbContext db, ILogger<UsuariosAdminRepositorio> logger) : IUsuariosAdminRepositorio
{
    public const int EsperaMaximaMs = 15_000;
    public const string MensajeOcupado = "Hay otra modificación de este usuario en curso; intente de nuevo."; // TAREA-25-2
    private static readonly int[] ErroresUnicos = [2601, 2627];

    public async Task<PaginaResultado<UsuarioAdminDto>> ListarAsync(UsuarioAdminFiltro filtro, CancellationToken ct)
    {
        var consulta = ConsultaListado(db, filtro);
        var total = await consulta.CountAsync(ct);
        var saltar = (long)(filtro.Pagina - 1) * filtro.Tamano;
        if (total == 0 || saltar >= total)
        {
            return new PaginaResultado<UsuarioAdminDto>([], filtro.Pagina, filtro.Tamano, total);
        }

        var items = await PaginaListado(consulta, (int)saltar, filtro.Tamano).ToListAsync(ct);
        return new PaginaResultado<UsuarioAdminDto>(items, filtro.Pagina, filtro.Tamano, total);
    }

    public Task<UsuarioAdminDto?> ObtenerAsync(int id, CancellationToken ct) => ConsultaDetalle(db, id).FirstOrDefaultAsync(ct);

    public Task<UsuarioRegistro?> ObtenerRegistroAsync(int id, CancellationToken ct) =>
        Registro(db.Usuarios.AsNoTracking().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    public Task<UsuarioRegistro?> BuscarPorEmailAsync(string email, CancellationToken ct) =>
        ConsultaPorEmail(db, email).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DepartamentoRegistro>> ObtenerDepartamentosAsync(CancellationToken ct) =>
        await ConsultaDepartamentos(db).ToListAsync(ct);

    public async Task<int?> CrearAsync(string email, string nombreMostrar, CancellationToken ct)
    {
        var usuario = new Usuario { Email = email, NombreMostrar = nombreMostrar, Activo = true };
        db.Usuarios.Add(usuario);
        try
        {
            await db.SaveChangesAsync(ct);
            return usuario.Id;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && ErroresUnicos.Contains(sql.Number))
        {
            db.ChangeTracker.Clear();
            return null;
        }
    }

    public Task<int> ActualizarAsync(int id, string nombreAnterior, bool activoAnterior, string nombre, bool activo, int usuarioId,
        DateTime fechaUtc, CancellationToken ct) =>
        ConsultaUsuarioConValores(db, id, nombreAnterior, activoAnterior).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.NombreMostrar, nombre)
            .SetProperty(u => u.Activo, activo)
            .SetProperty(u => u.ModificadoPorId, usuarioId)
            .SetProperty(u => u.FechaModificacion, fechaUtc), ct);

    public async Task<CambioDepartamentos> AsignarDepartamentosAsync(int usuarioId, IReadOnlySet<int> anteriores, IReadOnlySet<int> nuevos,
        CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await BloquearUsuarioAsync(usuarioId, ct);

        // Relectura dentro del bloqueo: otro Admin pudo cambiar el conjunto después de que este lo vio.
        var filas = await ConsultaAsignaciones(db, usuarioId).ToListAsync(ct);
        if (!filas.Where(f => f.Activo).Select(f => f.DepartamentoId).ToHashSet().SetEquals(anteriores))
        {
            await transaccion.RollbackAsync(ct);
            return new CambioDepartamentos(true, [], []);
        }

        var cambio = Aplicar(filas, nuevos, usuarioId);
        db.UsuarioDepartamentos.AddRange(cambio.Nuevas);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return new CambioDepartamentos(false, cambio.Agregados, cambio.Retirados);
    }

    /// <summary>
    /// Plan puro sobre las filas del usuario (con seguimiento): retira (Activo = 0) las activas que no siguen; reactiva la
    /// fila existente (UQ_UsuarioDepartamento) o crea una nueva para las que entran, con Notificado = 0 y FechaNotificacion
    /// NULL (pendiente 71); las que no cambian no se tocan. Nunca borra filas.
    /// </summary>
    internal static (IReadOnlyList<UsuarioDepartamento> Nuevas, IReadOnlyList<int> Agregados, IReadOnlyList<int> Retirados) Aplicar(
        IReadOnlyList<UsuarioDepartamento> filas, IReadOnlySet<int> nuevos, int usuarioId)
    {
        var retirados = new List<int>();
        foreach (var fila in filas.Where(f => f.Activo && !nuevos.Contains(f.DepartamentoId)))
        {
            fila.Activo = false;
            retirados.Add(fila.DepartamentoId);
        }

        var agregados = new List<int>();
        var nuevas = new List<UsuarioDepartamento>();
        foreach (var departamentoId in nuevos.Order())
        {
            var fila = filas.FirstOrDefault(f => f.DepartamentoId == departamentoId);
            if (fila is { Activo: true })
            {
                continue;
            }

            if (fila is null)
            {
                nuevas.Add(new UsuarioDepartamento { UsuarioId = usuarioId, DepartamentoId = departamentoId, Activo = true, Notificado = false });
            }
            else
            {
                fila.Activo = true;
                fila.Notificado = false;
                fila.FechaNotificacion = null;
            }

            agregados.Add(departamentoId);
        }

        return (nuevas, agregados, retirados.Order().ToList());
    }

    /// <summary>Bloqueo exclusivo por usuario (dueño = transacción): serializa dos cambios de departamentos del mismo usuario.</summary>
    private async Task BloquearUsuarioAsync(int usuarioId, CancellationToken ct)
    {
        var recurso = $"profesiograma:usuario:{usuarioId}";
        var resultado = new SqlParameter("@resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @r int;
            EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = @espera;
            SET @resultado = @r;
            """,
            [new SqlParameter("@recurso", SqlDbType.NVarChar, 255) { Value = recurso },
             new SqlParameter("@espera", SqlDbType.Int) { Value = EsperaMaximaMs },
             resultado],
            ct);

        var codigo = resultado.Value is int valor ? valor : -999;
        if (codigo < 0)
        {
            logger.LogWarning("No se obtuvo el bloqueo {Recurso} (sp_getapplock = {Codigo}).", recurso, codigo);
            throw new RegistroOcupadoException($"No se obtuvo el bloqueo del usuario (sp_getapplock = {codigo}).")
            {
                MensajeUsuario = MensajeOcupado,
            };
        }
    }

    // ------------------------------------------------------------------ consultas (prueba de traducción con ToQueryString)

    internal static IQueryable<Usuario> ConsultaListado(ProfesiogramaDbContext db, UsuarioAdminFiltro filtro)
    {
        var consulta = db.Usuarios.AsNoTracking().Where(u => !u.EsSistema);
        if (!filtro.IncluirInactivos)
        {
            consulta = consulta.Where(u => u.Activo);
        }

        if (filtro.Texto is { } texto)
        {
            consulta = consulta.Where(u => u.Email.Contains(texto) || u.NombreMostrar.Contains(texto));
        }

        return consulta;
    }

    internal static IQueryable<UsuarioAdminDto> PaginaListado(IQueryable<Usuario> consulta, int saltar, int tamano) =>
        Proyectar(consulta.OrderBy(u => u.Email).ThenBy(u => u.Id).Skip(saltar).Take(tamano));

    internal static IQueryable<UsuarioAdminDto> ConsultaDetalle(ProfesiogramaDbContext db, int id) =>
        Proyectar(db.Usuarios.AsNoTracking().Where(u => u.Id == id && !u.EsSistema));

    internal static IQueryable<UsuarioRegistro?> ConsultaPorEmail(ProfesiogramaDbContext db, string email) =>
        Registro(db.Usuarios.AsNoTracking().Where(u => u.Email == email));

    internal static IQueryable<DepartamentoRegistro> ConsultaDepartamentos(ProfesiogramaDbContext db) =>
        db.Departamentos.AsNoTracking().Select(d => new DepartamentoRegistro(d.Id, d.Nombre, d.NombreCorto, d.Activo, d.Orden));

    internal static IQueryable<Usuario> ConsultaUsuarioConValores(ProfesiogramaDbContext db, int id, string nombreAnterior, bool activoAnterior) =>
        db.Usuarios.Where(u => u.Id == id && !u.EsSistema && u.NombreMostrar == nombreAnterior && u.Activo == activoAnterior);

    internal static IQueryable<UsuarioDepartamento> ConsultaAsignaciones(ProfesiogramaDbContext db, int usuarioId) =>
        db.UsuarioDepartamentos.Where(ud => ud.UsuarioId == usuarioId);

    /// <summary>Una sola consulta con los departamentos activos de cada usuario (sin N+1).</summary>
    private static IQueryable<UsuarioAdminDto> Proyectar(IQueryable<Usuario> consulta) =>
        consulta.Select(u => new UsuarioAdminDto(u.Id, u.Email, u.NombreMostrar, u.Activo, u.EntraObjectId != null,
            u.Departamentos.Where(ud => ud.Activo).OrderBy(ud => ud.Departamento.Orden).ThenBy(ud => ud.DepartamentoId)
                .Select(ud => new DepartamentoUsuarioDto(ud.DepartamentoId, ud.Departamento.NombreCorto)).ToList()));

    private static IQueryable<UsuarioRegistro?> Registro(IQueryable<Usuario> consulta) =>
        consulta.Select(u => (UsuarioRegistro?)new UsuarioRegistro(u.Id, u.Email, u.EntraObjectId, u.NombreMostrar, u.Activo, u.EsSistema));
}
