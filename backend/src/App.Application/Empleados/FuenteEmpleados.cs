namespace App.Application.Empleados;

// Empleados desde la API EvolutionEmployee del ERP (TAREA-26d, opción C): la API es la ÚNICA fuente de los empleados;
// no hay sincronización. Ver docs/fases/FASE_4_Empleados_API.md.

/// <summary>
/// Empleado tal como lo entrega la API, SOLO con los campos permitidos (C31). Nunca se agregan aquí cédula, teléfono,
/// correo personal, dirección, fechas personales, sueldo, BPR, sexo ni datos de la persona a quien reporta.
/// </summary>
public sealed record EmpleadoErp(
    string? CodigoEkon,
    string? NombreCompleto,
    string? Apellidos,
    string? Nombres,
    string? CorreoEmpresa,
    string? CodEmpresa,
    string? Empresa,
    string? CodPuesto,
    string? Puesto,
    string? CodDepartamento,
    string? Departamento,
    string? CodUnidad,
    string? Unidad,
    string? CodArea,
    string? Area,
    string? CodSeccion,
    string? Seccion,
    string? FamiliaPuesto,
    string? Estado);

/// <summary>
/// Lista de empleados activos de la API (solo los válidos según <see cref="MapeoEmpleado"/>), con su índice por código.
/// EsAnterior = la API no respondió y se usa una lista anterior (solo para el buscador, P4).
/// </summary>
public sealed class ListaEmpleadosErp
{
    public ListaEmpleadosErp(IReadOnlyList<EmpleadoErp> activos, DateTimeOffset obtenidaUtc, bool esAnterior = false)
    {
        ArgumentNullException.ThrowIfNull(activos);
        Activos = activos;
        ObtenidaUtc = obtenidaUtc;
        EsAnterior = esAnterior;
        var porCodigo = new Dictionary<string, EmpleadoErp>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in activos)
        {
            porCodigo.TryAdd(e.CodigoEkon!, e); // códigos únicos según el sondeo; ante un repetido se queda el primero
        }

        PorCodigo = porCodigo;
    }

    public IReadOnlyList<EmpleadoErp> Activos { get; }
    public IReadOnlyDictionary<string, EmpleadoErp> PorCodigo { get; }
    public DateTimeOffset ObtenidaUtc { get; }
    public bool EsAnterior { get; }

    public ListaEmpleadosErp ComoAnterior() => new(Activos, ObtenidaUtc, esAnterior: true);
}

/// <summary>Fuente de los empleados activos del ERP (Http con caché en memoria, o Simulado).</summary>
public interface IFuenteEmpleadosErp
{
    /// <summary>
    /// Lista en caché (TTL ServiciosExternos:CacheEmpleadosMinutos). Para el buscador y la vista previa. Si la API no
    /// responde y la lista anterior tiene menos de CacheEmpleadosMaxAntiguedadMinutos, la devuelve con EsAnterior = true.
    /// </summary>
    /// <exception cref="Erp.ErpNoDisponibleException">La API no respondió y no hay lista anterior utilizable.</exception>
    Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct);

    /// <summary>Lectura fresca (renueva la caché). Para VALIDAR un registro (P5): nunca usa una lista anterior.</summary>
    /// <exception cref="Erp.ErpNoDisponibleException">Red, timeout, HTTP ≠ 200, JSON inválido, isSuccess = false, result nulo o vacío.</exception>
    Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct);
}
