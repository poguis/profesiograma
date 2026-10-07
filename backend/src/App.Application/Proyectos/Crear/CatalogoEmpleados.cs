using App.Application.Empleados;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Crear;

/// <summary>Cómo identifica la solicitud a una persona NUEVA (TAREA-26d): codigoEkon o, en la transición, empleadoId.</summary>
public readonly record struct ReferenciaEmpleado(int? EmpleadoId, string? CodigoEkon);

/// <summary>
/// Empleados que una solicitud puede asignar como personas NUEVAS (TAREA-26d, opción C). Se carga ANTES de validar
/// (async: base + API) y los validadores lo consultan de forma síncrona con <see cref="Validar"/>:
///  - por codigoEkon (P2) o por empleadoId (transición hasta la 26d-3; ambos o ninguno → 400);
///  - "activo" = está en la lista de la API (fresca al registrar, en caché en la vista previa);
///  - Id real si la persona ya tiene fila en Empleado; si no, un Id TEMPORAL negativo (-1, -2…) que la alta puntual
///    convierte en real dentro de la transacción.
/// </summary>
public sealed class CatalogoEmpleados
{
    public const string MensajeAmbos = "Indique codigoEkon o empleadoId, no ambos.";
    public const string MensajeObligatorio = "El empleado es obligatorio.";

    public static readonly CatalogoEmpleados Vacio = new(
        new Dictionary<int, EmpleadoAsignable>(), new Dictionary<string, int>(), new Dictionary<string, EmpleadoErp>());

    private readonly IReadOnlyDictionary<int, EmpleadoAsignable> filasPorId;
    private readonly IReadOnlyDictionary<string, int> idPorCodigo;
    private readonly IReadOnlyDictionary<string, EmpleadoErp> activos;
    private readonly IReadOnlyDictionary<string, EmpleadoAsignable>? fijos;

    private CatalogoEmpleados(
        IReadOnlyDictionary<int, EmpleadoAsignable> filasPorId, IReadOnlyDictionary<string, int> idPorCodigo,
        IReadOnlyDictionary<string, EmpleadoErp> activos, IReadOnlyDictionary<string, EmpleadoAsignable>? fijos = null)
    {
        this.filasPorId = filasPorId;
        this.activos = activos;
        this.fijos = fijos;

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

    /// <summary>
    /// Carga lo necesario para las referencias indicadas: filas por Id (transición), Id reales por código y la lista de
    /// la API (fresca = registro, P5; en caché = vista previa). Sin referencias no consulta nada.
    /// </summary>
    /// <exception cref="Erp.ErpNoDisponibleException">La API de empleados no respondió (503, sin escribir).</exception>
    public static async Task<CatalogoEmpleados> CargarAsync(IDatosReferenciaProyecto datos, IFuenteEmpleadosErp fuente,
        IEnumerable<ReferenciaEmpleado> referencias, bool fresco, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(fuente);
        var lista = referencias.Where(r => r.EmpleadoId is not null || MapeoEmpleado.Normalizar(r.CodigoEkon) is not null).ToList();
        if (lista.Count == 0)
        {
            return Vacio;
        }

        var ids = lista.Select(r => r.EmpleadoId).OfType<int>().Distinct().ToList();
        var filas = ids.Count == 0 ? new Dictionary<int, EmpleadoAsignable>() : await datos.ObtenerEmpleadosPorIdsAsync(ids, ct);

        var api = fresco ? await fuente.ObtenerActivosFrescosAsync(ct) : await fuente.ObtenerActivosAsync(ct);

        var codigos = lista.Select(r => MapeoEmpleado.Normalizar(r.CodigoEkon)).OfType<string>()
            .Concat(filas.Values.Select(f => f.CodigoEkon))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var activos = codigos
            .Where(api.PorCodigo.ContainsKey)
            .ToDictionary(c => c, c => api.PorCodigo[c], StringComparer.OrdinalIgnoreCase);
        var idsPorCodigo = activos.Count == 0
            ? new Dictionary<string, int>()
            : await datos.ObtenerIdsPorCodigosAsync(activos.Keys.ToList(), ct);

        return new CatalogoEmpleados(filas, idsPorCodigo, activos);
    }

    /// <summary>
    /// Catálogo armado en memoria (pruebas y vistas sin API): esos empleados tienen fila y están activos en la API con
    /// el mismo nombre y puesto.
    /// </summary>
    public static CatalogoEmpleados DesdeActivos(IEnumerable<EmpleadoAsignable> activos)
    {
        var lista = activos.ToList();
        return new CatalogoEmpleados(
            lista.ToDictionary(a => a.Id),
            lista.ToDictionary(a => a.CodigoEkon, a => a.Id, StringComparer.OrdinalIgnoreCase),
            lista.ToDictionary(a => a.CodigoEkon,
                a => a.Erp ?? new EmpleadoErp(a.CodigoEkon, a.NombreCompleto, null, null, null, null, null, null, a.Puesto,
                    null, null, null, null, null, null, null, null, null, MapeoEmpleado.EstadoActivo),
                StringComparer.OrdinalIgnoreCase),
            lista.ToDictionary(a => a.CodigoEkon, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Persona nueva: exactamente uno de empleadoId / codigoEkon y activo en la API. Los errores van a
    /// "{clave}.empleadoId" o "{clave}.codigoEkon" (según lo que se envió) con el identificador recibido.
    /// </summary>
    public EmpleadoAsignable? Validar(int? empleadoId, string? codigoEkon, string clave, Dictionary<string, List<string>> e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var codigoEnviado = MapeoEmpleado.Normalizar(codigoEkon);
        if (empleadoId is not null && codigoEnviado is not null)
        {
            Agregar(e, $"{clave}.codigoEkon", MensajeAmbos);
            return null;
        }

        string codigo;
        string campo;
        string identificador;
        if (empleadoId is int id)
        {
            campo = "empleadoId";
            identificador = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!filasPorId.TryGetValue(id, out var fila))
            {
                Agregar(e, $"{clave}.{campo}", Mensaje(identificador));
                return null;
            }

            codigo = fila.CodigoEkon;
        }
        else if (codigoEnviado is not null)
        {
            campo = "codigoEkon";
            identificador = codigoEnviado;
            codigo = codigoEnviado;
        }
        else
        {
            Agregar(e, $"{clave}.empleadoId", MensajeObligatorio);
            return null;
        }

        if (!activos.TryGetValue(codigo, out var erp))
        {
            Agregar(e, $"{clave}.{campo}", Mensaje(identificador));
            return null;
        }

        if (fijos is not null && fijos.TryGetValue(codigo, out var fijo))
        {
            return fijo; // DesdeActivos: el empleado tal como se indicó
        }

        return new EmpleadoAsignable(idPorCodigo[codigo], erp.CodigoEkon!, erp.NombreCompleto!, MapeoEmpleado.Normalizar(erp.Puesto), erp);
    }

    private static string Mensaje(string identificador) => $"El empleado {identificador} no existe o no está activo.";
}
