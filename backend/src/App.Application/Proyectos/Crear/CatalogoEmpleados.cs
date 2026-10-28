using App.Application.Empleados;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// Empleados que una solicitud puede asignar como personas NUEVAS (TAREA-26d, opción C). Se carga ANTES de validar
/// (async: base + API) y los validadores lo consultan de forma síncrona con <see cref="Validar"/>:
///  - por codigoEkon (P2; desde la TAREA-26d-3 es la única forma: se retiró la transición con empleadoId);
///  - "activo" = está en la lista de la API (fresca al registrar, en caché en la vista previa);
///  - Id real si la persona ya tiene fila en Empleado; si no, un Id TEMPORAL negativo (-1, -2…) que la alta puntual
///    convierte en real dentro de la transacción;
///  - <see cref="Cedulas"/> (TAREA-22-1, D15): las de esos códigos, solo con la lista fresca (registro); vacías en la vista
///    previa. Viajan aparte de EmpleadoAsignable (record) hasta la alta puntual.
/// </summary>
public sealed class CatalogoEmpleados
{
    public const string MensajeObligatorio = "El empleado es obligatorio.";

    public static readonly CatalogoEmpleados Vacio = new(new Dictionary<string, int>(), new Dictionary<string, EmpleadoErp>());

    private readonly IReadOnlyDictionary<string, int> idPorCodigo;
    private readonly IReadOnlyDictionary<string, EmpleadoErp> activos;
    private readonly IReadOnlyDictionary<string, EmpleadoAsignable>? fijos;
    private readonly IReadOnlySet<string> noAsignables;

    private CatalogoEmpleados(
        IReadOnlyDictionary<string, int> idPorCodigo, IReadOnlyDictionary<string, EmpleadoErp> activos,
        IReadOnlyDictionary<string, EmpleadoAsignable>? fijos = null, CedulasEmpleados? cedulas = null,
        IReadOnlySet<string>? noAsignables = null)
    {
        this.activos = activos;
        this.fijos = fijos;
        this.noAsignables = noAsignables ?? new HashSet<string>();
        Cedulas = cedulas ?? CedulasEmpleados.Vacio;

        // Id real si la persona tiene fila; si no, temporal negativo en orden de aparición (determinista).
        var ids = new Dictionary<string, int>(idPorCodigo, StringComparer.OrdinalIgnoreCase);
        var temporal = 0;
        foreach (var codigo in activos.Keys.Order(StringComparer.Ordinal))
        {
            if (!ids.ContainsKey(codigo))
            {
                ids[codigo] = --temporal;
            }
        }

        this.idPorCodigo = ids;
    }

    /// <summary>Cédulas de la API de las personas del catálogo (solo con la lista fresca). Para la alta puntual.</summary>
    public CedulasEmpleados Cedulas { get; }

    /// <summary>
    /// Carga lo necesario para los códigos indicados: la lista de la API (fresca = registro, P5; en caché = vista previa)
    /// y los Id reales de los que ya tienen fila. Sin códigos no consulta nada.
    /// </summary>
    /// <exception cref="Erp.ErpNoDisponibleException">La API de empleados no respondió (503, sin escribir).</exception>
    public static async Task<CatalogoEmpleados> CargarAsync(IDatosReferenciaProyecto datos, IFuenteEmpleadosErp fuente,
        IEnumerable<string?> codigosEkon, bool fresco, CancellationToken ct, IEnumerable<string>? exentos = null)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(fuente);
        var codigos = codigosEkon.Select(MapeoEmpleado.Normalizar).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codigos.Count == 0)
        {
            return Vacio;
        }

        var api = fresco ? await fuente.ObtenerActivosFrescosAsync(ct) : await fuente.ObtenerActivosAsync(ct);

        var activos = codigos
            .Where(api.PorCodigo.ContainsKey)
            .ToDictionary(c => c, c => api.PorCodigo[c], StringComparer.OrdinalIgnoreCase);
        var idsPorCodigo = activos.Count == 0
            ? new Dictionary<string, int>()
            : await datos.ObtenerIdsPorCodigosAsync(activos.Keys.ToList(), ct);

        // TAREA-62: criterio de personal elegible, leído en este momento (sin caché). Los exentos (personas que ya están
        // en ESE proyecto: actualizar personal y reactivar) no se filtran.
        IReadOnlyList<string> familias = activos.Count == 0 ? [] : await datos.ObtenerFamiliasAsignablesAsync(ct);
        var exentosNormalizados = (exentos ?? []).Select(MapeoEmpleado.Normalizar).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var noAsignables = activos
            .Where(a => !exentosNormalizados.Contains(a.Key) && !ReglasPersonalElegible.EsAsignable(a.Value, familias))
            .Select(a => a.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new CatalogoEmpleados(idsPorCodigo, activos, cedulas: api.Cedulas.Solo(activos.Keys), noAsignables: noAsignables);
    }

    /// <summary>
    /// Catálogo armado en memoria (pruebas y vistas sin API): esos empleados tienen fila y están activos en la API con
    /// el mismo nombre y puesto.
    /// </summary>
    public static CatalogoEmpleados DesdeActivos(IEnumerable<EmpleadoAsignable> activos)
    {
        var lista = activos.ToList();
        return new CatalogoEmpleados(
            lista.ToDictionary(a => a.CodigoEkon, a => a.Id, StringComparer.OrdinalIgnoreCase),
            lista.ToDictionary(a => a.CodigoEkon,
                a => a.Erp ?? new EmpleadoErp(a.CodigoEkon, a.NombreCompleto, null, null, null, null, null, null, a.Puesto,
                    null, null, null, null, null, null, null, null, null, MapeoEmpleado.EstadoActivo),
                StringComparer.OrdinalIgnoreCase),
            lista.ToDictionary(a => a.CodigoEkon, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>TAREA-62: el código está activo en la API pero no pertenece a un grupo asignable (y no está exento).</summary>
    public bool NoEsAsignable(string? codigoEkon) =>
        MapeoEmpleado.Normalizar(codigoEkon) is { } codigo && noAsignables.Contains(codigo);

    /// <summary>
    /// Persona nueva: codigoEkon obligatorio y activo en la API. Los errores van a "{clave}.codigoEkon" con el código
    /// recibido.
    /// </summary>
    public EmpleadoAsignable? Validar(string? codigoEkon, string clave, Dictionary<string, List<string>> e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var codigo = MapeoEmpleado.Normalizar(codigoEkon);
        if (codigo is null)
        {
            Agregar(e, $"{clave}.codigoEkon", MensajeObligatorio);
            return null;
        }

        if (!activos.TryGetValue(codigo, out var erp))
        {
            Agregar(e, $"{clave}.codigoEkon", $"El empleado {codigo} no existe o no está activo.");
            return null;
        }

        if (noAsignables.Contains(codigo))
        {
            Agregar(e, $"{clave}.codigoEkon", ReglasPersonalElegible.MensajeNoAsignable(codigo)); // TAREA-62, después de «no activo»
            return null;
        }

        if (fijos is not null && fijos.TryGetValue(codigo, out var fijo))
        {
            return fijo; // DesdeActivos: el empleado tal como se indicó
        }

        return new EmpleadoAsignable(idPorCodigo[codigo], erp.CodigoEkon!, erp.NombreCompleto!, MapeoEmpleado.Normalizar(erp.Puesto), erp);
    }
}
