using App.Application.Proyectos.Crear;
using App.Domain.Proyectos.Cronograma;

namespace App.Application.Tests.Proyectos.Crear;

/// <summary>Una prueba por regla del validador (FASE_5 §3), con el mensaje exacto en español.</summary>
public class CrearProyectoValidadorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Task<ResultadoValidacionProyecto> Validar(CrearProyectoSolicitud s, DatosFalsos? datos = null, int? usuarioId = 3) =>
        new CrearProyectoValidador(datos ?? new DatosFalsos(), new ErpFalso(), new UsuarioFalso(usuarioId)).ValidarAsync(s, Ct);

    private static async Task Error(CrearProyectoSolicitud s, string clave, string mensaje, DatosFalsos? datos = null)
    {
        var r = await Validar(s, datos);
        Assert.Null(r.Valido);
        Assert.True(r.Errores.ContainsKey(clave), $"Falta el error '{clave}'. Errores: {string.Join(" | ", r.Errores.Select(e => e.Key + ": " + string.Join(", ", e.Value)))}");
        Assert.Contains(mensaje, r.Errores[clave]);
    }

    // ------------------------------------------------------------------ válido

    [Fact]
    public async Task Campo_Valido_ResuelveDatosDelErpYNumera()
    {
        var r = await Validar(Dobles.SolicitudCampo());

        Assert.Empty(r.Errores);
        var p = r.Valido!;
        Assert.Equal("COMPAÑÍA DE PRUEBA S.A.", p.Compania.Nombre);           // del ERP, no del cliente
        Assert.Equal("PROYECTO ERP DE PRUEBA", p.ProyectoErp!.Nombre);
        Assert.Equal("DEV.01", p.Actividad!.Id);
        Assert.Null(p.Dimension);
        Assert.Equal("07:00 - 18:00 (PRUEBA)", p.Horario.Descripcion);
        Assert.Equal(new TimeOnly(13, 0), p.SalidaAlmuerzo);
        Assert.Equal(Dobles.DepartamentoSig, p.DepartamentoId);                // P5: un departamento → ese
        Assert.Equal(3, p.PropietarioUsuarioId);
        Assert.Equal([new PersonaProyecto(RolCronograma.Principal, 1), new PersonaProyecto(RolCronograma.Principal, 2)],
            p.Principales.Select(x => x.Persona));
        Assert.Equal(new PersonaProyecto(RolCronograma.Back, 1), p.Backs.Single().Persona);
        Assert.Equal("PUESTO 6", p.Principales[0].Cargo);                     // sin cargo → puesto del empleado
        Assert.Equal("CARGO MANUAL", p.Principales[1].Cargo);
        Assert.Equal((byte)4, p.Principales[0].DiasDescanso);                  // de la jornada TIPO_2
        Assert.Equal(TipoRegistroBack.Jornada, p.Backs[0].TipoRegistro);
        Assert.Equal((short)1, p.Backs[0].PrincipalRelacionado);
    }

    [Fact]
    public async Task Planta_Valida_SinProyectoErpNiActividad()
    {
        var r = await Validar(Dobles.SolicitudPlanta());
        Assert.Empty(r.Errores);
        Assert.Equal("PLANTA DE PRUEBA", r.Valido!.Dimension!.Descripcion);
        Assert.Null(r.Valido.ProyectoErp);
        Assert.Null(r.Valido.Actividad);
    }

    [Fact]
    public async Task SinPrincipales_400_C10MinimoUno()
    {
        Assert.Equal(1, CrearProyectoValidador.MinimoPrincipales);
        var r = await Validar(Dobles.SolicitudCampo() with { Principales = [], Backs = [] });
        Assert.Equal(["Se requiere al menos 1 principal(es)."], r.Errores["principales"]);
    }

    // ------------------------------------------------------------------ cabecera

    public static TheoryData<string, Func<CrearProyectoSolicitud, CrearProyectoSolicitud>, string, string> CasosCabecera => new()
    {
        { "compañía obligatoria", s => s with { CompaniaId = null }, "companiaId", "La compañía es obligatoria." },
        { "compañía inexistente", s => s with { CompaniaId = 1234 }, "companiaId", "La compañía no existe en el ERP." },
        { "grupo obligatorio", s => s with { Grupo = " " }, "grupo", "El grupo es obligatorio." },
        { "grupo inexistente", s => s with { Grupo = "OTRO" }, "grupo", "El grupo 'OTRO' no existe." },
        { "grupo sin origen", s => s with { Grupo = "SIN_ORIGEN", ProyectoErpId = null, ActividadId = null }, "grupo", "El grupo SIN_ORIGEN no define si usa proyecto ERP o dimensión." },
        { "CAMPO sin proyecto ERP", s => s with { ProyectoErpId = null }, "proyectoErpId", "El grupo CAMPO requiere un proyecto ERP." },
        { "proyecto ERP inexistente", s => s with { ProyectoErpId = "DEV-ERP-002" }, "proyectoErpId", "El proyecto ERP no existe o no está activo." },
        { "CAMPO sin actividad", s => s with { ActividadId = null }, "actividadId", "El grupo CAMPO requiere una actividad." },
        { "actividad inexistente", s => s with { ActividadId = "DEV.99" }, "actividadId", "La actividad no existe en el proyecto ERP." },
        { "CAMPO con dimensión", s => s with { DimensionUegpId = "DEV-DIM-01" }, "dimensionUegpId", "El grupo CAMPO no usa dimensión." },
        { "PLANTA sin dimensión", s => Dobles.SolicitudPlanta() with { DimensionUegpId = null }, "dimensionUegpId", "El grupo PLANTA requiere una dimensión." },
        { "dimensión inexistente", s => Dobles.SolicitudPlanta() with { DimensionUegpId = "X" }, "dimensionUegpId", "La dimensión no existe en el ERP." },
        { "PLANTA con proyecto ERP", s => Dobles.SolicitudPlanta() with { ProyectoErpId = "DEV-ERP-001" }, "proyectoErpId", "El grupo PLANTA no usa proyecto ERP." },
        { "PLANTA con actividad", s => Dobles.SolicitudPlanta() with { ActividadId = "DEV.01" }, "actividadId", "El grupo PLANTA no usa actividad." },
        { "inicio obligatorio", s => s with { FechaInicio = null }, "fechaInicio", "La fecha de inicio es obligatoria." },
        { "fin obligatorio", s => s with { FechaFin = null }, "fechaFin", "La fecha fin es obligatoria." },
        { "fin antes que inicio", s => s with { FechaFin = new DateOnly(2026, 11, 30) }, "fechaFin", "La fecha fin debe ser mayor o igual a la fecha de inicio." },
        { "horario obligatorio", s => s with { HorarioCodigo = null }, "horarioCodigo", "El horario es obligatorio." },
        { "horario inexistente", s => s with { HorarioCodigo = 3 }, "horarioCodigo", "El horario no existe o no está activo." },
        { "salida obligatoria", s => s with { SalidaAlmuerzo = null }, "salidaAlmuerzo", "La hora de salida a almuerzo es obligatoria." },
        { "regreso obligatorio", s => s with { RegresoAlmuerzo = "" }, "regresoAlmuerzo", "La hora de regreso de almuerzo es obligatoria." },
        { "formato de hora", s => s with { SalidaAlmuerzo = "1pm" }, "salidaAlmuerzo", "Use el formato HH:mm." },
        { "salida antes de 11", s => s with { SalidaAlmuerzo = "10:30" }, "salidaAlmuerzo", "La salida a almuerzo debe estar entre 11:00 y 14:00." },
        { "salida después de 14", s => s with { SalidaAlmuerzo = "14:30", RegresoAlmuerzo = "15:00" }, "salidaAlmuerzo", "La salida a almuerzo debe estar entre 11:00 y 14:00." },
        { "regreso después de 15", s => s with { RegresoAlmuerzo = "15:30" }, "regresoAlmuerzo", "El regreso de almuerzo debe estar entre 12:00 y 15:00." },
        { "regreso igual a salida", s => s with { SalidaAlmuerzo = "13:00", RegresoAlmuerzo = "13:00" }, "regresoAlmuerzo", "El regreso de almuerzo debe ser posterior a la salida." },
        { "regreso antes de salida", s => s with { SalidaAlmuerzo = "14:00", RegresoAlmuerzo = "13:00" }, "regresoAlmuerzo", "El regreso de almuerzo debe ser posterior a la salida." },
    };

    [Theory]
    [MemberData(nameof(CasosCabecera))]
    public Task Cabecera_Invalida(string caso, Func<CrearProyectoSolicitud, CrearProyectoSolicitud> cambio, string clave, string mensaje)
    {
        _ = caso;
        return Error(cambio(Dobles.SolicitudCampo()), clave, mensaje);
    }

    // ------------------------------------------------------------------ personal

    public static TheoryData<string, Func<CrearProyectoSolicitud, CrearProyectoSolicitud>, string, string> CasosPersonal => new()
    {
        { "empleado obligatorio", s => s with { Principales = [s.Principales![0] with { EmpleadoId = null }] }, "principales[0].empleadoId", "El empleado es obligatorio." },
        { "empleado inactivo o inexistente", s => s with { Principales = [s.Principales![0] with { EmpleadoId = 9 }] }, "principales[0].empleadoId", "El empleado 9 no existe o no está activo." },
        { "jornada obligatoria", s => s with { Principales = [s.Principales![0] with { Jornada = null }] }, "principales[0].jornada", "La jornada es obligatoria." },
        { "jornada inexistente", s => s with { Principales = [s.Principales![0] with { Jornada = "TIPO_9" }] }, "principales[0].jornada", "La jornada 'TIPO_9' no existe." },
        { "principal antes del proyecto", s => s with { Principales = [s.Principales![0] with { FechaInicio = new DateOnly(2026, 11, 30) }] }, "principales[0].fechaInicio", "Las fechas deben estar dentro del rango del proyecto (01/12/2026 – 31/12/2026)." },
        { "principal después del proyecto", s => s with { Principales = [s.Principales![0] with { FechaFin = new DateOnly(2027, 1, 1) }] }, "principales[0].fechaFin", "Las fechas deben estar dentro del rango del proyecto (01/12/2026 – 31/12/2026)." },
        { "principal fin antes que inicio", s => s with { Principales = [s.Principales![0] with { FechaInicio = new DateOnly(2026, 12, 10), FechaFin = new DateOnly(2026, 12, 9) }] }, "principales[0].fechaFin", "La fecha fin debe ser mayor o igual a la fecha de inicio." },
        { "principal sin fecha", s => s with { Principales = [s.Principales![0] with { FechaInicio = null }] }, "principales[0].fechaInicio", "La fecha de inicio es obligatoria." },
        { "cargo largo", s => s with { Principales = [s.Principales![0] with { Cargo = new string('X', 201) }] }, "principales[0].cargo", "El cargo admite como máximo 200 caracteres." },
        { "tipo de registro inválido", s => s with { Backs = [s.Backs![0] with { TipoRegistro = "VACACIONES" }] }, "backs[0].tipoRegistro", "El tipo de registro debe ser JORNADA o DESCANSO." },
        { "tipo de registro vacío", s => s with { Backs = [s.Backs![0] with { TipoRegistro = null }] }, "backs[0].tipoRegistro", "El tipo de registro debe ser JORNADA o DESCANSO." },
        { "días de descanso > máximo", s => s with { Backs = [s.Backs![0] with { DiasDescanso = 21 }] }, "backs[0].diasDescanso", "Los días de descanso deben estar entre 0 y 20." },
        { "días de descanso negativos", s => s with { Backs = [s.Backs![0] with { DiasDescanso = -1 }] }, "backs[0].diasDescanso", "Los días de descanso deben estar entre 0 y 20." },
        { "principal relacionado inexistente", s => s with { Backs = [s.Backs![0] with { PrincipalRelacionado = 5 }] }, "backs[0].principalRelacionado", "El principal relacionado 5 no existe entre los principales enviados." },
        { "principal relacionado cero", s => s with { Backs = [s.Backs![0] with { PrincipalRelacionado = 0 }] }, "backs[0].principalRelacionado", "El principal relacionado 0 no existe entre los principales enviados." },
        { "back fuera del rango", s => s with { Backs = [s.Backs![0] with { FechaFin = new DateOnly(2027, 1, 2) }] }, "backs[0].fechaFin", "Las fechas deben estar dentro del rango del proyecto (01/12/2026 – 31/12/2026)." },
        { "back empleado inexistente", s => s with { Backs = [s.Backs![0] with { EmpleadoId = 99 }] }, "backs[0].empleadoId", "El empleado 99 no existe o no está activo." },
        { "observación larga", s => s with { Backs = [s.Backs![0] with { Observacion = new string('X', 501) }] }, "backs[0].observacion", "La observación admite como máximo 500 caracteres." },
    };

    [Theory]
    [MemberData(nameof(CasosPersonal))]
    public Task Personal_Invalido(string caso, Func<CrearProyectoSolicitud, CrearProyectoSolicitud> cambio, string clave, string mensaje)
    {
        _ = caso;
        return Error(cambio(Dobles.SolicitudCampo()), clave, mensaje);
    }

    [Fact]
    public Task MaximoPrincipales_DesdeParametro() =>
        Error(Dobles.SolicitudCampo(), "principales", "Se permiten como máximo 1 principales.", new DatosFalsos { Limites = new(1, 20, 20) });

    [Fact]
    public Task MaximoBacks_DesdeParametro() =>
        Error(Dobles.SolicitudCampo(), "backs", "Se permiten como máximo 0 backs.", new DatosFalsos { Limites = new(20, 0, 20) });

    [Fact]
    public Task MaximoDiasDescansoBack_DesdeParametro() =>
        Error(Dobles.SolicitudCampo(), "backs[0].diasDescanso", "Los días de descanso deben estar entre 0 y 1.", new DatosFalsos { Limites = new(20, 20, 1) });

    [Fact]
    public async Task BackDescanso_SinPrincipalRelacionado_EsValido()
    {
        var s = Dobles.SolicitudCampo();
        var r = await Validar(s with { Backs = [s.Backs![0] with { TipoRegistro = "descanso", PrincipalRelacionado = null, DiasDescanso = null }] });
        Assert.Empty(r.Errores);
        Assert.Equal(TipoRegistroBack.Descanso, r.Valido!.Backs[0].TipoRegistro);
        Assert.Equal((byte)0, r.Valido.Backs[0].DiasDescanso);
    }

    // ------------------------------------------------------------------ departamento (P5)

    [Fact]
    public async Task Departamento_UsuarioSinDepartamentos_Null_IgnoraLoEnviado()
    {
        var r = await Validar(Dobles.SolicitudCampo() with { DepartamentoId = 3 }, new DatosFalsos { DepartamentosUsuario = [] });
        Assert.Empty(r.Errores);
        Assert.Null(r.Valido!.DepartamentoId);
    }

    [Fact]
    public Task Departamento_UnoYEnviaOtro_Error() =>
        Error(Dobles.SolicitudCampo() with { DepartamentoId = 3 }, "departamentoId", "El departamento no está entre los asignados al usuario.");

    [Fact]
    public Task Departamento_VariosSinElegir_Error() =>
        Error(Dobles.SolicitudCampo(), "departamentoId", "Debe elegir el departamento del proyecto.",
            new DatosFalsos { DepartamentosUsuario = [new(1, "A"), new(2, "B")] });

    [Fact]
    public Task Departamento_VariosConAjeno_Error() =>
        Error(Dobles.SolicitudCampo() with { DepartamentoId = 5 }, "departamentoId", "El departamento no está entre los asignados al usuario.",
            new DatosFalsos { DepartamentosUsuario = [new(1, "A"), new(2, "B")] });

    [Fact]
    public async Task Departamento_VariosConUnoSuyo_Ok()
    {
        var r = await Validar(Dobles.SolicitudCampo() with { DepartamentoId = 2 }, new DatosFalsos { DepartamentosUsuario = [new(1, "A"), new(2, "B")] });
        Assert.Empty(r.Errores);
        Assert.Equal(2, r.Valido!.DepartamentoId);
    }

    [Fact]
    public async Task SinUsuario_Error()
    {
        var r = await Validar(Dobles.SolicitudCampo(), usuarioId: null);
        Assert.Equal(["No se pudo identificar al usuario actual."], r.Errores["usuario"]);
    }
}
