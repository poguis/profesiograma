using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Proyectos.Estados;
using App.Domain.Configuracion;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistencia.Empleados;

/// <summary>
/// TAREA-25-3-1: CargoInfor (Admin). CargoInfor no tiene RowVer: la edición es un compare-and-set por valores con
/// ExecuteUpdate (sin migración), que no pasa por el AuditoriaInterceptor, así que ModificadoPorId y FechaModificacion se
/// escriben aquí (como la TAREA-62 y la 25-1). La importación inserta con seguimiento (auditoría por el interceptor) y la
/// llama el servicio dentro de ITransaccionAsignaciones.
/// </summary>
internal sealed class CargosInforRepositorio(ProfesiogramaDbContext db, ILogger<CargosInforRepositorio> logger) : ICargosInforRepositorio
{
    private static readonly int[] ErroresUnicos = [2601, 2627];

    public async Task<PaginaResultado<CargoInforFila>> ListarAsync(CargoInforFiltro filtro, CancellationToken ct)
    {
        var consulta = ConsultaListado(db, filtro);
        var total = await consulta.CountAsync(ct);
        var saltar = (long)(filtro.Pagina - 1) * filtro.Tamano;
        if (total == 0 || saltar >= total)
        {
            return new PaginaResultado<CargoInforFila>([], filtro.Pagina, filtro.Tamano, total);
        }

        var items = await PaginaListado(db, consulta, (int)saltar, filtro.Tamano).ToListAsync(ct);
        return new PaginaResultado<CargoInforFila>(items, filtro.Pagina, filtro.Tamano, total);
    }

    public async Task<IReadOnlyList<CargoRegistro>> ObtenerTodosAsync(CancellationToken ct) => await ConsultaTodos(db).ToListAsync(ct);

    public async Task InsertarAsync(IReadOnlyList<string> cargos, CancellationToken ct)
    {
        db.CargosInfor.AddRange(cargos.Select(c => new CargoInfor { Cargo = c, CodigoInfor = null, Activo = true }));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && ErroresUnicos.Contains(sql.Number))
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("Importación de cargos Infor: UQ_CargoInfor_Cargo al insertar {Cantidad} filas; no se guardó ninguna.", cargos.Count);
            throw new ConflictoConcurrenciaException("UQ_CargoInfor_Cargo al importar cargos.", ex);
        }
    }

    public Task<int> ActualizarAsync(int id, string? codigoAnterior, bool activoAnterior, string? codigo, bool activo, int usuarioId,
        DateTime fechaUtc, CancellationToken ct) =>
        ConsultaCargoConValores(db, id, codigoAnterior, activoAnterior).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.CodigoInfor, codigo)
            .SetProperty(c => c.Activo, activo)
            .SetProperty(c => c.ModificadoPorId, usuarioId)
            .SetProperty(c => c.FechaModificacion, fechaUtc), ct);

    public Task<bool> ExisteAsync(int id, CancellationToken ct) => ConsultaPorId(db, id).AnyAsync(ct);

    // ------------------------------------------------------------------ consultas (prueba de traducción con ToQueryString)

    internal static IQueryable<CargoInfor> ConsultaListado(ProfesiogramaDbContext db, CargoInforFiltro filtro)
    {
        var consulta = db.CargosInfor.AsNoTracking();
        if (!filtro.IncluirInactivos)
        {
            consulta = consulta.Where(c => c.Activo);
        }

        if (filtro.Texto is { } texto)
        {
            consulta = consulta.Where(c => c.Cargo.Contains(texto) || (c.CodigoInfor != null && c.CodigoInfor.Contains(texto)));
        }

        return filtro.Filtro switch
        {
            FiltroCodigoInfor.SinCodigo => consulta.Where(c => c.CodigoInfor == null || c.CodigoInfor == ""),
            FiltroCodigoInfor.ConCodigo => consulta.Where(c => c.CodigoInfor != null && c.CodigoInfor != ""),
            _ => consulta,
        };
    }

    /// <summary>Página por Cargo, con las personas guardadas en Empleado con ese puesto (subconsulta: una sola consulta).</summary>
    internal static IQueryable<CargoInforFila> PaginaListado(ProfesiogramaDbContext db, IQueryable<CargoInfor> consulta, int saltar, int tamano) =>
        consulta.OrderBy(c => c.Cargo).ThenBy(c => c.Id).Skip(saltar).Take(tamano)
            .Select(c => new CargoInforFila(c.Id, c.Cargo, c.CodigoInfor, c.Activo, db.Empleados.Count(e => e.Puesto == c.Cargo)));

    internal static IQueryable<CargoRegistro> ConsultaTodos(ProfesiogramaDbContext db) =>
        db.CargosInfor.AsNoTracking().Select(c => new CargoRegistro(c.Id, c.Cargo, c.CodigoInfor, c.Activo));

    /// <summary>Fila solo si el código y el activo siguen siendo los que vio el Admin (anterior null = IS NULL).</summary>
    internal static IQueryable<CargoInfor> ConsultaCargoConValores(ProfesiogramaDbContext db, int id, string? codigoAnterior, bool activoAnterior) =>
        db.CargosInfor.Where(c => c.Id == id && c.CodigoInfor == codigoAnterior && c.Activo == activoAnterior);

    internal static IQueryable<CargoInfor> ConsultaPorId(ProfesiogramaDbContext db, int id) => db.CargosInfor.Where(c => c.Id == id);
}
