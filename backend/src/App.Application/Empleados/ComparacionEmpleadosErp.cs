using System.Globalization;
using App.Application.Comun;

namespace App.Application.Empleados;

// TAREA-26d-3: avisos "guardado frente al ERP" por persona (solo informan; D1). Ver docs/fases/FASE_4_Empleados_API.md §8.

/// <summary>Datos laborales guardados en Empleado de una persona asignada (sin cédula ni correo).</summary>
public sealed record EmpleadoGuardado(int Id, string CodigoEkon, string NombreCompleto, string? Puesto, string? Departamento, string? Unidad);

/// <summary>Tipo = NO_ACTIVO | CAMBIO_NOMBRE | CAMBIO_CARGO | CAMBIO_DEPARTAMENTO. Guardado/Erp: valores comparados (null en NO_ACTIVO).</summary>
public sealed record AvisoErpDto(string Tipo, string Mensaje, string? Guardado, string? Erp);

public sealed record AvisosPersonaErpDto(string CodigoEkon, IReadOnlyList<AvisoErpDto> Avisos);

/// <summary>
/// GET /api/proyectos/{id}/personal/comparacion-erp. Personas: solo las que tienen avisos. AvisoGeneral: ERP caído
/// (sin comparar) o lista anterior. ComparadoUtc: momento de la lista usada (null si no se comparó).
/// </summary>
public sealed record ComparacionErpDto(DateTimeOffset? ComparadoUtc, string? AvisoGeneral, IReadOnlyList<AvisosPersonaErpDto> Personas);

/// <summary>
/// Compara lo guardado en Empleado con la lista de activos de la API. Lógica pura. Normalización coherente con el
/// buscador: espacios extremos fuera, espacios internos colapsados, vacío = null, sin distinguir mayúsculas ni tildes;
/// el puesto, además, con <see cref="MapeoEmpleado.NormalizarCargo"/>.
/// </summary>
public static class ComparacionEmpleadosErp
{
    public const string NoActivo = "NO_ACTIVO";
    public const string CambioNombre = "CAMBIO_NOMBRE";
    public const string CambioCargo = "CAMBIO_CARGO";
    public const string CambioDepartamento = "CAMBIO_DEPARTAMENTO";

    /// <summary>D4: único texto con el ERP caído (también reemplaza el de la propuesta R7 de la reactivación).</summary>
    public const string MensajeNoComparado = "No se pudo comparar con el ERP.";
    public const string MensajeListaAnterior = "Comparado con la lista de las {0:HH:mm}; el ERP no respondió.";
    public const string MensajeNoActivo = "Ya no está activo en el ERP.";
    private const string Vacio = "(vacío)";

    private static readonly CompareInfo Comparador = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions SinMayusculasNiTildes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static ComparacionErpDto NoComparado() => new(null, MensajeNoComparado, []);

    public static ComparacionErpDto Comparar(IEnumerable<EmpleadoGuardado> guardados, ListaEmpleadosErp lista)
    {
        ArgumentNullException.ThrowIfNull(guardados);
        ArgumentNullException.ThrowIfNull(lista);
        var personas = guardados
            .DistinctBy(g => g.CodigoEkon, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.CodigoEkon, StringComparer.Ordinal)
            .Select(g => new AvisosPersonaErpDto(g.CodigoEkon, Avisos(g, lista.PorCodigo.GetValueOrDefault(g.CodigoEkon))))
            .Where(p => p.Avisos.Count > 0)
            .ToList();
        var aviso = lista.EsAnterior
            ? string.Format(CultureInfo.InvariantCulture, MensajeListaAnterior, FechaNegocio.Hora(lista.ObtenidaUtc))
            : null;
        return new ComparacionErpDto(lista.ObtenidaUtc, aviso, personas);
    }

    /// <summary>Avisos de una persona: no activo (no está en la lista) o los campos que cambiaron.</summary>
    public static IReadOnlyList<AvisoErpDto> Avisos(EmpleadoGuardado guardado, EmpleadoErp? erp)
    {
        ArgumentNullException.ThrowIfNull(guardado);
        if (erp is null)
        {
            return [new AvisoErpDto(NoActivo, MensajeNoActivo, null, null)];
        }

        var avisos = new List<AvisoErpDto>();
        if (!Iguales(guardado.NombreCompleto, erp.NombreCompleto))
        {
            avisos.Add(Cambio(CambioNombre, "El nombre en el ERP es distinto", Texto(guardado.NombreCompleto), Texto(erp.NombreCompleto)));
        }

        var puestoGuardado = MapeoEmpleado.NormalizarCargo(Texto(guardado.Puesto));
        var puestoErp = MapeoEmpleado.NormalizarCargo(Texto(erp.Puesto));
        if (!Iguales(puestoGuardado, puestoErp))
        {
            avisos.Add(Cambio(CambioCargo, "El cargo en el ERP cambió", puestoGuardado, puestoErp));
        }

        // D5: la unidad va dentro del aviso de departamento (una unidad igual al departamento no cuenta, como en el buscador).
        var ubicacionGuardada = Ubicacion(guardado.Departamento, guardado.Unidad);
        var ubicacionErp = Ubicacion(erp.Departamento, erp.Unidad);
        if (!Iguales(ubicacionGuardada, ubicacionErp))
        {
            avisos.Add(Cambio(CambioDepartamento, "El departamento o la unidad en el ERP cambió", ubicacionGuardada, ubicacionErp));
        }

        return avisos;
    }

    /// <summary>Iguales tras normalizar (null = vacío), sin distinguir mayúsculas ni tildes.</summary>
    public static bool Iguales(string? a, string? b)
    {
        var x = Texto(a);
        var y = Texto(b);
        return x is null || y is null ? x is null && y is null : Comparador.Compare(x, y, SinMayusculasNiTildes) == 0;
    }

    /// <summary>Espacios extremos fuera, internos colapsados; vacío → null.</summary>
    private static string? Texto(string? valor) =>
        MapeoEmpleado.Normalizar(valor) is { } t ? string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries)) : null;

    /// <summary>"DEPARTAMENTO · UNIDAD" (la unidad solo si es distinta del departamento, como en el buscador).</summary>
    private static string? Ubicacion(string? departamento, string? unidad)
    {
        var d = Texto(departamento);
        var u = Texto(unidad);
        var partes = new[] { d, u is not null && !Iguales(u, d) ? u : null }.OfType<string>().ToList();
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static AvisoErpDto Cambio(string tipo, string texto, string? guardado, string? erp) =>
        new(tipo, $"{texto}: {guardado ?? Vacio} → {erp ?? Vacio}.", guardado, erp);
}
