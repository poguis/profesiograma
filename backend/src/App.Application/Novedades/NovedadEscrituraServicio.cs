using App.Application.Comun;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Seguridad;
using App.Domain.Catalogos;
using App.Domain.Novedades;

namespace App.Application.Novedades;

/// <summary>
/// "Crear novedad" (TAREA-23-1; editar y anular: 23-2). Validar (lectura fresca de empleados FUERA de la transacción)
/// → dentro de ITransaccionAsignaciones (desviación aprobada: así N7 no tiene carrera y la alta puntual de PERSONA va en
/// el mismo applock que todas las escrituras de Empleado): duplicado (N7) → código (R7, hasta 3 intentos) → guardar.
/// Si algo falla antes del commit no se guarda nada; después del commit no se vuelve a la base (regla 4 de §7.1).
/// </summary>
public sealed class NovedadEscrituraServicio(
    NovedadValidador validador,
    INovedadRepositorio repositorio,
    ITransaccionAsignaciones transaccion,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    /// <summary>Intentos de generar un código y un Uid únicos (R7, como RN01).</summary>
    public const int IntentosCodigo = 3;

    public async Task<ResultadoCrearNovedad> CrearAsync(CrearNovedadSolicitud solicitud, CancellationToken ct)
    {
        var validacion = await validador.ValidarAsync(solicitud, ct);
        if (validacion.Prohibido)
        {
            return ResultadoCrearNovedad.Prohibido();
        }

        if (validacion.Valida is not { } novedad)
        {
            return ResultadoCrearNovedad.Invalido(validacion.Errores);
        }

        var dias = DiasNovedad.Generar(novedad.FechaInicio, novedad.FechaFin);
        // R6: el código del duplicado solo si quien registra lo ve (sin usuario resuelto: 0, ninguno propio).
        var visibleParaUsuarioId = PermisosNovedad.TryObtenerVisibilidad(usuario, out var visible) ? visible : 0;

        return await transaccion.EjecutarAsync(async ctTx =>
        {
            // N7: una persona sin fila (Id temporal negativo) no puede tener novedades guardadas.
            if (novedad.Empleado is not { Id: <= 0 })
            {
                var criterio = new CriterioDuplicado(novedad.AplicacionId, novedad.Tipo.Id, novedad.Empleado?.Id, novedad.ProyectoId,
                    novedad.FechaInicio, novedad.FechaFin);
                if (await repositorio.BuscarDuplicadoAsync(criterio, visibleParaUsuarioId, ctTx) is { } duplicado)
                {
                    return ResultadoCrearNovedad.Duplicado(duplicado);
                }
            }

            // R7: código con la fecha de hoy en Ecuador; si choca el código o el Uid, nuevo Uid (máx. 3 intentos).
            var hoy = FechaNegocio.Hoy(reloj);
            for (var intento = 1; intento <= IntentosCodigo; intento++)
            {
                var uid = Guid.NewGuid();
                var codigo = GeneradorCodigoNovedad.Generar(hoy, uid);
                if (await repositorio.ExisteCodigoOUidAsync(codigo, uid, ctTx))
                {
                    continue;
                }

                try
                {
                    var creado = await repositorio.AgregarAsync(
                        new NuevaNovedad(novedad, codigo, uid, dias, usuario.NombreMostrar, usuario.Email), ctTx);
                    return ResultadoCrearNovedad.Registrado(creado);
                }
                catch (ConflictoConcurrenciaException)
                {
                    return ResultadoCrearNovedad.Cambiado(); // alta puntual concurrente; se revierte
                }
            }

            throw new InvalidOperationException($"No se pudo generar un código de novedad único tras {IntentosCodigo} intentos.");
        }, r => r.Estado == EstadoCrearNovedad.Creado, ct);
    }
}

/// <summary>Opciones del formulario (N17): permiso de GENERAL, tipos elegibles por aplicación (RN16) y tope de días.</summary>
public sealed class OpcionesFormularioNovedad(INovedadConsultas consultas, IUsuarioActual usuario)
{
    public async Task<OpcionesFormularioNovedadDto> ObtenerAsync(CancellationToken ct)
    {
        var tipos = await consultas.ObtenerTiposAsync(ct);
        var puedeGeneral = PermisosNovedad.PuedeRegistrarGeneral(usuario);

        IReadOnlyList<TipoNovedadResumenDto> Elegibles(byte aplicacionId) =>
            tipos.Where(t => t.EsElegible(aplicacionId)).Select(t => t.Resumen()).ToList();

        return new OpcionesFormularioNovedadDto(
            puedeGeneral,
            new Dictionary<string, IReadOnlyList<TipoNovedadResumenDto>>
            {
                [AplicacionNovedad.Persona] = Elegibles(CatalogoIds.TipoAplicacionNovedad.Persona),
                [AplicacionNovedad.Proyecto] = Elegibles(CatalogoIds.TipoAplicacionNovedad.Proyecto),
                [AplicacionNovedad.General] = puedeGeneral ? Elegibles(CatalogoIds.TipoAplicacionNovedad.General) : [],
            },
            DiasNovedad.MaxDias);
    }
}
