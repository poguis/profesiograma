using App.Application.Comun;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// Caso de uso "Crear proyecto" (FASE_5 §3, §6–§8): validar → datos ERP por Id → MotorCronograma →
/// cruces internos + externos → previsualizar (no guarda) o registrar (una transacción con applock).
/// El servidor recalcula todo al registrar; la vista previa es solo informativa.
/// </summary>
public sealed class CrearProyectoServicio(
    CrearProyectoValidador validador,
    IConsultaCrucesExternos crucesExternos,
    IProyectoRepositorio repositorio,
    ITransaccionAsignaciones transaccion,
    TimeProvider reloj)
{
    /// <summary>Intentos de generar un código único (RN01).</summary>
    public const int IntentosCodigo = 3;

    public async Task<ResultadoCrearProyecto> PrevisualizarAsync(CrearProyectoSolicitud solicitud, CancellationToken ct)
    {
        var validacion = await validador.ValidarAsync(solicitud, ct);
        if (validacion.Valido is not { } proyecto)
        {
            return ResultadoCrearProyecto.Invalido(validacion.Errores);
        }

        var calculo = await CalcularAsync(proyecto, ct);
        return ResultadoCrearProyecto.Previsualizado(calculo.Previsualizacion);
    }

    public async Task<ResultadoCrearProyecto> RegistrarAsync(CrearProyectoSolicitud solicitud, CancellationToken ct)
    {
        var validacion = await validador.ValidarAsync(solicitud, ct);
        if (validacion.Valido is not { } proyecto)
        {
            return ResultadoCrearProyecto.Invalido(validacion.Errores);
        }

        // Verificación previa (fuera de la transacción): si ya hay cruces, no se abre la transacción.
        var calculo = await CalcularAsync(proyecto, ct);
        if (calculo.Previsualizacion.Cruces.Count > 0)
        {
            return ResultadoCrearProyecto.Conflicto(calculo.Previsualizacion);
        }

        var nuevo = new NuevoProyecto(
            proyecto,
            NombreVisualProyecto.Calcular(proyecto.ProyectoErp?.Nombre, proyecto.Dimension?.Descripcion),
            calculo.Cronograma.DiasFinales,
            CrearSnapshot(proyecto));

        return await transaccion.EjecutarAsync(async ctTx =>
        {
            // FASE_5 §8.5: repetir los cruces externos dentro de la transacción (otro usuario pudo registrar a la vez).
            var externos = await BuscarCrucesExternosAsync(calculo.Cronograma, calculo.Empleados, ctTx);
            if (externos.Count > 0)
            {
                return ResultadoCrearProyecto.Conflicto(calculo.Previsualizacion with
                {
                    Cruces = externos,
                    Resumen = CalculadorCruces.Resumen(externos),
                });
            }

            // RN01: código con la fecha de hoy en Ecuador; si choca, nuevo Uid (máx. 3 intentos).
            var hoy = FechaNegocio.Hoy(reloj);
            for (var intento = 1; intento <= IntentosCodigo; intento++)
            {
                var uid = Guid.NewGuid();
                var codigo = GeneradorCodigoProyecto.Generar(hoy, uid);
                if (!await repositorio.ExisteCodigoAsync(codigo, ctTx))
                {
                    var id = await repositorio.AgregarAsync(nuevo, codigo, uid, ctTx);
                    return ResultadoCrearProyecto.Registrado(new ProyectoCreadoDto(id, codigo));
                }
            }

            throw new InvalidOperationException($"No se pudo generar un código de proyecto único tras {IntentosCodigo} intentos.");
        }, r => r.Estado == EstadoCrearProyecto.Creado, ct);
    }

    // ------------------------------------------------------------------ cálculo común

    private sealed record Calculo(ResultadoCronograma Cronograma, IReadOnlyDictionary<int, EmpleadoRef> Empleados, PrevisualizacionDto Previsualizacion);

    private async Task<Calculo> CalcularAsync(ProyectoValidado p, CancellationToken ct)
    {
        var cronograma = MotorCronograma.Generar(new SolicitudCronograma(
            p.FechaInicio, p.FechaFin,
            p.Principales.Select(x => new PrincipalEntrada(
                x.Persona.Numero, x.Empleado.Id, x.Inicio, x.Fin, x.Jornada!.DiasTrabajo, x.Jornada.DiasDescanso)).ToList(),
            p.Backs.Select(x => new BackEntrada(
                x.Persona.Numero, x.Empleado.Id, x.Inicio, x.Fin, x.TipoRegistro!.Value, x.DiasDescanso, x.PrincipalRelacionado)).ToList()));

        var empleados = p.Principales.Concat(p.Backs)
            .Select(x => x.Empleado)
            .DistinctBy(x => x.Id)
            .ToDictionary(x => x.Id);

        var cruces = CalculadorCruces.Internos(cronograma, empleados)
            .Concat(await BuscarCrucesExternosAsync(cronograma, empleados, ct))
            .ToList();

        var previsualizacion = new PrevisualizacionDto(
            cronograma.Tramos.Select(t => new TramoDto(
                CalculadorCruces.NombreRol(t.Rol), NombreTipo(t.Tipo), t.Bloque, Persona(t.Persona),
                t.EmpleadoId, empleados[t.EmpleadoId].CodigoEkon, empleados[t.EmpleadoId].NombreCompleto,
                t.Inicio, t.Fin, t.Dias)).ToList(),
            cronograma.DiasFinales
                .GroupBy(d => d.Persona)
                .OrderBy(g => g.Key.Rol).ThenBy(g => g.Key.Numero)
                .Select(g => new DiasPersonaDto(
                    Persona(g.Key), g.First().EmpleadoId, empleados[g.First().EmpleadoId].CodigoEkon, empleados[g.First().EmpleadoId].NombreCompleto,
                    g.OrderBy(d => d.Fecha).Select(d => new DiaDto(d.Fecha, CalculadorCruces.NombreRol(d.Rol), NombreTipo(d.Tipo), d.Bloque)).ToList()))
                .ToList(),
            cruces,
            CalculadorCruces.Resumen(cruces),
            // TAREA-19y: sin principales no hay principal inicial (responsable vacío). No bloquea.
            p.Principales.Count == 0 ? [MinimoPersonal.AdvertenciaSinPrincipal] : []);

        return new Calculo(cronograma, empleados, previsualizacion);
    }

    /// <summary>Consulta los días existentes en el rango de los días ≠ DESCANSO del proyecto nuevo y los cruza.</summary>
    private async Task<IReadOnlyList<CruceDto>> BuscarCrucesExternosAsync(
        ResultadoCronograma cronograma, IReadOnlyDictionary<int, EmpleadoRef> empleados, CancellationToken ct)
    {
        var trabajo = cronograma.DiasFinales.Where(d => d.Rol != RolCronograma.Descanso).ToList();
        if (trabajo.Count == 0)
        {
            return [];
        }

        var existentes = await crucesExternos.BuscarAsync(
            trabajo.Select(d => d.EmpleadoId).Distinct().ToList(),
            trabajo.Min(d => d.Fecha), trabajo.Max(d => d.Fecha), excluirProyectoId: null, ct);

        return CalculadorCruces.Externos(trabajo, existentes, empleados);
    }

    /// <summary>Snapshot JSON de la etapa v1 (formato común en SnapshotPersonal). Sin cédula ni correo.</summary>
    private static string CrearSnapshot(ProyectoValidado p) =>
        SnapshotPersonal.Serializar(p.Principales.Concat(p.Backs).Select(x => new ElementoSnapshot(
            x.Persona.Numero,
            CalculadorCruces.NombreRol(x.Persona.Rol),
            x.Empleado.CodigoEkon,
            x.Empleado.NombreCompleto,
            x.Inicio,
            x.Fin,
            x.Jornada?.Codigo,
            x.Jornada?.DiasTrabajo,
            x.DiasDescanso,
            x.TipoRegistro == TipoRegistroBack.Descanso ? "DESCANSO" : "JORNADA")));

    private static PersonaDto Persona(PersonaProyecto p) => new(CalculadorCruces.NombreRol(p.Rol), p.Numero);

    private static string NombreTipo(TipoAsignacionCronograma tipo) =>
        tipo == TipoAsignacionCronograma.Auto ? "AUTO" : "MANUAL";
}
