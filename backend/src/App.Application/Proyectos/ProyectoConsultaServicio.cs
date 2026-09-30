using System.Globalization;
using App.Application.Comun;
using App.Application.Seguridad;

namespace App.Application.Proyectos;

/// <summary>
/// Consultas de proyectos para la API. Único lugar donde se decide la visibilidad (R1)
/// y se validan los parámetros del listado (R3).
/// </summary>
public sealed class ProyectoConsultaServicio(IProyectoConsultas consultas, IUsuarioActual usuario, TimeProvider reloj)
{
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    // [PENDIENTE] Leer de Parametro.ZONA_HORARIA (hoy es el mismo valor que usa el sembrador de datos de prueba).
    private const string ZonaNegocio = "SA Pacific Standard Time";
    private static readonly Lazy<TimeZoneInfo> Zona = new(() => TimeZoneInfo.FindSystemTimeZoneById(ZonaNegocio));

    private const string FormatoFecha = "yyyy-MM-dd";

    public async Task<ResultadoConsulta<PaginaResultado<ProyectoResumenDto>>> ListarAsync(
        ProyectoListadoSolicitud solicitud, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();

        var pagina = LeerEntero(solicitud.Pagina, "pagina", 1, 1, int.MaxValue,
            "La página debe ser un número entero mayor o igual a 1.", errores);
        var tamano = LeerEntero(solicitud.Tamano, "tamano", TamanoPorDefecto, 1, TamanoMaximo,
            $"El tamaño de página debe ser un número entero entre 1 y {TamanoMaximo}.", errores);
        var desde = LeerFecha(solicitud.Desde, "desde", errores);
        var hasta = LeerFecha(solicitud.Hasta, "hasta", errores);

        if (desde is not null && hasta is not null && desde > hasta)
        {
            errores["desde"] = ["La fecha 'desde' no puede ser posterior a la fecha 'hasta'."];
        }

        if (errores.Count > 0)
        {
            return ResultadoConsulta<PaginaResultado<ProyectoResumenDto>>.Invalido(errores);
        }

        if (!TryObtenerVisibilidad(out var propietarioUsuarioId))
        {
            return ResultadoConsulta<PaginaResultado<ProyectoResumenDto>>.Ok(new([], pagina, tamano, 0));
        }

        var filtro = new ProyectoFiltro(
            Normalizar(solicitud.Estado), Normalizar(solicitud.Grupo), Normalizar(solicitud.Texto),
            desde, hasta, pagina, tamano, propietarioUsuarioId);

        return ResultadoConsulta<PaginaResultado<ProyectoResumenDto>>.Ok(await consultas.ListarAsync(filtro, ct));
    }

    /// <summary>Null si no existe o no es visible para el usuario actual (la API responde 404, nunca 403).</summary>
    public async Task<ProyectoDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct)
    {
        if (!TryObtenerVisibilidad(out var propietarioUsuarioId))
        {
            return null;
        }

        return await consultas.ObtenerDetalleAsync(id, propietarioUsuarioId, HoyEcuador(), ct);
    }

    /// <summary>
    /// R1: Admin ve todos (null); cualquier otro rol solo sus proyectos (PropietarioUsuarioId = usuario actual).
    /// Devuelve false si no hay usuario resuelto (no debería ocurrir en endpoints autenticados).
    /// </summary>
    private bool TryObtenerVisibilidad(out int? propietarioUsuarioId)
    {
        if (usuario.TieneRol(RolesApp.Admin))
        {
            propietarioUsuarioId = null;
            return true;
        }

        propietarioUsuarioId = usuario.UsuarioId;
        return propietarioUsuarioId is not null;
    }

    private DateOnly HoyEcuador()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona.Value).DateTime);

    private static string? Normalizar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static int LeerEntero(string? valor, string campo, int porDefecto, int minimo, int maximo,
        string mensaje, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return porDefecto;
        }

        if (int.TryParse(valor.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            && numero >= minimo && numero <= maximo)
        {
            return numero;
        }

        errores[campo] = [mensaje];
        return porDefecto;
    }

    private static DateOnly? LeerFecha(string? valor, string campo, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (DateOnly.TryParseExact(valor.Trim(), FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            return fecha;
        }

        errores[campo] = [$"La fecha '{campo}' debe tener el formato {FormatoFecha}."];
        return null;
    }
}
