using App.Application.Proyectos.Crear;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Personal;

/// <summary>Resultado del motor y de los cruces para un plan de personal ya validado.</summary>
internal sealed record CalculoRegeneracion(
    ResultadoRegeneracion Regeneracion,
    IReadOnlyDictionary<string, int> ClavesGuardadas,
    IReadOnlyList<CruceDto> Cruces,
    IReadOnlyList<TramoDto> Tramos);

/// <summary>
/// Cálculo común de "Actualizar personal" (TAREA-17) y "Reactivar" (TAREA-17b): motor (Regenerar), cruces internos,
/// históricos y externos, tramos para la vista previa, días a insertar, personas a escribir y snapshot.
/// Extraído de EdicionPersonalServicio sin cambiar su comportamiento.
/// </summary>
internal static class CalculoPersonal
{
    /// <summary>
    /// Ejecuta el motor con el corte indicado. Vigentes y nuevas por su clave; las históricas entran con
    /// ForzarHistorica = true (clave "h{Id}", igual que sus días de la base), así que no se regeneran.
    /// </summary>
    public static async Task<CalculoRegeneracion> RegenerarAsync(DatosEdicion proyecto, PlanEdicion plan, DateOnly corte,
        DateOnly finProyecto, IConsultaCrucesExternos crucesExternos, CancellationToken ct)
    {
        var clavesGuardadas = plan.Principales.Concat(plan.Backs).Where(p => p.Id is not null)
            .ToDictionary(p => p.Clave, p => p.Id!.Value, StringComparer.Ordinal);
        var clavePorId = clavesGuardadas.ToDictionary(kv => kv.Value, kv => kv.Key);

        var principales = plan.Principales.Select(p => new PrincipalEdicion(p.Clave, p.Numero, p.Empleado.Id, p.Inicio, p.Fin,
                p.Jornada!.DiasTrabajo, p.Jornada.DiasDescanso, p.EsNueva))
            .Concat(plan.Historicas
                .Where(h => h.Rol == RolCronograma.Principal && h.DiasTrabajo is > 0) // CK_ProyectoPersonal_Jornada: siempre > 0
                .Select(h => new PrincipalEdicion(ClaveHistorica(h.Id), h.Numero, h.EmpleadoId, h.FechaInicio, h.FechaFin,
                    h.DiasTrabajo!.Value, h.DiasDescanso, EsNuevo: false, ForzarHistorica: true)))
            .ToList();

        var backs = plan.Backs.Select(b => new BackEdicion(b.Clave, b.Numero, b.Empleado.Id, b.Inicio, b.Fin, b.TipoRegistro,
                b.DiasDescanso, b.EsNueva))
            .Concat(plan.Historicas
                .Where(h => h.Rol == RolCronograma.Back)
                .Select(h => new BackEdicion(ClaveHistorica(h.Id), h.Numero, h.EmpleadoId, h.FechaInicio, h.FechaFin,
                    h.TipoRegistro == ProyectoPersonal.TipoRegistroDescanso ? TipoRegistroBack.Descanso : TipoRegistroBack.Jornada,
                    h.DiasDescanso, EsNuevo: false, ForzarHistorica: true)))
            .ToList();

        var regeneracion = MotorCronograma.Regenerar(new SolicitudRegeneracion(
            proyecto.FechaInicio, finProyecto, corte, principales, backs,
            proyecto.DiasBase.Select(d => new DiaExistente(clavePorId.GetValueOrDefault(d.PersonalId, ClaveHistorica(d.PersonalId)), d.Dia)).ToList()));

        // Empleados de todo el proyecto (guardados + nuevos) para nombres y cruces.
        var empleados = Empleados(proyecto, plan);

        // E6: internos + históricos propios (motor) + externos (otros proyectos vigentes, excluyendo este).
        var cruces = CalculadorCruces.Internos(regeneracion.CrucesInternos, empleados)
            .Concat(CalculadorCruces.Historicos(regeneracion.CrucesHistoricos, empleados))
            .Concat(await BuscarExternosAsync(proyecto.Id, regeneracion.DiasTrabajoRegenerados, empleados, crucesExternos, ct))
            .ToList();

        var tramos = regeneracion.Tramos.Select(t => new TramoDto(
            CalculadorCruces.NombreRol(t.Rol), t.Tipo == TipoAsignacionCronograma.Auto ? "AUTO" : "MANUAL", t.Bloque,
            new PersonaDto(CalculadorCruces.NombreRol(t.Persona.Rol), t.Persona.Numero),
            t.EmpleadoId, empleados[t.EmpleadoId].CodigoEkon, empleados[t.EmpleadoId].NombreCompleto, t.Inicio, t.Fin, t.Dias)).ToList();

        return new CalculoRegeneracion(regeneracion, clavesGuardadas, cruces, tramos);
    }

    public static string ClaveHistorica(int id) => $"h{id}";

    /// <summary>Personas de la vista previa: históricas (sin cambio), vigentes y nuevas del plan, y eliminadas.</summary>
    public static List<PersonaCambioDto> Personas(PlanEdicion plan) =>
    [
        .. plan.Historicas.Select(h => new PersonaCambioDto(ClaveHistorica(h.Id), h.Id, CalculadorCruces.NombreRol(h.Rol), h.Numero,
            Empleado(h), "HISTORICO", EdicionPersonalValidador.SinCambio)),
        .. plan.Principales.Concat(plan.Backs).Select(p => new PersonaCambioDto(p.Clave, p.Id, CalculadorCruces.NombreRol(p.Rol), p.Numero,
            new EmpleadoEdicionDto(p.Empleado.Id, p.Empleado.CodigoEkon, p.Empleado.NombreCompleto),
            p.EsNueva ? "NUEVO" : "VIGENTE", p.Accion)),
        .. plan.Eliminadas.Select(x => new PersonaCambioDto($"e{x.Id}", x.Id, CalculadorCruces.NombreRol(x.Rol), x.Numero, Empleado(x),
            "VIGENTE", EdicionPersonalValidador.Eliminado)),
    ];

    /// <summary>Vigentes con sus valores finales y nuevas con su número (para AplicarAsync).</summary>
    public static (List<PersonaVigenteActualizada> Vigentes, List<PersonaNueva> Nuevas) PersonalParaEscribir(PlanEdicion plan)
    {
        static RelacionPrincipal Relacion(PersonaPlan p) => new(p.Relacion.PrincipalId, p.Relacion.PrincipalClaveNueva);

        var vigentes = plan.Principales.Concat(plan.Backs).Where(p => !p.EsNueva).Select(p => new PersonaVigenteActualizada(
            p.Id!.Value, p.Jornada?.Id, p.Jornada?.DiasTrabajo, p.DiasDescanso, p.Inicio, p.Fin, TipoRegistro(p), p.Cargo, p.Observacion,
            Relacion(p))).ToList();

        var nuevas = plan.Principales.Concat(plan.Backs).Where(p => p.EsNueva).Select(p => new PersonaNueva(
            p.Clave, p.Rol, p.Numero, p.Empleado.Id, p.Jornada?.Id, p.Jornada?.DiasTrabajo, p.DiasDescanso, p.Inicio, p.Fin,
            TipoRegistro(p), p.Cargo, p.Observacion, Relacion(p))).ToList();

        return (vigentes, nuevas);
    }

    /// <summary>DiasAInsertar del motor: clave guardada → ProyectoPersonalId; clave nueva → se resuelve al insertar.</summary>
    public static List<DiaParaInsertar> Dias(CalculoRegeneracion c) =>
        c.Regeneracion.DiasAInsertar.Select(d => c.ClavesGuardadas.TryGetValue(d.Clave, out var id)
            ? new DiaParaInsertar(id, null, d.Dia)
            : new DiaParaInsertar(null, d.Clave, d.Dia)).ToList();

    /// <summary>D8: snapshot del personal RESULTANTE (históricas + vigentes + nuevas), formato común.</summary>
    public static string Snapshot(PlanEdicion plan) =>
        SnapshotPersonal.Serializar(
            plan.Historicas.Select(h => new ElementoSnapshot(h.Numero, CalculadorCruces.NombreRol(h.Rol), h.CodigoEkon, h.NombreCompleto,
                    h.FechaInicio, h.FechaFin, h.JornadaCodigo, h.DiasTrabajo, h.DiasDescanso, h.TipoRegistro))
                .Concat(plan.Principales.Concat(plan.Backs).Select(p => new ElementoSnapshot(p.Numero, CalculadorCruces.NombreRol(p.Rol),
                    p.Empleado.CodigoEkon, p.Empleado.NombreCompleto, p.Inicio, p.Fin, p.Jornada?.Codigo, p.Jornada?.DiasTrabajo,
                    p.DiasDescanso, TipoRegistro(p))))
                .OrderBy(x => x.Rol == "PRINCIPAL" ? 0 : 1).ThenBy(x => x.Numero));

    public static EmpleadoEdicionDto Empleado(PersonaGuardada p) => new(p.EmpleadoId, p.CodigoEkon, p.NombreCompleto);

    private static string TipoRegistro(PersonaPlan p) =>
        p.TipoRegistro == TipoRegistroBack.Descanso ? ProyectoPersonal.TipoRegistroDescanso : ProyectoPersonal.TipoRegistroJornada;

    private static Dictionary<int, EmpleadoRef> Empleados(DatosEdicion proyecto, PlanEdicion plan) =>
        proyecto.Personal
            .Select(p => new EmpleadoRef(p.EmpleadoId, p.CodigoEkon, p.NombreCompleto, p.Cargo))
            .Concat(plan.Principales.Concat(plan.Backs).Select(p => p.Empleado))
            .DistinctBy(x => x.Id)
            .ToDictionary(x => x.Id);

    private static async Task<IReadOnlyList<CruceDto>> BuscarExternosAsync(int proyectoId, IReadOnlyList<DiaAsignado> trabajo,
        IReadOnlyDictionary<int, EmpleadoRef> empleados, IConsultaCrucesExternos crucesExternos, CancellationToken ct)
    {
        if (trabajo.Count == 0)
        {
            return [];
        }

        var existentes = await crucesExternos.BuscarAsync(
            trabajo.Select(d => d.EmpleadoId).Distinct().ToList(), trabajo.Min(d => d.Fecha), trabajo.Max(d => d.Fecha),
            excluirProyectoId: proyectoId, ct);
        return CalculadorCruces.Externos(trabajo, existentes, empleados);
    }
}
