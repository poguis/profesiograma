using System.Globalization;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Consultas;

/// <summary>Datos de referencia para validar "Crear proyecto" (solo lectura).</summary>
internal sealed class DatosReferenciaProyecto(ProfesiogramaDbContext db, ILogger<DatosReferenciaProyecto> registro) : IDatosReferenciaProyecto
{
    private const string ClaveMaxPrincipales = "PROYECTO_MAX_PRINCIPALES";
    private const string ClaveMaxBacks = "PROYECTO_MAX_BACKS";
    private const string ClaveMaxDiasDescansoBack = "BACK_MAX_DIAS_DESCANSO";
    private const string ClaveExigePrincipal = "PROYECTO_EXIGE_PRINCIPAL"; // TAREA-19y
    internal const string ClaveHorariosNoLaborales = "HORARIOS_NO_LABORALES"; // TAREA-26c
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

    /// <summary>Sin caché: un cambio del parámetro aplica en la siguiente lista o validación (TAREA-26c).</summary>
    public async Task<IReadOnlyList<string>> ObtenerHorariosNoLaboralesAsync(CancellationToken ct) =>
        SepararLista(await ConsultaHorariosNoLaborales(db).FirstOrDefaultAsync(ct));

    /// <summary>TAREA-62: sin caché; un cambio del Admin aplica en la siguiente búsqueda o validación de toda la API.</summary>
    public Task<IReadOnlyList<string>> ObtenerFamiliasAsignablesAsync(CancellationToken ct) => LeerFamiliasAsignablesAsync(db, registro, ct);

    /// <summary>Lectura común del buscador (EmpleadoConsultas) y de las validaciones. Vacía o ausente = sin filtro (Warning).</summary>
    internal static async Task<IReadOnlyList<string>> LeerFamiliasAsignablesAsync(ProfesiogramaDbContext db, ILogger registro, CancellationToken ct)
    {
        var familias = SepararLista(await ConsultaFamiliasAsignables(db).FirstOrDefaultAsync(ct));
        if (familias.Count == 0)
        {
            registro.LogWarning("Parámetro {Clave} vacío o ausente: sin filtro de personal elegible.", ReglasPersonalElegible.ClaveParametro);
        }

        return familias;
    }

    /// <summary>"A, B,,C" → ["A", "B", "C"]; null → vacía.</summary>
    internal static IReadOnlyList<string> SepararLista(string? valor) =>
        valor?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    public async Task<IReadOnlyList<EmpleadoGuardado>> ObtenerEmpleadosGuardadosAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await ConsultaEmpleadosPorIds(db, ids).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> ObtenerIdsPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct) =>
        (await ConsultaIdsPorCodigos(db, codigos).ToListAsync(ct))
            .ToDictionary(e => e.CodigoEkon, e => e.Id, StringComparer.OrdinalIgnoreCase);

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

    /// <summary>TAREA-62: valor de EMPLEADO_FAMILIAS_ASIGNABLES.</summary>
    internal static IQueryable<string> ConsultaFamiliasAsignables(ProfesiogramaDbContext db) =>
        db.Parametros.AsNoTracking().Where(p => p.Clave == ReglasPersonalElegible.ClaveParametro).Select(p => p.Valor);

    internal static IQueryable<string> ConsultaHorariosNoLaborales(ProfesiogramaDbContext db) =>
        db.Parametros.AsNoTracking().Where(p => p.Clave == ClaveHorariosNoLaborales).Select(p => p.Valor);

    /// <summary>
    /// TAREA-26d-3: datos laborales guardados en Empleado por Id SIN filtrar por estado (comparación "guardado frente al
    /// ERP"; si el empleado está activo lo decide la API, no la base). Sin cédula ni correo.
    /// </summary>
    internal static IQueryable<EmpleadoGuardado> ConsultaEmpleadosPorIds(ProfesiogramaDbContext db, IReadOnlyCollection<int> ids) =>
        db.Empleados.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new EmpleadoGuardado(e.Id, e.CodigoEkon, e.NombreCompleto, e.Puesto, e.Departamento, e.Unidad));

    /// <summary>TAREA-26d: Id de las filas de Empleado por CodigoEkon (collation CI de la base).</summary>
    internal static IQueryable<FilaCodigoEmpleado> ConsultaIdsPorCodigos(ProfesiogramaDbContext db, IReadOnlyCollection<string> codigos) =>
        db.Empleados.AsNoTracking()
            .Where(e => codigos.Contains(e.CodigoEkon))
            .Select(e => new FilaCodigoEmpleado { Id = e.Id, CodigoEkon = e.CodigoEkon });

    internal sealed class FilaCodigoEmpleado
    {
        public int Id { get; init; }
        public string CodigoEkon { get; init; } = string.Empty;
    }

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
