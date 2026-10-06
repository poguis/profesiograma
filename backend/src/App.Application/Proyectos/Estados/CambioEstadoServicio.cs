using App.Application.Comun;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Estados;

/// <summary>
/// Caso de uso "Cambio de estado" (TAREA-14: SUSPENSION y CIERRE; FASE_5_Estados_Proyecto.md).
/// Validar → RecorteProyecto → previsualizar (no guarda) o aplicar en una transacción con applock (§7.1):
/// dentro del bloqueo se vuelve a leer el proyecto, se comprueba que el estado no cambió (409) y se recalcula el plan.
/// </summary>
public sealed class CambioEstadoServicio(
    ICambioEstadoRepositorio repositorio,
    CambioEstadoValidador validador,
    ITransaccionAsignaciones transaccion,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string AdvertenciaDiasTranscurridos = "Se eliminarán días ya transcurridos.";

    public async Task<ResultadoCambioEstado> PrevisualizarAsync(int proyectoId, CambioEstadoSolicitud solicitud, CancellationToken ct)
    {
        var preparado = await PrepararAsync(proyectoId, solicitud, ct);
        return preparado.Error ?? ResultadoCambioEstado.Previsualizado(CrearPrevisualizacion(preparado.Calculo!));
    }

    public async Task<ResultadoCambioEstado> AplicarAsync(int proyectoId, CambioEstadoSolicitud solicitud, CancellationToken ct)
    {
        // 1. Validación y plan fuera de la transacción (rápido; evita abrir el bloqueo si la solicitud no es válida).
        var preparado = await PrepararAsync(proyectoId, solicitud, ct);
        if (preparado.Error is { } error)
        {
            return error;
        }

        var previo = preparado.Calculo!;
        return await transaccion.EjecutarAsync(async ctTx =>
        {
            // 2. Dentro del applock: volver a leer y recalcular el plan sobre esos datos (no se reutiliza el de fuera).
            var datos = await repositorio.ObtenerAsync(proyectoId, previo.PropietarioUsuarioId, previo.Fecha, ctTx);
            if (datos is null)
            {
                return ResultadoCambioEstado.NoEncontrado();
            }

            if (!string.Equals(datos.EstadoCodigo, previo.Datos.EstadoCodigo, StringComparison.OrdinalIgnoreCase))
            {
                return ResultadoCambioEstado.Conflicto();
            }

            // Si cambiaron las fechas del proyecto y la fecha ya no es válida, también es un conflicto.
            if (!validador.Validar(solicitud, datos.EstadoCodigo, datos.FechaInicio, datos.FechaFin).EsValido)
            {
                return ResultadoCambioEstado.Conflicto();
            }

            var calculo = previo with { Datos = datos, Plan = Recortar(datos, previo.Fecha) };
            try
            {
                // E4: versión = máx + 1 dentro del applock (UQ_ProyectoEtapa_Version es la red de seguridad).
                var version = await repositorio.ObtenerUltimaVersionEtapaAsync(datos.Id, ctTx) + 1;
                await repositorio.AplicarAsync(new CambioEstadoAplicar(
                    datos.Id,
                    version,
                    previo.EstadoDestino,
                    MaquinaEstadosProyecto.CodigoTipoMovimiento(previo.Movimiento),
                    datos.FechaInicio,
                    calculo.Plan,
                    CrearSnapshot(calculo)), ctTx);

                return ResultadoCambioEstado.Hecho(new CambioEstadoRealizadoDto(datos.Id, previo.EstadoDestino, version));
            }
            catch (ConflictoConcurrenciaException)
            {
                return ResultadoCambioEstado.Conflicto(); // se revierte la transacción (confirmar = false)
            }
        }, r => r.Estado == EstadoCambio.Realizado, ct);
    }

    // ------------------------------------------------------------------ cálculo común

    private sealed record Calculo(
        DatosCambioEstado Datos, int? PropietarioUsuarioId, MovimientoEstado Movimiento, string EstadoDestino, DateOnly Fecha, PlanRecorte Plan);

    private sealed record Preparado(ResultadoCambioEstado? Error, Calculo? Calculo);

    private async Task<Preparado> PrepararAsync(int proyectoId, CambioEstadoSolicitud solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return new Preparado(ResultadoCambioEstado.NoEncontrado(), null);
        }

        // Sin fecha no hay días que leer (se valida después): DateOnly.MaxValue no trae ninguno.
        var datos = await repositorio.ObtenerAsync(proyectoId, propietario, solicitud.Fecha ?? DateOnly.MaxValue, ct);
        if (datos is null)
        {
            return new Preparado(ResultadoCambioEstado.NoEncontrado(), null); // inexistente, eliminado o no visible
        }

        var validacion = validador.Validar(solicitud, datos.EstadoCodigo, datos.FechaInicio, datos.FechaFin);
        if (!validacion.EsValido)
        {
            return new Preparado(ResultadoCambioEstado.Invalido(validacion.Errores), null);
        }

        var fecha = validacion.Fecha!.Value;
        return new Preparado(null, new Calculo(
            datos, propietario, validacion.Movimiento!.Value, validacion.EstadoDestino!, fecha, Recortar(datos, fecha)));
    }

    private static PlanRecorte Recortar(DatosCambioEstado d, DateOnly fecha) =>
        RecorteProyecto.Calcular(d.FechaInicio, d.FechaFin, fecha, d.Personal.Select(p => p.Corte).ToList(), d.DiasPosteriores, d.Actividades);

    /// <summary>R1: Admin ve todos (null); cualquier otro rol solo sus proyectos.</summary>
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

    // ------------------------------------------------------------------ vista previa

    private CambioEstadoPrevisualizacionDto CrearPrevisualizacion(Calculo c)
    {
        var impacto = VistaRecorte.Crear(c.Datos.Personal, c.Datos.Actividades, c.Plan);

        var advertencias = new List<string>();
        if (c.Fecha < FechaNegocio.Hoy(reloj))
        {
            advertencias.Add(AdvertenciaDiasTranscurridos); // E6 / H2
        }
        advertencias.AddRange(impacto.AdvertenciasBacks);

        return new CambioEstadoPrevisualizacionDto(
            MaquinaEstadosProyecto.CodigoTipoMovimiento(c.Movimiento),
            c.Datos.EstadoCodigo,
            c.EstadoDestino,
            c.Fecha,
            c.Datos.FechaFin,
            c.Fecha,
            impacto.DiasEliminados,
            impacto.PersonalEliminado,
            impacto.PersonalRecortado,
            impacto.ActividadesAfectadas,
            advertencias);
    }

    /// <summary>E4: snapshot del personal RESULTANTE (sin eliminados, fechas recortadas). Mismo formato que la creación.</summary>
    private static string CrearSnapshot(Calculo c) => VistaRecorte.Snapshot(c.Datos.Personal, c.Plan);
}
