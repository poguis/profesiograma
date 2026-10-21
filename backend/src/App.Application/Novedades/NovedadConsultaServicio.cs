using App.Application.Comun;
using App.Application.Seguridad;

namespace App.Application.Novedades;

/// <summary>Listado y detalle de novedades (TAREA-23-1). La visibilidad la decide <see cref="PermisosNovedad"/> (J2).</summary>
public sealed class NovedadConsultaServicio(INovedadConsultas consultas, IUsuarioActual usuario)
{
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    public async Task<ResultadoConsulta<PaginaResultado<NovedadResumenDto>>> ListarAsync(
        NovedadListadoSolicitud solicitud, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();

        var pagina = LectorParametros.LeerPagina(solicitud.Pagina, errores);
        var tamano = LectorParametros.LeerTamano(solicitud.Tamano, TamanoPorDefecto, TamanoMaximo, errores);
        var desde = LectorParametros.LeerFecha(solicitud.Desde, "desde", errores);
        var hasta = LectorParametros.LeerFecha(solicitud.Hasta, "hasta", errores);
        var incluirAnuladas = LectorParametros.LeerBooleano(solicitud.IncluirAnuladas, "incluirAnuladas", false, errores);

        byte? aplicacionId = null;
        if (LectorParametros.Normalizar(solicitud.Aplicacion) is not null && (aplicacionId = AplicacionNovedad.Id(solicitud.Aplicacion)) is null)
        {
            errores["aplicacion"] = ["La aplicación debe ser PERSONA, PROYECTO o GENERAL."];
        }

        if (desde is not null && hasta is not null && desde > hasta)
        {
            errores["desde"] = ["La fecha 'desde' no puede ser posterior a la fecha 'hasta'."];
        }

        if (errores.Count > 0)
        {
            return ResultadoConsulta<PaginaResultado<NovedadResumenDto>>.Invalido(errores);
        }

        if (!PermisosNovedad.TryObtenerVisibilidad(usuario, out var visibleParaUsuarioId))
        {
            return ResultadoConsulta<PaginaResultado<NovedadResumenDto>>.Ok(new([], pagina, tamano, 0));
        }

        var filtro = new NovedadFiltro(
            aplicacionId,
            LectorParametros.Normalizar(solicitud.Tipo)?.ToUpperInvariant(),
            LectorParametros.Normalizar(solicitud.Origen)?.ToUpperInvariant(),
            LectorParametros.Normalizar(solicitud.Texto),
            desde, hasta, incluirAnuladas, pagina, tamano, visibleParaUsuarioId);

        return ResultadoConsulta<PaginaResultado<NovedadResumenDto>>.Ok(await consultas.ListarAsync(filtro, ct));
    }

    /// <summary>Null si no existe o no es visible (la API responde 404, nunca 403).</summary>
    public async Task<NovedadDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct)
    {
        if (!PermisosNovedad.TryObtenerVisibilidad(usuario, out var visibleParaUsuarioId)
            || await consultas.ObtenerDetalleAsync(id, visibleParaUsuarioId, ct) is not { } n)
        {
            return null;
        }

        var (puede, motivo) = PermisosNovedad.Modificacion(usuario, n);
        return new NovedadDetalleDto(
            n.Id, n.Codigo, AplicacionNovedad.Codigo(n.AplicacionId), n.Tipo, n.OrigenCodigo, n.Sujeto,
            n.FechaInicio, n.FechaFin, n.Dias, n.Observacion,
            n.Anulada, n.FechaAnulacion, n.MotivoAnulacion,
            PermisosNovedad.VeRegistrante(usuario) ? new RegistroNovedadDto(n.RegistradoPorNombre, n.RegistradoPorCorreo) : null,
            n.FechaCreacion, Convert.ToBase64String(n.RowVer),
            puede, puede, motivo);
    }
}
