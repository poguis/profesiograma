using App.Application.Proyectos.Crear;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Personal;

/// <summary>Relación del back en el plan: Id de un principal guardado o clave de uno del cuerpo que es nuevo.</summary>
public sealed record RelacionPlan(int? PrincipalId, string? PrincipalClaveNueva)
{
    public static readonly RelacionPlan Ninguna = new(null, null);
}

/// <summary>Persona vigente o nueva ya validada. Numero = el guardado (vigente) o máx + 1 (nueva).</summary>
public sealed record PersonaPlan(
    string Clave, int? Id, RolCronograma Rol, short Numero, EmpleadoRef Empleado, JornadaRef? Jornada,
    DateOnly Inicio, DateOnly Fin, TipoRegistroBack TipoRegistro, byte DiasDescanso,
    string? Cargo, string? Observacion, RelacionPlan Relacion, string Accion)
{
    public bool EsNueva => Id is null;
}

public sealed record PlanEdicion(
    IReadOnlyList<PersonaPlan> Principales,
    IReadOnlyList<PersonaPlan> Backs,
    IReadOnlyList<PersonaGuardada> Historicas,
    IReadOnlyList<PersonaGuardada> Eliminadas);

/// <summary>Plan válido, errores por campo (400) o Cambiado = un Id ya no existe en el proyecto (409).</summary>
public sealed record ResultadoValidacionEdicion(PlanEdicion? Plan, IReadOnlyDictionary<string, string[]> Errores, bool Cambiado);

/// <summary>
/// Diferencias entre la edición (TAREA-17) y la reactivación (TAREA-17b) en el núcleo común de validación.
/// Reactivacion: todas las personas del cuerpo son nuevas (sin id), todo el personal guardado es histórico (R5) y
/// las nuevas empiezan en el corte R o después.
/// </summary>
internal sealed record ReglasValidacionPersonal(DateOnly Corte, (DateOnly Inicio, DateOnly Fin) Rango, bool Reactivacion);

/// <summary>
/// Validación de "Actualizar personal" (TAREA-17: E1–E4, D2–D5). Lógica pura sobre los datos ya leídos.
/// Las reglas comunes con la creación están en ReglasPersonal (mismos mensajes).
/// </summary>
public sealed class EdicionPersonalValidador
{
    public const string SinCambio = "SIN_CAMBIO", Modificado = "MODIFICADO", Eliminado = "ELIMINADO", Nuevo = "NUEVO";
    public const string MensajeIdReactivacion = "En la reactivación todas las personas son nuevas; no envíe id.";

    public ResultadoValidacionEdicion Validar(
        ActualizarPersonalSolicitud s,
        DatosEdicion proyecto,
        DateOnly corte,
        IReadOnlyDictionary<string, JornadaRef> jornadas,
        LimitesProyecto limites,
        IReadOnlyDictionary<int, EmpleadoRef> empleadosActivos)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(proyecto);

        // E1: proyecto ACTIVO y no terminado.
        if (MotivoNoEditable(proyecto, corte) is { } motivo)
        {
            var e = new Dictionary<string, List<string>>();
            Agregar(e, "proyecto", motivo);
            return Invalido(e);
        }

        return ValidarPersonal(s, proyecto, new ReglasValidacionPersonal(corte, (proyecto.FechaInicio, proyecto.FechaFin), Reactivacion: false),
            jornadas, limites, empleadosActivos);
    }

    /// <summary>
    /// Núcleo común (TAREA-17 y TAREA-17b): identidad, claves, empleado, jornada, fechas (RN08), relación del back,
    /// numeración máx + 1 por rol, personas omitidas y máximos. Lógica pura.
    /// </summary>
    internal ResultadoValidacionEdicion ValidarPersonal(
        ActualizarPersonalSolicitud s,
        DatosEdicion proyecto,
        ReglasValidacionPersonal reglas,
        IReadOnlyDictionary<string, JornadaRef> jornadas,
        LimitesProyecto limites,
        IReadOnlyDictionary<int, EmpleadoRef> empleadosActivos)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(proyecto);
        ArgumentNullException.ThrowIfNull(reglas);
        var e = new Dictionary<string, List<string>>();

        var principales = s.Principales ?? [];
        var backs = s.Backs ?? [];
        var guardadas = proyecto.Personal.ToDictionary(p => p.Id);
        var corte = reglas.Corte;
        var rango = reglas.Rango;
        var corteMenosUno = corte.AddDays(-1);
        DateOnly? inicioMinimoNuevas = reglas.Reactivacion ? corte : null;

        // R5 (reactivación): todo el personal guardado es histórico. Edición: R2 del motor + H12.
        bool EsHistorica(PersonaGuardada p) => reglas.Reactivacion || p.EsHistorica(corte);

        // Ids (edición): deben existir (si no, el proyecto cambió → 409), en su lista y sin repetirse.
        if (!reglas.Reactivacion)
        {
            foreach (var id in principales.Select(p => p.Id).Concat(backs.Select(b => b.Id)).OfType<int>())
            {
                if (!guardadas.ContainsKey(id))
                {
                    return new ResultadoValidacionEdicion(null, Congelar(e), Cambiado: true);
                }
            }
        }

        // Claves: obligatorias y únicas en todo el cuerpo.
        var claves = new HashSet<string>(StringComparer.Ordinal);
        ValidarClaves(principales.Select(p => p.Clave), "principales", claves, e);
        ValidarClaves(backs.Select(b => b.Clave), "backs", claves, e);

        // Numeración de las nuevas: máx + 1 por rol sobre TODO el personal guardado (sin reutilizar huecos).
        var siguiente = new Dictionary<RolCronograma, short>
        {
            [RolCronograma.Principal] = (short)(proyecto.Personal.Where(p => p.Rol == RolCronograma.Principal).Select(p => (int)p.Numero).DefaultIfEmpty(0).Max() + 1),
            [RolCronograma.Back] = (short)(proyecto.Personal.Where(p => p.Rol == RolCronograma.Back).Select(p => (int)p.Numero).DefaultIfEmpty(0).Max() + 1),
        };

        var idsEnviados = new HashSet<int>();
        var planPrincipales = new List<PersonaPlan>();
        for (var i = 0; i < principales.Count; i++)
        {
            var p = principales[i];
            var clave = $"principales[{i}]";
            if (!ValidarIdentidad(p.Id, RolCronograma.Principal, clave, guardadas, idsEnviados, reglas.Reactivacion, EsHistorica, e,
                    out var guardada))
            {
                continue; // Id inválido: no se valida el resto de la fila (un solo error, en "id")
            }

            var empleado = ResolverEmpleado(p.EmpleadoId, guardada, clave, empleadosActivos, e);
            var jornada = ValidarJornada(p.Jornada, clave, jornadas, e);
            var fechas = ValidarFechas(p.FechaInicio, p.FechaFin, guardada, clave, rango, corte, corteMenosUno, inicioMinimoNuevas, e);
            var cargo = guardada is not null && p.Cargo is null
                ? guardada.Cargo
                : ValidarLargo(p.Cargo, $"{clave}.cargo", CrearProyectoValidador.LargoMaximoCargo, "El cargo", e) ?? empleado?.Puesto;

            if (empleado is not null && jornada is not null && fechas is (DateOnly inicio, DateOnly fin) && !string.IsNullOrWhiteSpace(p.Clave))
            {
                var accion = guardada is null ? Nuevo
                    : guardada.JornadaCodigo != jornada.Codigo || guardada.FechaInicio != inicio || guardada.FechaFin != fin
                      || guardada.Cargo != cargo ? Modificado : SinCambio;
                planPrincipales.Add(new PersonaPlan(p.Clave.Trim(), guardada?.Id, RolCronograma.Principal,
                    guardada?.Numero ?? siguiente[RolCronograma.Principal]++, empleado, jornada, inicio, fin,
                    TipoRegistroBack.Jornada, jornada.DiasDescanso, cargo, null, RelacionPlan.Ninguna, accion));
            }
        }

        var principalesPorClave = planPrincipales.ToDictionary(p => p.Clave, StringComparer.Ordinal);
        var clavesPrincipales = principales.Select(p => p.Clave?.Trim()).OfType<string>().ToHashSet(StringComparer.Ordinal);

        var planBacks = new List<PersonaPlan>();
        for (var i = 0; i < backs.Count; i++)
        {
            var b = backs[i];
            var clave = $"backs[{i}]";
            if (!ValidarIdentidad(b.Id, RolCronograma.Back, clave, guardadas, idsEnviados, reglas.Reactivacion, EsHistorica, e,
                    out var guardada))
            {
                continue; // Id inválido: no se valida el resto de la fila (un solo error, en "id")
            }

            var empleado = ResolverEmpleado(b.EmpleadoId, guardada, clave, empleadosActivos, e);
            var tipo = ValidarTipoRegistro(b.TipoRegistro, clave, e);
            var dias = ValidarDiasDescanso(b.DiasDescanso, clave, limites.MaxDiasDescansoBack, e);
            var fechas = ValidarFechas(b.FechaInicio, b.FechaFin, guardada, clave, rango, corte, corteMenosUno, inicioMinimoNuevas, e);
            var observacion = ValidarLargo(b.Observacion, $"{clave}.observacion", CrearProyectoValidador.LargoMaximoObservacion, "La observación", e);
            var relacion = ResolverRelacion(b, clave, clavesPrincipales, principalesPorClave, guardadas, EsHistorica, e);

            if (empleado is not null && tipo is TipoRegistroBack t && fechas is (DateOnly inicio, DateOnly fin) && relacion is not null
                && !string.IsNullOrWhiteSpace(b.Clave))
            {
                var tipoTexto = t == TipoRegistroBack.Descanso ? ProyectoPersonal.TipoRegistroDescanso : ProyectoPersonal.TipoRegistroJornada;
                var accion = guardada is null ? Nuevo
                    : guardada.TipoRegistro != tipoTexto || guardada.DiasDescanso != dias || guardada.FechaInicio != inicio
                      || guardada.FechaFin != fin || guardada.Observacion != observacion
                      || guardada.PrincipalRelacionadoId != relacion.PrincipalId || relacion.PrincipalClaveNueva is not null
                      ? Modificado : SinCambio;
                planBacks.Add(new PersonaPlan(b.Clave.Trim(), guardada?.Id, RolCronograma.Back,
                    guardada?.Numero ?? siguiente[RolCronograma.Back]++, empleado, null, inicio, fin, t, (byte)Math.Clamp(dias, 0, byte.MaxValue),
                    guardada?.Cargo ?? empleado.Puesto, observacion, relacion, accion));
            }
        }

        // E3: las vigentes que ya empezaron son obligatorias; las que aún no empiezan se eliminan si se omiten.
        var historicas = proyecto.Personal.Where(EsHistorica).ToList();
        var eliminadas = new List<PersonaGuardada>();
        foreach (var vigente in proyecto.Personal.Where(p => !EsHistorica(p) && !idsEnviados.Contains(p.Id)))
        {
            if (vigente.FechaInicio < corte)
            {
                Agregar(e, vigente.Rol == RolCronograma.Principal ? "principales" : "backs",
                    $"Falta {Etiqueta(vigente)} ({vigente.NombreCompleto}): una persona que ya empezó no se puede quitar; " +
                    $"acorte su fecha fin al {Formato(corteMenosUno)}.");
            }
            else
            {
                eliminadas.Add(vigente);
            }
        }

        // E4 / RN18: máximos sobre vigentes + nuevas.
        if (principales.Count > limites.MaxPrincipales)
        {
            Agregar(e, "principales", $"Se permiten como máximo {limites.MaxPrincipales} principales.");
        }

        if (backs.Count > limites.MaxBacks)
        {
            Agregar(e, "backs", $"Se permiten como máximo {limites.MaxBacks} backs.");
        }

        return e.Count > 0
            ? Invalido(e)
            : new ResultadoValidacionEdicion(new PlanEdicion(planPrincipales, planBacks, historicas, eliminadas), Congelar(e), Cambiado: false);
    }

    /// <summary>E1: motivo por el que no se puede editar el personal (null = se puede).</summary>
    public static string? MotivoNoEditable(DatosEdicion proyecto, DateOnly corte)
    {
        if (!string.Equals(proyecto.EstadoCodigo, CodigosEstadoProyecto.Activo, StringComparison.OrdinalIgnoreCase))
        {
            return $"Solo se puede modificar el personal de un proyecto ACTIVO (estado actual: {proyecto.EstadoCodigo}).";
        }

        return proyecto.FechaFin < corte
            ? $"El proyecto finalizó el {Formato(proyecto.FechaFin)}; amplía la fecha fin antes de modificar el personal."
            : null;
    }

    public static string Etiqueta(PersonaGuardada p) => p.Rol == RolCronograma.Principal ? $"P{p.Numero}" : $"Back {p.Numero}";

    // ------------------------------------------------------------------ reglas por persona

    private static void ValidarClaves(IEnumerable<string?> claves, string lista, HashSet<string> vistas, Dictionary<string, List<string>> e)
    {
        var i = 0;
        foreach (var clave in claves)
        {
            var texto = clave?.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                Agregar(e, $"{lista}[{i}].clave", "La clave es obligatoria.");
            }
            else if (!vistas.Add(texto))
            {
                Agregar(e, $"{lista}[{i}].clave", $"La clave '{texto}' está repetida.");
            }

            i++;
        }
    }

    /// <summary>
    /// Persona guardada que corresponde al Id (null = nueva). false = Id inválido (otra lista, repetido o histórico;
    /// en la reactivación, cualquier id): el error queda en "{clave}.id" y el resto de la fila no se valida.
    /// Un Id inexistente (edición) ya se resolvió como 409.
    /// </summary>
    private static bool ValidarIdentidad(int? id, RolCronograma rol, string clave, IReadOnlyDictionary<int, PersonaGuardada> guardadas,
        HashSet<int> enviados, bool reactivacion, Func<PersonaGuardada, bool> esHistorica, Dictionary<string, List<string>> e,
        out PersonaGuardada? guardada)
    {
        guardada = null;
        if (id is not int personaId)
        {
            return true;
        }

        if (reactivacion)
        {
            Agregar(e, $"{clave}.id", MensajeIdReactivacion);
            return false;
        }

        var encontrada = guardadas[personaId];
        if (encontrada.Rol != rol)
        {
            Agregar(e, $"{clave}.id", $"La persona {personaId} no es un {(rol == RolCronograma.Principal ? "principal" : "back")}.");
            return false;
        }

        if (!enviados.Add(personaId))
        {
            Agregar(e, $"{clave}.id", $"La persona {personaId} está repetida.");
            return false;
        }

        if (esHistorica(encontrada))
        {
            Agregar(e, $"{clave}.id", $"La persona {Etiqueta(encontrada)} es histórica (terminó el {Formato(encontrada.FechaFin)}) y no se puede modificar.");
            return false;
        }

        guardada = encontrada;
        return true;
    }

    /// <summary>Vigente: el empleado no cambia (null = el mismo). Nueva: obligatorio y activo (D4).</summary>
    private static EmpleadoRef? ResolverEmpleado(int? empleadoId, PersonaGuardada? guardada, string clave,
        IReadOnlyDictionary<int, EmpleadoRef> activos, Dictionary<string, List<string>> e)
    {
        if (guardada is null)
        {
            return ValidarEmpleado(empleadoId, clave, activos, e);
        }

        if (empleadoId is int id && id != guardada.EmpleadoId)
        {
            Agregar(e, $"{clave}.empleadoId",
                "No se puede cambiar el empleado de una persona vigente. Para reemplazarla, acorte su fecha fin y agregue una persona nueva.");
            return null;
        }

        return new EmpleadoRef(guardada.EmpleadoId, guardada.CodigoEkon, guardada.NombreCompleto, guardada.Cargo);
    }

    /// <summary>
    /// RN08 + E3 (vigentes): inicio fijo si ya empezó; si aún no empieza, inicio ≥ corte (D2); fin ≥ corte − 1 si cambia (D5).
    /// Reactivación (inicioMinimoNuevas = R): las nuevas no pueden empezar antes de R (R5).
    /// </summary>
    private static (DateOnly Inicio, DateOnly Fin)? ValidarFechas(DateOnly? inicio, DateOnly? fin, PersonaGuardada? guardada, string clave,
        (DateOnly, DateOnly) rango, DateOnly corte, DateOnly corteMenosUno, DateOnly? inicioMinimoNuevas, Dictionary<string, List<string>> e)
    {
        var errores = e.Count;
        if (guardada is null && inicioMinimoNuevas is DateOnly minimo && inicio is DateOnly inicioNueva && inicioNueva < minimo)
        {
            Agregar(e, $"{clave}.fechaInicio", $"La fecha de inicio no puede ser anterior a la fecha de reactivación ({Formato(minimo)}).");
        }

        if (guardada is not null && inicio is DateOnly i)
        {
            if (guardada.FechaInicio < corte && i != guardada.FechaInicio)
            {
                Agregar(e, $"{clave}.fechaInicio",
                    $"No se puede cambiar la fecha de inicio de una persona que ya empezó ({Formato(guardada.FechaInicio)}).");
            }
            else if (guardada.FechaInicio >= corte && i < corte)
            {
                Agregar(e, $"{clave}.fechaInicio",
                    $"La fecha de inicio de una persona que aún no empieza no puede ser anterior al corte ({Formato(corte)}).");
            }
        }

        if (guardada is not null && fin is DateOnly f && f != guardada.FechaFin && f < corteMenosUno)
        {
            Agregar(e, $"{clave}.fechaFin", $"La fecha fin no puede ser anterior al {Formato(corteMenosUno)} (día anterior al corte).");
        }

        var fechas = ValidarFechasPersona(inicio, fin, clave, rango, e);
        return e.Count == errores ? fechas : null;
    }

    /// <summary>Relación del back: clave de un principal del cuerpo o Id de un principal HISTÓRICO (D3). null = inválida.</summary>
    private static RelacionPlan? ResolverRelacion(BackEdicionSolicitud b, string clave, HashSet<string> clavesPrincipales,
        IReadOnlyDictionary<string, PersonaPlan> principales, IReadOnlyDictionary<int, PersonaGuardada> guardadas,
        Func<PersonaGuardada, bool> esHistorica, Dictionary<string, List<string>> e)
    {
        var principalClave = b.PrincipalClave?.Trim();
        if (!string.IsNullOrEmpty(principalClave) && b.PrincipalId is not null)
        {
            Agregar(e, $"{clave}.principalClave", "Indique principalClave o principalId, no ambos.");
            return null;
        }

        if (!string.IsNullOrEmpty(principalClave))
        {
            if (!clavesPrincipales.Contains(principalClave))
            {
                Agregar(e, $"{clave}.principalClave", $"El principal relacionado '{principalClave}' no está entre los principales enviados.");
                return null;
            }

            // Si el principal tiene errores no está en el plan: su error ya se informa en su propia clave.
            return principales.TryGetValue(principalClave, out var principal)
                ? principal.EsNueva ? new RelacionPlan(null, principal.Clave) : new RelacionPlan(principal.Id, null)
                : RelacionPlan.Ninguna;
        }

        if (b.PrincipalId is int principalId)
        {
            if (!guardadas.TryGetValue(principalId, out var guardada) || guardada.Rol != RolCronograma.Principal || !esHistorica(guardada))
            {
                Agregar(e, $"{clave}.principalId", $"El principal {principalId} no es un principal histórico de este proyecto.");
                return null;
            }

            return new RelacionPlan(principalId, null);
        }

        return RelacionPlan.Ninguna;
    }

    private static ResultadoValidacionEdicion Invalido(Dictionary<string, List<string>> e) => new(null, Congelar(e), Cambiado: false);
}
