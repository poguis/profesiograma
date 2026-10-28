using App.Application.Seguridad;

namespace App.Application.Empleados;

// TAREA-62: administración del criterio de personal elegible (Parametro EMPLEADO_FAMILIAS_ASIGNABLES). Solo Admin.
// Sin datos personales: familias y conteos.

/// <summary>Familia de puesto de la lista de la API (agrupada sin mayúsculas ni tildes) o del parámetro.</summary>
public sealed record FamiliaCriterioDto(string Familia, int Activos, bool Asignable);

/// <summary>
/// GET /api/admin/empleados/familias. ValorActual = texto guardado tal cual (token de concurrencia del PUT).
/// SinCriterio = el valor está vacío o falta (sin filtro).
/// </summary>
public sealed record CriterioPersonalDto(string ValorActual, bool SinCriterio, IReadOnlyList<FamiliaCriterioDto> Familias);

/// <summary>PUT /api/admin/empleados/familias-asignables. ValorAnterior = el ValorActual que vio el Admin (obligatorio).</summary>
public sealed record GuardarCriterioSolicitud(IReadOnlyList<string?>? Familias, string? ValorAnterior);

public sealed record CriterioGuardadoDto(string ValorActual);

/// <summary>Valor guardado del parámetro (null si falta la fila) y sus familias ya separadas.</summary>
public sealed record CriterioGuardado(string? Valor, IReadOnlyList<string> Familias);

public interface ICriterioPersonalRepositorio
{
    Task<CriterioGuardado> ObtenerAsync(CancellationToken ct);

    /// <summary>
    /// UPDATE … WHERE Clave = EMPLEADO_FAMILIAS_ASIGNABLES AND Valor = valorAnterior (ExecuteUpdate, compare-and-set), con
    /// ModificadoPorId y FechaModificacion explícitos (no pasa por el AuditoriaInterceptor). Devuelve las filas afectadas:
    /// 0 = otro lo cambió (o falta la fila).
    /// </summary>
    Task<int> ActualizarAsync(string valorAnterior, string valorNuevo, int usuarioId, DateTime fechaUtc, CancellationToken ct);
}

public enum EstadoGuardarCriterio { Guardado, Invalido, Conflicto }

public sealed record ResultadoGuardarCriterio(
    EstadoGuardarCriterio Estado, CriterioGuardadoDto? Guardado = null, IReadOnlyDictionary<string, string[]>? Errores = null)
{
    public const string MensajeConflicto = "Otro usuario cambió el criterio; recargue.";
}

public sealed class CriterioPersonalServicio(
    ICriterioPersonalRepositorio repositorio, IFuenteEmpleadosErp empleados, IUsuarioActual usuario, TimeProvider reloj)
{
    public const int LargoMaximo = 400; // Parametro.Valor NVARCHAR(400)
    public const string MensajeAlMenosUna = "Seleccione al menos una familia.";
    public const string MensajeVacia = "La familia no puede estar vacía.";
    public const string MensajeLargo = "El criterio supera el largo permitido.";
    public const string MensajeValorAnterior = "Falta el valor anterior del criterio; recargue.";
    public static string MensajeComa(string valor) => $"La familia {valor} no puede contener comas.";
    public static string MensajeRepetida(string valor) => $"La familia {valor} está repetida.";
    public static string MensajeNoExiste(string valor) => $"La familia {valor} no existe.";

    /// <summary>
    /// Familias de los activos de la lista en caché (agrupadas sin mayúsculas ni tildes, con su cantidad), más las del
    /// parámetro sin activos (con 0). Orden: activos descendente. Sin lista utilizable → ErpNoDisponibleException (503).
    /// </summary>
    public async Task<CriterioPersonalDto> ObtenerAsync(CancellationToken ct)
    {
        var guardado = await repositorio.ObtenerAsync(ct);
        var lista = await empleados.ObtenerActivosAsync(ct);
        var familias = AgruparFamilias(lista.Activos)
            .Select(g => new FamiliaCriterioDto(g.Familia, g.Activos, EnCriterio(g.Familia, guardado.Familias)))
            .ToList();
        familias.AddRange(guardado.Familias
            .Where(f => !familias.Any(x => ComparacionEmpleadosErp.Iguales(x.Familia, f)))
            .Select(f => new FamiliaCriterioDto(f, 0, true)));

        return new CriterioPersonalDto(guardado.Valor ?? string.Empty, guardado.Familias.Count == 0,
            familias.OrderByDescending(f => f.Activos).ThenBy(f => f.Familia, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public async Task<ResultadoGuardarCriterio> GuardarAsync(GuardarCriterioSolicitud solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var errores = new Dictionary<string, List<string>>();
        if (solicitud.ValorAnterior is null)
        {
            Agregar(errores, "valorAnterior", MensajeValorAnterior);
        }

        var familias = new List<string>();
        foreach (var bruta in solicitud.Familias ?? [])
        {
            var familia = MapeoEmpleado.Normalizar(bruta) is { } t ? string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries)) : null;
            if (familia is null)
            {
                Agregar(errores, "familias", MensajeVacia);
            }
            else if (familia.Contains(','))
            {
                Agregar(errores, "familias", MensajeComa(familia));
            }
            else if (familias.Any(f => ComparacionEmpleadosErp.Iguales(f, familia)))
            {
                Agregar(errores, "familias", MensajeRepetida(familia));
            }
            else
            {
                familias.Add(familia);
            }
        }

        if (familias.Count == 0 && !errores.ContainsKey("familias"))
        {
            Agregar(errores, "familias", MensajeAlMenosUna);
        }

        var valorNuevo = string.Join(", ", familias);
        if (valorNuevo.Length > LargoMaximo)
        {
            Agregar(errores, "familias", MensajeLargo);
        }

        // Existen = familias de la lista actual de la API (caché; sin lista utilizable → 503) o ya guardadas en el parámetro.
        // Se revisa aunque otra familia tenga un error de forma, para informar todos los errores de una vez.
        if (familias.Count > 0)
        {
            var guardado = await repositorio.ObtenerAsync(ct);
            var disponibles = AgruparFamilias((await empleados.ObtenerActivosAsync(ct)).Activos).Select(g => g.Familia)
                .Concat(guardado.Familias).ToList();
            foreach (var familia in familias.Where(f => !EnCriterio(f, disponibles)))
            {
                Agregar(errores, "familias", MensajeNoExiste(familia));
            }
        }

        if (errores.Count > 0)
        {
            return Invalido(errores);
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var fecha = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc); // DATETIME2(0)
        var filas = await repositorio.ActualizarAsync(solicitud.ValorAnterior!, valorNuevo,
            usuario.UsuarioId ?? Domain.Seguridad.Usuario.IdSistema, fecha, ct);
        return filas == 0
            ? new ResultadoGuardarCriterio(EstadoGuardarCriterio.Conflicto)
            : new ResultadoGuardarCriterio(EstadoGuardarCriterio.Guardado, new CriterioGuardadoDto(valorNuevo));
    }

    /// <summary>Grupos por familia normalizada; el texto mostrado es la forma más frecuente (recortada y con espacios colapsados).</summary>
    internal static IReadOnlyList<(string Familia, int Activos)> AgruparFamilias(IEnumerable<EmpleadoErp> activos)
    {
        var grupos = new List<(List<string> Formas, int Activos)>();
        foreach (var e in activos)
        {
            if (MapeoEmpleado.Normalizar(e.FamiliaPuesto) is not { } t)
            {
                continue;
            }

            var forma = string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var i = grupos.FindIndex(g => ComparacionEmpleadosErp.Iguales(g.Formas[0], forma));
            if (i < 0)
            {
                grupos.Add(([forma], 1));
            }
            else
            {
                grupos[i].Formas.Add(forma);
                grupos[i] = (grupos[i].Formas, grupos[i].Activos + 1);
            }
        }

        return grupos
            .Select(g => (g.Formas.GroupBy(f => f).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key, g.Activos))
            .ToList();
    }

    private static bool EnCriterio(string familia, IEnumerable<string> criterio) =>
        criterio.Any(c => ComparacionEmpleadosErp.Iguales(c, familia));

    private static void Agregar(Dictionary<string, List<string>> e, string clave, string mensaje)
    {
        if (!e.TryGetValue(clave, out var lista))
        {
            e[clave] = lista = [];
        }

        lista.Add(mensaje);
    }

    private static ResultadoGuardarCriterio Invalido(Dictionary<string, List<string>> e) =>
        new(EstadoGuardarCriterio.Invalido, Errores: e.ToDictionary(x => x.Key, x => x.Value.ToArray()));
}
