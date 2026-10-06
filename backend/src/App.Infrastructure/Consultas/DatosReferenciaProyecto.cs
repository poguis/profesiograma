using System.Globalization;
using App.Application.Proyectos.Crear;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

/// <summary>Datos de referencia para validar "Crear proyecto" (solo lectura).</summary>
internal sealed class DatosReferenciaProyecto(ProfesiogramaDbContext db) : IDatosReferenciaProyecto
{
    private const string EstadoEmpleadoActivo = "A";
    private const string ClaveMaxPrincipales = "PROYECTO_MAX_PRINCIPALES";
    private const string ClaveMaxBacks = "PROYECTO_MAX_BACKS";
    private const string ClaveMaxDiasDescansoBack = "BACK_MAX_DIAS_DESCANSO";
    private const string ClaveExigePrincipal = "PROYECTO_EXIGE_PRINCIPAL"; // TAREA-19y
    private const int PorDefecto = 20; // valor de las semillas si el parámetro faltara

    public Task<GrupoRef?> ObtenerGrupoAsync(string codigo, CancellationToken ct) =>
        ConsultaGrupo(db, codigo).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<string, JornadaRef>> ObtenerJornadasAsync(CancellationToken ct) =>
        (await ConsultaJornadas(db).ToListAsync(ct)).ToDictionary(j => j.Codigo, StringComparer.OrdinalIgnoreCase);

    public async Task<LimitesProyecto> ObtenerLimitesAsync(CancellationToken ct)
    {
        var valores = await ConsultaLimites(db).ToDictionaryAsync(p => p.Clave, p => p.Valor, ct);

        int Leer(string clave) =>
            valores.TryGetValue(clave, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : PorDefecto;

        return new LimitesProyecto(Leer(ClaveMaxPrincipales), Leer(ClaveMaxBacks), Leer(ClaveMaxDiasDescansoBack),
            LeerExigePrincipal(valores.GetValueOrDefault(ClaveExigePrincipal)));
    }

    /// <summary>PROYECTO_EXIGE_PRINCIPAL: "1" o "true" (sin importar mayúsculas) = true; otro valor o ausente = false (inicial 0).</summary>
    internal static bool LeerExigePrincipal(string? valor) =>
        valor?.Trim() is { } v && (v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyDictionary<int, EmpleadoRef>> ObtenerEmpleadosActivosAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await ConsultaEmpleadosActivos(db, ids).ToDictionaryAsync(e => e.Id, ct);

    public async Task<IReadOnlyList<DepartamentoRef>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct) =>
        (await ConsultaDepartamentosDeUsuario(db, usuarioId).ToListAsync(ct))
            .Select(d => new DepartamentoRef(d.Id, d.Nombre))
            .ToList();

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    internal static IQueryable<GrupoRef> ConsultaGrupo(ProfesiogramaDbContext db, string codigo) =>
        db.GruposProyecto.AsNoTracking()
            .Where(g => g.Activo && g.Codigo == codigo)
            .Select(g => new GrupoRef(g.Id, g.Codigo, g.RequiereProyectoErp, g.RequiereDimension));

    internal static IQueryable<JornadaRef> ConsultaJornadas(ProfesiogramaDbContext db) =>
        db.Jornadas.AsNoTracking()
            .Where(j => j.Activo)
            .Select(j => new JornadaRef(j.Id, j.Codigo, j.DiasTrabajo, j.DiasDescanso));

    internal static IQueryable<Domain.Configuracion.Parametro> ConsultaLimites(ProfesiogramaDbContext db)
    {
        string[] claves = [ClaveMaxPrincipales, ClaveMaxBacks, ClaveMaxDiasDescansoBack, ClaveExigePrincipal];
        return db.Parametros.AsNoTracking().Where(p => claves.Contains(p.Clave));
    }

    internal static IQueryable<EmpleadoRef> ConsultaEmpleadosActivos(ProfesiogramaDbContext db, IReadOnlyCollection<int> ids) =>
        db.Empleados.AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.EstadoErp == EstadoEmpleadoActivo)
            .Select(e => new EmpleadoRef(e.Id, e.CodigoEkon, e.NombreCompleto, e.Puesto));

    /// <summary>
    /// Distinct y OrderBy sobre un tipo anónimo (traducible a SQL); el record se arma en memoria.
    /// Ordenar después de proyectar a un record con constructor NO es traducible por EF (TAREA-12).
    /// </summary>
    internal static IQueryable<(int Id, string Nombre)> ConsultaDepartamentosDeUsuario(ProfesiogramaDbContext db, int usuarioId) =>
        db.UsuarioDepartamentos.AsNoTracking()
            .Where(ud => ud.UsuarioId == usuarioId && ud.Activo && ud.Departamento.Activo)
            .Select(ud => new { ud.Departamento.Id, ud.Departamento.Nombre })
            .Distinct()
            .OrderBy(d => d.Id)
            .Select(d => ValueTuple.Create(d.Id, d.Nombre));
}
