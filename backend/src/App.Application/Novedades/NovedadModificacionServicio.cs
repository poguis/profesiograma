using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Seguridad;
using App.Domain.Novedades;
using App.Domain.Seguridad;

namespace App.Application.Novedades;

/// <summary>
/// Editar y anular una novedad (TAREA-23-2). Orden de errores (H5): version ausente o ilegible (400) → no existe o no
/// visible (404) → origen no editable (400 `origen`) → sin permiso (403) → anulada (409) → versión distinta (409) →
/// campos (400) → dentro de ITransaccionAsignaciones: anulada y versión de nuevo (409), duplicado (409, solo editar) →
/// escritura con el token del cliente como OriginalValue de RowVer (R1; otro cambio entre medio → 409).
/// No llama a la API de empleados. Después del commit no vuelve a la base: la versión sale del SaveChanges (A2).
/// </summary>
public sealed class NovedadModificacionServicio(
    NovedadValidador validador,
    INovedadModificacion modificacion,
    INovedadRepositorio repositorio,
    ITransaccionAsignaciones transaccion,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const int MotivoMinimo = 5, MotivoMaximo = 500;
    public const string MensajeMotivo = "El motivo debe tener entre 5 y 500 caracteres.";

    public async Task<ResultadoModificacionNovedad> EditarAsync(int id, EditarNovedadSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var (previa, guardada, token, visible) = await ComprobarAsync(id, s.Version, PermisosNovedad.MensajeAnulada, ct);
        if (previa is not null)
        {
            return previa;
        }

        var (edicion, errores) = await validador.ValidarEdicionAsync(guardada!, s, ct);
        if (edicion is null)
        {
            return ResultadoModificacionNovedad.Invalido(errores);
        }

        var (desde, hasta) = DiasNovedad.Limites(edicion.Dias);
        return await transaccion.EjecutarAsync(async ctTx =>
        {
            if (await RevisarDentroAsync(id, token!, visible, PermisosNovedad.MensajeAnulada, ctTx) is { } dentro)
            {
                return dentro;
            }

            // N7 en la edición: los días resultantes (con huecos) y sin contar la propia novedad.
            var criterio = new CriterioDuplicado(guardada!.AplicacionId, edicion.Tipo.Id, guardada.EmpleadoId, guardada.ProyectoId,
                desde, hasta, edicion.Dias, id);
            if (await repositorio.BuscarDuplicadoAsync(criterio, visible, ctTx) is { } duplicado)
            {
                return ResultadoModificacionNovedad.Duplicado(duplicado);
            }

            try
            {
                var version = await modificacion.EditarAsync(new EdicionNovedad(id, token!, edicion.Tipo.Id, edicion.Dias, edicion.Observacion), ctTx);
                return ResultadoModificacionNovedad.Exito(new NovedadEditadaDto(id, version));
            }
            catch (ConflictoConcurrenciaException)
            {
                return ResultadoModificacionNovedad.Cambio();
            }
        }, r => r.Estado == EstadoModificacionNovedad.Hecho, ct);
    }

    public async Task<ResultadoModificacionNovedad> AnularAsync(int id, AnularNovedadSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var (previa, _, token, visible) = await ComprobarAsync(id, s.Version, ResultadoModificacionNovedad.MensajeYaAnulada, ct);
        if (previa is not null)
        {
            return previa;
        }

        var motivo = s.Motivo?.Trim();
        if (motivo is not { Length: >= MotivoMinimo and <= MotivoMaximo })
        {
            return ResultadoModificacionNovedad.Invalido("motivo", MensajeMotivo);
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var fecha = new DateTime(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc); // DATETIME2(0)
        return await transaccion.EjecutarAsync(async ctTx =>
        {
            if (await RevisarDentroAsync(id, token!, visible, ResultadoModificacionNovedad.MensajeYaAnulada, ctTx) is { } dentro)
            {
                return dentro;
            }

            try
            {
                var version = await modificacion.AnularAsync(
                    new AnulacionNovedad(id, token!, motivo, usuario.UsuarioId ?? Usuario.IdSistema, fecha), ctTx);
                return ResultadoModificacionNovedad.Exito(new NovedadEditadaDto(id, version));
            }
            catch (ConflictoConcurrenciaException)
            {
                return ResultadoModificacionNovedad.Cambio();
            }
        }, r => r.Estado == EstadoModificacionNovedad.Hecho, ct);
    }

    /// <summary>
    /// H5 hasta la versión (fuera de la transacción). Devuelve el resultado si hay que cortar; si no, la novedad guardada,
    /// el token y la visibilidad. La lectura es sin seguimiento: la escritura vuelve a leer con seguimiento dentro de la
    /// transacción.
    /// </summary>
    private async Task<(ResultadoModificacionNovedad? Previa, NovedadParaModificar? Guardada, byte[]? Token, int? Visible)> ComprobarAsync(
        int id, string? version, string mensajeAnulada, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        var token = VersionNovedad.Leer(version, errores);
        if (token is null)
        {
            return (ResultadoModificacionNovedad.Invalido(errores), null, null, null);
        }

        if (!PermisosNovedad.TryObtenerVisibilidad(usuario, out var visible)
            || await modificacion.ObtenerParaModificarAsync(id, visible, ct) is not { } n)
        {
            return (ResultadoModificacionNovedad.NoEncontrada(), null, null, null);
        }

        var corte = !n.OrigenEditable ? ResultadoModificacionNovedad.Invalido("origen", PermisosNovedad.MensajeSoloLectura(n.OrigenCodigo))
            : !PermisosNovedad.PuedeModificar(usuario, n.AplicacionId, n.CreadoPorId, n.PropietarioProyectoId) ? ResultadoModificacionNovedad.Prohibido()
            : n.Anulada ? ResultadoModificacionNovedad.Anulada(mensajeAnulada)
            : !VersionNovedad.Igual(token, n.RowVer) ? ResultadoModificacionNovedad.Cambio()
            : null;
        return corte is null ? (null, n, token, visible) : (corte, null, null, null);
    }

    /// <summary>Dentro de la transacción: la novedad sigue visible, sin anular y con la misma versión.</summary>
    private async Task<ResultadoModificacionNovedad?> RevisarDentroAsync(int id, byte[] token, int? visible, string mensajeAnulada, CancellationToken ct)
    {
        var actual = await modificacion.ObtenerParaModificarAsync(id, visible, ct);
        return actual is null ? ResultadoModificacionNovedad.NoEncontrada()
            : actual.Anulada ? ResultadoModificacionNovedad.Anulada(mensajeAnulada)
            : !VersionNovedad.Igual(token, actual.RowVer) ? ResultadoModificacionNovedad.Cambio()
            : null;
    }
}
