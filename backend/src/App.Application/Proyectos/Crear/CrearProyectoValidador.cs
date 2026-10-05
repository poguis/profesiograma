using System.Globalization;
using App.Application.Comun;
using App.Application.Erp;
using App.Application.Seguridad;
using App.Domain.Proyectos.Cronograma;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Crear;

/// <summary>Resultado de la validación: la solicitud resuelta (con datos del ERP por Id) o los errores por campo.</summary>
public sealed record ResultadoValidacionProyecto(ProyectoValidado? Valido, IReadOnlyDictionary<string, string[]> Errores);

/// <summary>
/// Validación de "Crear proyecto" (FASE_5 §3: RN02, RN08, RN09, RN18, P1, P5). Mensajes en español por campo
/// (claves camelCase con índice, p. ej. "principales[0].jornada"). Los datos del ERP se obtienen SIEMPRE por Id.
/// </summary>
public sealed class CrearProyectoValidador(IDatosReferenciaProyecto datos, ICatalogoErp erp, IUsuarioActual usuario)
{
    /// <summary>
    /// P1: mínimo de principales. Hoy 0 (no obligatorio). Para exigir 1 o más, cambiar esta constante:
    /// la regla ya se valida más abajo ("Se requiere al menos {n} principal(es).").
    /// </summary>
    public const int MinimoPrincipales = 0;

    public const int LargoMaximoCargo = 200;
    public const int LargoMaximoObservacion = 500;

    private static readonly string[] FormatosHora = ["HH:mm", "HH:mm:ss"];

    public async Task<ResultadoValidacionProyecto> ValidarAsync(CrearProyectoSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var e = new Dictionary<string, List<string>>();
        var principales = s.Principales ?? [];
        var backs = s.Backs ?? [];

        if (usuario.UsuarioId is not int usuarioId)
        {
            Agregar(e, "usuario", "No se pudo identificar al usuario actual.");
            return Invalido(e);
        }

        // --- Compañía y grupo (RN02 con los flags del catálogo GrupoProyecto)
        CompaniaErp? compania = null;
        if (s.CompaniaId is not int companiaId)
        {
            Agregar(e, "companiaId", "La compañía es obligatoria.");
        }
        else if ((compania = await erp.ObtenerCompaniaAsync(companiaId, ct)) is null)
        {
            Agregar(e, "companiaId", "La compañía no existe en el ERP.");
        }

        GrupoRef? grupo = null;
        var codigoGrupo = LectorParametros.Normalizar(s.Grupo);
        if (codigoGrupo is null)
        {
            Agregar(e, "grupo", "El grupo es obligatorio.");
        }
        else if ((grupo = await datos.ObtenerGrupoAsync(codigoGrupo, ct)) is null)
        {
            Agregar(e, "grupo", $"El grupo '{codigoGrupo}' no existe.");
        }
        else if (!grupo.RequiereProyectoErp && !grupo.RequiereDimension)
        {
            Agregar(e, "grupo", $"El grupo {grupo.Codigo} no define si usa proyecto ERP o dimensión.");
        }

        ProyectoErp? proyectoErp = null;
        ActividadErp? actividad = null;
        DimensionErp? dimension = null;
        var proyectoErpId = LectorParametros.Normalizar(s.ProyectoErpId);
        var actividadId = LectorParametros.Normalizar(s.ActividadId);
        var dimensionId = LectorParametros.Normalizar(s.DimensionUegpId);

        if (grupo is not null)
        {
            if (grupo.RequiereProyectoErp)
            {
                if (proyectoErpId is null)
                {
                    Agregar(e, "proyectoErpId", $"El grupo {grupo.Codigo} requiere un proyecto ERP.");
                }
                else if (compania is not null && (proyectoErp = await erp.ObtenerProyectoAsync(compania.Id, proyectoErpId, ct)) is null)
                {
                    Agregar(e, "proyectoErpId", "El proyecto ERP no existe o no está activo.");
                }

                if (actividadId is null)
                {
                    Agregar(e, "actividadId", $"El grupo {grupo.Codigo} requiere una actividad.");
                }
                else if (compania is not null && proyectoErp is not null
                         && (actividad = await erp.ObtenerActividadAsync(compania.Id, proyectoErp.Id, actividadId, ct)) is null)
                {
                    Agregar(e, "actividadId", "La actividad no existe en el proyecto ERP.");
                }
            }
            else
            {
                if (proyectoErpId is not null)
                {
                    Agregar(e, "proyectoErpId", $"El grupo {grupo.Codigo} no usa proyecto ERP.");
                }

                if (actividadId is not null)
                {
                    Agregar(e, "actividadId", $"El grupo {grupo.Codigo} no usa actividad.");
                }
            }

            if (grupo.RequiereDimension)
            {
                if (dimensionId is null)
                {
                    Agregar(e, "dimensionUegpId", $"El grupo {grupo.Codigo} requiere una dimensión.");
                }
                else if (compania is not null && (dimension = await erp.ObtenerDimensionAsync(compania.Id, dimensionId, ct)) is null)
                {
                    Agregar(e, "dimensionUegpId", "La dimensión no existe en el ERP.");
                }
            }
            else if (dimensionId is not null)
            {
                Agregar(e, "dimensionUegpId", $"El grupo {grupo.Codigo} no usa dimensión.");
            }
        }

        // --- Fechas del proyecto
        if (s.FechaInicio is null)
        {
            Agregar(e, "fechaInicio", "La fecha de inicio es obligatoria.");
        }

        if (s.FechaFin is null)
        {
            Agregar(e, "fechaFin", "La fecha fin es obligatoria.");
        }

        var rangoValido = s.FechaInicio is not null && s.FechaFin is not null;
        if (rangoValido && s.FechaFin < s.FechaInicio)
        {
            Agregar(e, "fechaFin", "La fecha fin debe ser mayor o igual a la fecha de inicio.");
            rangoValido = false;
        }

        // --- Horario (ERP) y almuerzo (RN09)
        HorarioErp? horario = null;
        if (s.HorarioCodigo is not int horarioCodigo)
        {
            Agregar(e, "horarioCodigo", "El horario es obligatorio.");
        }
        else if ((horario = await erp.ObtenerHorarioAsync(horarioCodigo, ct)) is null)
        {
            Agregar(e, "horarioCodigo", "El horario no existe o no está activo.");
        }

        var salida = LeerHora(s.SalidaAlmuerzo, "salidaAlmuerzo", "La hora de salida a almuerzo es obligatoria.", e);
        var regreso = LeerHora(s.RegresoAlmuerzo, "regresoAlmuerzo", "La hora de regreso de almuerzo es obligatoria.", e);
        // RN09: rangos en ReglasAlmuerzo (misma fuente que las opciones del formulario).
        if (salida is TimeOnly sal && !ReglasAlmuerzo.SalidaEnRango(sal))
        {
            Agregar(e, "salidaAlmuerzo",
                $"La salida a almuerzo debe estar entre {ReglasAlmuerzo.Formato(ReglasAlmuerzo.SalidaMinima)} y {ReglasAlmuerzo.Formato(ReglasAlmuerzo.SalidaMaxima)}.");
        }

        if (regreso is TimeOnly reg && !ReglasAlmuerzo.RegresoEnRango(reg))
        {
            Agregar(e, "regresoAlmuerzo",
                $"El regreso de almuerzo debe estar entre {ReglasAlmuerzo.Formato(ReglasAlmuerzo.RegresoMinimo)} y {ReglasAlmuerzo.Formato(ReglasAlmuerzo.RegresoMaximo)}.");
        }

        if (salida is TimeOnly s1 && regreso is TimeOnly r1 && r1 <= s1)
        {
            Agregar(e, "regresoAlmuerzo", "El regreso de almuerzo debe ser posterior a la salida.");
        }

        // --- Departamento (P5)
        var departamentos = await datos.ObtenerDepartamentosDeUsuarioAsync(usuarioId, ct);
        var departamentoId = ResolverDepartamento(s.DepartamentoId, departamentos, e);

        // --- Personal: cantidades (RN18, P1)
        var limites = await datos.ObtenerLimitesAsync(ct);
        if (principales.Count > limites.MaxPrincipales)
        {
            Agregar(e, "principales", $"Se permiten como máximo {limites.MaxPrincipales} principales.");
        }

        if (principales.Count < MinimoPrincipales)
        {
            Agregar(e, "principales", $"Se requiere al menos {MinimoPrincipales} principal(es).");
        }

        if (backs.Count > limites.MaxBacks)
        {
            Agregar(e, "backs", $"Se permiten como máximo {limites.MaxBacks} backs.");
        }

        var idsEmpleados = principales.Select(p => p.EmpleadoId).Concat(backs.Select(b => b.EmpleadoId))
            .OfType<int>().Distinct().ToList();
        var empleados = idsEmpleados.Count == 0
            ? new Dictionary<int, EmpleadoRef>()
            : await datos.ObtenerEmpleadosActivosAsync(idsEmpleados, ct);
        var jornadas = principales.Count == 0
            ? new Dictionary<string, JornadaRef>()
            : await datos.ObtenerJornadasAsync(ct);
        var rango = rangoValido ? (s.FechaInicio!.Value, s.FechaFin!.Value) : ((DateOnly, DateOnly)?)null;

        // --- Principales (numeración 1..n según el orden recibido)
        var principalesValidos = new List<PersonalValidado>();
        for (var i = 0; i < principales.Count; i++)
        {
            var p = principales[i];
            var clave = $"principales[{i}]";
            var empleado = ValidarEmpleado(p.EmpleadoId, clave, empleados, e);

            var jornada = ValidarJornada(p.Jornada, clave, jornadas, e);

            var fechas = ValidarFechasPersona(p.FechaInicio, p.FechaFin, clave, rango, e);
            var cargo = ValidarLargo(p.Cargo, $"{clave}.cargo", LargoMaximoCargo, "El cargo", e);

            if (empleado is not null && jornada is not null && fechas is (DateOnly inicio, DateOnly fin))
            {
                principalesValidos.Add(new PersonalValidado(
                    new PersonaProyecto(RolCronograma.Principal, (short)(i + 1)), empleado, inicio, fin,
                    jornada, null, jornada.DiasDescanso, null, cargo ?? empleado.Puesto, null));
            }
        }

        // --- Backs
        var backsValidos = new List<PersonalValidado>();
        for (var i = 0; i < backs.Count; i++)
        {
            var b = backs[i];
            var clave = $"backs[{i}]";
            var empleado = ValidarEmpleado(b.EmpleadoId, clave, empleados, e);

            var tipo = ValidarTipoRegistro(b.TipoRegistro, clave, e);
            var diasDescanso = ValidarDiasDescanso(b.DiasDescanso, clave, limites.MaxDiasDescansoBack, e);

            if (b.PrincipalRelacionado is int relacionado && (relacionado < 1 || relacionado > principales.Count))
            {
                Agregar(e, $"{clave}.principalRelacionado", $"El principal relacionado {relacionado} no existe entre los principales enviados.");
            }

            var fechas = ValidarFechasPersona(b.FechaInicio, b.FechaFin, clave, rango, e);
            var cargo = ValidarLargo(b.Cargo, $"{clave}.cargo", LargoMaximoCargo, "El cargo", e);
            var observacion = ValidarLargo(b.Observacion, $"{clave}.observacion", LargoMaximoObservacion, "La observación", e);

            if (empleado is not null && tipo is not null && fechas is (DateOnly inicio, DateOnly fin))
            {
                backsValidos.Add(new PersonalValidado(
                    new PersonaProyecto(RolCronograma.Back, (short)(i + 1)), empleado, inicio, fin,
                    null, tipo, (byte)Math.Clamp(diasDescanso, 0, byte.MaxValue), (short?)b.PrincipalRelacionado,
                    cargo ?? empleado.Puesto, observacion));
            }
        }

        if (e.Count > 0)
        {
            return Invalido(e);
        }

        return new ResultadoValidacionProyecto(
            new ProyectoValidado(compania!, grupo!, proyectoErp, actividad, dimension, s.FechaInicio!.Value, s.FechaFin!.Value,
                horario!, salida!.Value, regreso!.Value, departamentoId, usuarioId, principalesValidos, backsValidos),
            new Dictionary<string, string[]>());
    }

    /// <summary>P5: 0 departamentos → null; 1 → ese (otro enviado = error); varios → obligatorio y uno de los suyos.</summary>
    private static int? ResolverDepartamento(int? enviado, IReadOnlyList<DepartamentoRef> departamentos, Dictionary<string, List<string>> e)
    {
        switch (departamentos.Count)
        {
            case 0:
                return null;
            case 1:
                if (enviado is int unico && unico != departamentos[0].Id)
                {
                    Agregar(e, "departamentoId", "El departamento no está entre los asignados al usuario.");
                }

                return departamentos[0].Id;
            default:
                if (enviado is not int elegido)
                {
                    Agregar(e, "departamentoId", "Debe elegir el departamento del proyecto.");
                    return null;
                }

                if (departamentos.All(d => d.Id != elegido))
                {
                    Agregar(e, "departamentoId", "El departamento no está entre los asignados al usuario.");
                }

                return elegido;
        }
    }

    private static TimeOnly? LeerHora(string? valor, string clave, string mensajeObligatorio, Dictionary<string, List<string>> e)
    {
        var texto = LectorParametros.Normalizar(valor);
        if (texto is null)
        {
            Agregar(e, clave, mensajeObligatorio);
            return null;
        }

        if (TimeOnly.TryParseExact(texto, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
        {
            return hora;
        }

        Agregar(e, clave, "Use el formato HH:mm.");
        return null;
    }

    private static ResultadoValidacionProyecto Invalido(Dictionary<string, List<string>> e) =>
        new(null, e.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
}
