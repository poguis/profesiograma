using App.Application.Empleados;
using App.Application.Proyectos.Crear;

namespace App.Application.Novedades.Sincronizacion;

/// <summary>
/// Respuesta de POST /api/admin/novedades/sincronizar-permisos-medicos. Solo conteos y códigos NOV (máx. 100): nunca
/// nombres, correos, cédulas ni el JSON. La fecha de la sincronización no se guarda: va aquí y en el log.
/// </summary>
public sealed record ResultadoSincronizacionDto(
    DateTime SincronizadaUtc,
    string Fuente,
    int Leidas,
    int Nuevas,
    int Actualizadas,
    int Anuladas,
    int Reactivadas,
    int SinCambios,
    int AusentesEnOrigen,
    IReadOnlyDictionary<string, int> Omitidas,
    IReadOnlyList<string> CodigosOmitidos);

/// <summary>Novedad de PERMISO MEDICO guardada (sin seguimiento), para comparar con la fuente.</summary>
public sealed record NovedadExistente(
    int Id,
    Guid Uid,
    byte OrigenId,
    string Codigo,
    int? LegacyId,
    int? EmpleadoId,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<DateOnly> Dias,
    string? OrigenArea,
    string? RegistradoPorNombre,
    string? RegistradoPorCorreo,
    string? DetalleOrigenJson,
    bool Anulada);

/// <summary>Empleado de la base encontrado por su CorreoEmpresa (P2). El correo no sale de la sincronización.</summary>
public sealed record EmpleadoPorCorreo(int Id, string CodigoEkon, string Correo)
{
    public override string ToString() => $"EmpleadoPorCorreo({Id}, {CodigoEkon})";
}

/// <summary>
/// Una novedad a escribir. EmpleadoId: real (> 0) o temporal negativo de una persona en <see cref="LoteSincronizacion.Altas"/>.
/// </summary>
public sealed record ItemSincronizacion(PermisoMedicoMapeado Mapeado, int EmpleadoId);

/// <summary>
/// Un lote (máx. 100) que se escribe en su propia transacción. Altas: personas sin fila, activas en la API (alta
/// puntual con su cédula, D15). AhoraUtc: FechaAnulacion de las que se anulan.
/// </summary>
public sealed record LoteSincronizacion(
    IReadOnlyList<ItemSincronizacion> Items, IReadOnlyList<EmpleadoAsignable> Altas, CedulasEmpleados Cedulas, DateTime AhoraUtc);

/// <summary>Lo que el lote escribió realmente (A1: se reclasifica con lo que hay en la base dentro de la transacción).</summary>
public sealed record ResultadoLote(int Nuevas, int Actualizadas, int Anuladas, int Reactivadas, int SinCambios);

/// <summary>Lecturas masivas (sin seguimiento, en bloques de 500) y escritura por lotes de la sincronización.</summary>
public interface ISincronizacionNovedades
{
    Task<IReadOnlyList<NovedadExistente>> ObtenerExistentesPorUidAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct);

    /// <summary>Uid de las novedades (de cualquier origen) que ya usan esos códigos.</summary>
    Task<IReadOnlyDictionary<string, Guid>> ObtenerDuenosCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct);

    /// <summary>Uid de las novedades de origen PERMISOS_MEDICOS con esos LegacyId.</summary>
    Task<IReadOnlyDictionary<int, Guid>> ObtenerDuenosLegacyAsync(IReadOnlyCollection<int> legacyIds, CancellationToken ct);

    Task<IReadOnlyList<EmpleadoPorCorreo>> ObtenerEmpleadosPorCorreoAsync(IReadOnlyCollection<string> correos, CancellationToken ct);

    /// <summary>Uid de todas las novedades de origen PERMISOS_MEDICOS (para contar las ausentes en el origen).</summary>
    Task<IReadOnlyList<Guid>> ObtenerUidsPermisosMedicosAsync(CancellationToken ct);

    /// <summary>Escribe un lote. Se ejecuta DENTRO de ITransaccionAsignaciones.</summary>
    Task<ResultadoLote> AplicarLoteAsync(LoteSincronizacion lote, CancellationToken ct);
}
