using System.Globalization;
using App.Application.Comun;

namespace App.Application.Empleados;

/// <summary>
/// Filtro y paginación del buscador sobre la lista de la API, en memoria (TAREA-26d). Lógica pura.
/// Texto sin distinguir mayúsculas ni tildes (P13) sobre nombre completo, apellidos, nombres y código EKON.
/// Departamentos POR NOMBRE con el departamento o la unidad del empleado (sin distinguir mayúsculas).
/// </summary>
public static class BusquedaEmpleados
{
    public const string AvisoListaAnterior = "Lista de empleados de las {0:HH:mm}; el ERP no respondió.";

    private static readonly CompareInfo Comparador = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions SinMayusculasNiTildes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
    private static readonly StringComparer OrdenNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-EC"), ignoreCase: true);

    public static ResultadoBusquedaEmpleadosDto Buscar(ListaEmpleadosErp lista, EmpleadoFiltro filtro)
    {
        ArgumentNullException.ThrowIfNull(lista);
        ArgumentNullException.ThrowIfNull(filtro);

        IEnumerable<EmpleadoErp> consulta = lista.Activos;
        if (filtro.Texto is { } texto)
        {
            consulta = consulta.Where(e => Contiene(e.NombreCompleto, texto) || Contiene(e.Apellidos, texto)
                                           || Contiene(e.Nombres, texto) || Contiene(e.CodigoEkon, texto));
        }

        if (filtro.Departamentos is { } departamentos)
        {
            var permitidos = departamentos.Select(d => d.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            consulta = consulta.Where(e => EnDepartamento(e.Departamento, permitidos) || EnDepartamento(e.Unidad, permitidos));
        }

        var filtrados = consulta
            .OrderBy(e => e.NombreCompleto, OrdenNombre)
            .ThenBy(e => e.CodigoEkon, StringComparer.Ordinal)
            .ToList();
        var items = filtrados
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(e => new EmpleadoBusquedaDto(e.CodigoEkon!, e.NombreCompleto!, MapeoEmpleado.Normalizar(e.Puesto),
                MapeoEmpleado.Normalizar(e.Departamento), MapeoEmpleado.Normalizar(e.Unidad)))
            .ToList();

        var aviso = lista.EsAnterior
            ? string.Format(CultureInfo.InvariantCulture, AvisoListaAnterior, FechaNegocio.Hora(lista.ObtenidaUtc))
            : null;
        return new ResultadoBusquedaEmpleadosDto(items, filtro.Pagina, filtro.Tamano, filtrados.Count, aviso);
    }

    private static bool Contiene(string? valor, string texto) =>
        valor is not null && Comparador.IndexOf(valor, texto, SinMayusculasNiTildes) >= 0;

    private static bool EnDepartamento(string? valor, HashSet<string> permitidos) =>
        MapeoEmpleado.Normalizar(valor) is { } nombre && permitidos.Contains(nombre);
}
