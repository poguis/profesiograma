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

    public async Task<ResultadoConsulta<PaginaResultado<ProyectoResumenDto>>> ListarAsync(
        ProyectoListadoSolicitud solicitud, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();

        var pagina = LectorParametros.LeerPagina(solicitud.Pagina, errores);
        var tamano = LectorParametros.LeerTamano(solicitud.Tamano, TamanoPorDefecto, TamanoMaximo, errores);
        var desde = LectorParametros.LeerFecha(solicitud.Desde, "desde", errores);
        var hasta = LectorParametros.LeerFecha(solicitud.Hasta, "hasta", errores);

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
            LectorParametros.Normalizar(solicitud.Estado), LectorParametros.Normalizar(solicitud.Grupo),
            LectorParametros.Normalizar(solicitud.Texto),
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

    private DateOnly HoyEcuador() => FechaNegocio.Hoy(reloj);
}
