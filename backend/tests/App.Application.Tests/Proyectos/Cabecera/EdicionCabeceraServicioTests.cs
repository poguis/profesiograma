using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Personal;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using static App.Application.Tests.Proyectos.Cabecera.DoblesCabecera;

namespace App.Application.Tests.Proyectos.Cabecera;

/// <summary>"Editar cabecera" (TAREA-18) con dobles sobre el "Proyecto C" (01/11–30/11/2026; hoy 06/10/2026).</summary>
public class EdicionCabeceraServicioTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed record Entorno(EdicionCabeceraServicio Servicio, RepositorioCabeceraFalso Repo, TransaccionFalsa Tx);

    private static Entorno Crear(RepositorioCabeceraFalso? repo = null, IUsuarioActual? usuario = null)
    {
        repo ??= new RepositorioCabeceraFalso(Proyecto());
        var tx = new TransaccionFalsa();
        var servicio = new EdicionCabeceraServicio(repo, new EdicionCabeceraValidador(new ErpCabeceraFalso()), tx,
            usuario ?? new UsuarioFalso(), new RelojFijo(Ahora));
        return new Entorno(servicio, repo, tx);
    }

    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture);

    private static EditarCabeceraSolicitud S(string? inicio = null, string? fin = null, int? horario = null, string? salida = null,
        string? regreso = null, string? actividad = null, string? desde = null) =>
        new(inicio is null ? null : D(inicio), fin is null ? null : D(fin), horario, salida, regreso,
            actividad is null && desde is null ? null : new ActividadCabeceraSolicitud(actividad, desde is null ? null : D(desde)));

    private static async Task<PrevisualizacionCabeceraDto> Vista(EditarCabeceraSolicitud s, DatosCabecera? proyecto = null)
    {
        var r = await Crear(proyecto is null ? null : new RepositorioCabeceraFalso(proyecto)).Servicio.PrevisualizarAsync(ProyectoId, s, Ct);
        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        return r.Previsualizacion!;
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> Errores(EditarCabeceraSolicitud s, DatosCabecera? proyecto = null)
    {
        var r = await Crear(proyecto is null ? null : new RepositorioCabeceraFalso(proyecto)).Servicio.PrevisualizarAsync(ProyectoId, s, Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        return r.Errores!;
    }

    // ------------------------------------------------------------------ GET (C1)

    [Fact]
    public async Task Get_Activo_InicioFuturo_TodoEditable()
    {
        var e = Crear();
        var d = (await e.Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.True(d.PuedeEditar);
        Assert.Null(d.Motivo);
        Assert.Equal(new PermisosCabeceraDto(true, null, Hoy, true), d.Permisos);
        Assert.Equal((Inicio, Fin, "CAMPO", "13:00", "14:00"), (d.FechaInicio, d.FechaFin, d.Grupo, d.SalidaAlmuerzo, d.RegresoAlmuerzo));
        Assert.Equal(1, d.Horario.Codigo);
        Assert.Equal("DEV.01", d.ActividadVigente!.Codigo); // P3: vigente en max(hoy, inicio) = 01/11
        Assert.Equal(Hoy, d.Corte);
        Assert.Contains("13:00", d.OpcionesAlmuerzo.Salida);
        Assert.Equal(DateOnly.MaxValue, e.Repo.Lecturas[0].FechaDias);
    }

    [Fact]
    public async Task Get_InicioPasado_InicioNoEditable_ConMotivo()
    {
        var d = (await Crear(new RepositorioCabeceraFalso(Proyecto(inicio: D("2026-10-01"), personal: []))).Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.False(d.Permisos.FechaInicioEditable);
        Assert.Equal("La fecha de inicio ya no se puede cambiar: el proyecto empezó el 01/10/2026.", d.Permisos.MotivoFechaInicio);
        Assert.True(d.PuedeEditar);
    }

    [Fact]
    public async Task Get_NoActivo_PuedeEditarFalse_YNoVisibleNull()
    {
        var d = (await Crear(new RepositorioCabeceraFalso(Proyecto("SUSPENDIDO"))).Servicio.ObtenerAsync(ProyectoId, Ct))!;
        Assert.False(d.PuedeEditar);
        Assert.Equal("Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: SUSPENDIDO).", d.Motivo);
        Assert.False(d.Permisos.FechaInicioEditable);
        Assert.False(d.Permisos.ActividadEditable);

        Assert.Null(await Crear(new RepositorioCabeceraFalso()).Servicio.ObtenerAsync(5, Ct));
    }

    // ------------------------------------------------------------------ C2 + P1: fecha de inicio

    [Fact]
    public async Task C2_InicioDespuesDelInicioDelPersonal_400()
    {
        var e = await Errores(S(inicio: "2026-11-05"));
        Assert.Equal([EdicionCabeceraValidador.MensajePersonalAntesDelInicio], e["fechaInicio"]);
    }

    [Fact]
    public async Task C2_InicioAnteriorAHoy_400()
    {
        var e = await Errores(S(inicio: "2026-10-05"));
        Assert.Equal(["La nueva fecha de inicio no puede ser anterior a hoy (06/10/2026)."], e["fechaInicio"]);
    }

    [Fact]
    public async Task C2_ProyectoQueYaEmpezo_400()
    {
        var e = await Errores(S(inicio: "2026-10-20"), Proyecto(inicio: D("2026-10-01"), personal: []));
        Assert.Equal(["La fecha de inicio ya no se puede cambiar: el proyecto empezó el 01/10/2026."], e["fechaInicio"]);
    }

    [Fact]
    public async Task C2_InicioDespuesDelFin_400()
    {
        var e = await Errores(S(inicio: "2026-12-05"), Proyecto(personal: []));
        Assert.Equal(["La fecha fin debe ser mayor o igual a la fecha de inicio."], e["fechaFin"]);
    }

    [Fact]
    public async Task C2_P1_Adelantar_LaActividadV1SeMueve_SinTocarPersonalNiDias()
    {
        var p = await Vista(S(inicio: "2026-10-25"));

        Assert.Equal("EDICION_CABECERA", p.TipoEtapa);
        Assert.Equal([new CambioCampoDto("fechaInicio", "2026-11-01", "2026-10-25")], p.Cambios);
        var a = Assert.Single(p.Actividades);
        Assert.Equal(("MODIFICADA", D("2026-10-25"), (DateOnly?)Inicio), (a.Accion, a.FechaInicio, a.FechaInicioAnterior));
        Assert.Empty(p.DiasEliminados);
        Assert.Empty(p.PersonalRecortado);
    }

    [Fact]
    public async Task P1_Atrasar_LaActividadV1PasaAlNuevoInicio()
    {
        var p = await Vista(S(inicio: "2026-11-05"), Proyecto(personal: []));

        var a = Assert.Single(p.Actividades);
        Assert.Equal(("MODIFICADA", D("2026-11-05"), Fin), (a.Accion, a.FechaInicio, a.FechaFin));
    }

    [Fact]
    public async Task P1_ActividadQueTerminaAntesDelNuevoInicio_400()
    {
        var proyecto = Proyecto(personal: [], actividades:
            [Actividad(1, 1, "DEV.01", Inicio, D("2026-11-05")), Actividad(2, 2, "DEV.02", D("2026-11-06"), Fin, "CAMBIO_ACTIVIDAD")]);

        var e = await Errores(S(inicio: "2026-11-10"), proyecto);
        Assert.Equal([EdicionCabeceraValidador.MensajeActividadesAntesDelInicio], e["fechaInicio"]);
    }

    // ------------------------------------------------------------------ C3 / C4: fecha fin

    [Fact]
    public async Task C3_Ampliar_ExtiendeLaActividad_H14_PersonalSinCambios()
    {
        var p = await Vista(S(fin: "2026-12-20"));

        var a = Assert.Single(p.Actividades);
        Assert.Equal(("MODIFICADA", D("2026-12-20"), (DateOnly?)Fin), (a.Accion, a.FechaFin, a.FechaFinAnterior));
        Assert.Empty(p.PersonalRecortado);
        Assert.Empty(p.DiasEliminados);
        Assert.Equal(D("2026-12-20"), p.FechaFinNueva);
    }

    [Fact]
    public async Task C4_P2_FinAnteriorAHoy_400()
    {
        var e = await Errores(S(fin: "2026-10-05"), Proyecto(inicio: D("2026-10-01"), personal: []));
        Assert.Equal(["La fecha fin no puede ser anterior a hoy (06/10/2026)."], e["fechaFin"]);
    }

    [Fact]
    public async Task C4_Acortar_RecortePersonalDiasYActividad()
    {
        var e = Crear();
        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, S(fin: "2026-11-11"), Ct);

        var p = r.Previsualizacion!;
        Assert.Equal(D("2026-11-11"), e.Repo.Lecturas[0].FechaDias); // días > nueva fin
        Assert.Equal([("DEV007", Fin, D("2026-11-11")), ("DEV001", D("2026-11-12"), D("2026-11-11"))],
            p.PersonalRecortado.Select(x => (x.Empleado.CodigoEkon, x.FechaFinAnterior, x.FechaFinNueva)));
        Assert.Contains(p.DiasEliminados, d => d.Empleado.CodigoEkon == "DEV001" && d.Rol == "DESCANSO");
        Assert.All(p.DiasEliminados, d => Assert.True(d.Desde > D("2026-11-11")));
        Assert.Equal(("MODIFICADA", D("2026-11-11")), p.Actividades.Select(a => (a.Accion, a.FechaFin)).Single());
        Assert.Empty(p.Advertencias);
    }

    [Fact]
    public async Task C4_Acortar_BackSinPrincipal_YSinPrincipalInicial_Advertencias()
    {
        // P2 (inicial) empieza el 20/11 y tiene un back: al acortar al 15/11 se elimina P2 y el back queda sin principal.
        var p2 = new PersonaCorte(203, RolCronograma.Principal, 2, 6, D("2026-11-20"), Fin, null);
        var k2 = new PersonaCorte(204, RolCronograma.Back, 2, 2, D("2026-11-14"), D("2026-11-25"), 203);
        var proyecto = Proyecto(personal: [P1 with { FechaFin = D("2026-11-15") }, p2, k2], iniciales: new HashSet<int> { 203 });

        var p = await Vista(S(fin: "2026-11-15"), proyecto);

        Assert.Equal(["DEV006"], p.PersonalEliminado.Select(x => x.Empleado.CodigoEkon));
        Assert.Equal(
            ["El back 2 (EMPLEADO PRUEBA 02) quedará sin principal relacionado.", EdicionCabeceraValidador.AdvertenciaSinPrincipalInicial],
            p.Advertencias);
    }

    [Fact]
    public async Task C4_ElInicialSeConserva_SinAdvertenciaDeInicial()
    {
        var p2 = new PersonaCorte(203, RolCronograma.Principal, 2, 6, D("2026-11-20"), Fin, null);
        var p = await Vista(S(fin: "2026-11-15"), Proyecto(personal: [P1, p2]));

        Assert.DoesNotContain(EdicionCabeceraValidador.AdvertenciaSinPrincipalInicial, p.Advertencias);
    }

    // ------------------------------------------------------------------ C5: horario y almuerzo

    [Fact]
    public async Task C5_HorarioInexistenteOInactivo_400()
    {
        var e = await Errores(S(horario: 3));
        Assert.Equal(["El horario no existe o no está activo."], e["horarioCodigo"]);
    }

    [Theory]
    [InlineData("1300", null, "salidaAlmuerzo", "Use el formato HH:mm.")]
    [InlineData("10:00", null, "salidaAlmuerzo", "La salida a almuerzo debe estar entre 11:00 y 14:00.")]
    [InlineData(null, "16:00", "regresoAlmuerzo", "El regreso de almuerzo debe estar entre 12:00 y 15:00.")]
    [InlineData(null, "13:00", "regresoAlmuerzo", "El regreso de almuerzo debe ser posterior a la salida.")] // salida guardada 13:00
    public async Task C5_Almuerzo_400(string? salida, string? regreso, string clave, string mensaje)
    {
        var e = await Errores(S(salida: salida, regreso: regreso));
        Assert.Equal([mensaje], e[clave]);
    }

    [Fact]
    public async Task C5_HorarioYAlmuerzoValidos_Cambios_SinTocarCronograma()
    {
        var p = await Vista(S(horario: 2, salida: "12:00", regreso: "13:00"));

        Assert.Equal(["horario", "salidaAlmuerzo", "regresoAlmuerzo"], p.Cambios.Select(c => c.Campo));
        Assert.Equal("2 – 08:00 - 17:00 (PRUEBA)", p.Cambios[0].Nuevo);
        Assert.Empty(p.DiasEliminados);
        Assert.Equal("SIN_CAMBIO", p.Actividades.Single().Accion);
        Assert.Equal("EDICION_CABECERA", p.TipoEtapa);
    }

    // ------------------------------------------------------------------ C6: actividad

    [Fact]
    public async Task C6_GrupoSinProyectoErp_400()
    {
        var e = await Errores(S(actividad: "DEV.02", desde: "2026-11-15"), Proyecto(grupo: "PLANTA"));
        Assert.Equal(["El grupo PLANTA no usa actividad."], e["actividad.actividadId"]);
    }

    [Fact]
    public async Task C6_ActividadQueNoEstaEnElErp_YDesdeFueraDelRango_400()
    {
        var e = await Errores(S(actividad: "DEV.99", desde: "2026-12-01"));
        Assert.Equal(["La actividad no existe en el proyecto ERP."], e["actividad.actividadId"]);
        Assert.Equal(["La fecha desde debe estar dentro del rango del proyecto (01/11/2026 – 30/11/2026)."], e["actividad.desde"]);
    }

    [Fact]
    public async Task C6_MismaActividadQueLaVigente_400()
    {
        var e = await Errores(S(actividad: "DEV.01", desde: "2026-11-15"));
        Assert.Equal(["La actividad DEV.01 ya está vigente el 15/11/2026."], e["actividad.actividadId"]);
    }

    [Fact]
    public async Task C6_CambioValido_RecortaLaAnterior_NuevaVersion2_CambioActividad()
    {
        var p = await Vista(S(actividad: "DEV.02", desde: "2026-11-15"));

        Assert.Equal("CAMBIO_ACTIVIDAD", p.TipoEtapa);
        Assert.Equal(
            [("MODIFICADA", (int?)1, "DEV.01", Inicio, D("2026-11-14")), ("NUEVA", (int?)2, "DEV.02", D("2026-11-15"), Fin)],
            p.Actividades.Select(a => (a.Accion, a.Version, a.Codigo, a.FechaInicio, a.FechaFin)));
        Assert.Equal([new CambioCampoDto("actividad", "DEV.01", "DEV.02 desde 2026-11-15")], p.Cambios);
        Assert.Empty(p.Advertencias);
    }

    [Fact]
    public async Task C6_DesdeYaTranscurrido_Advertencia()
    {
        var p = await Vista(S(actividad: "DEV.02", desde: "2026-10-03"), Proyecto(inicio: D("2026-10-01"), personal: []));
        Assert.Equal([EdicionCabeceraValidador.AdvertenciaActividadPasada], p.Advertencias);
    }

    [Fact]
    public async Task C6_ConFinAmpliado_PrimeroLaFechaFin_LaNuevaLlegaAlNuevoFin()
    {
        var p = await Vista(S(fin: "2026-12-20", actividad: "DEV.02", desde: "2026-11-15"));

        Assert.Equal("EDICION_CABECERA", p.TipoEtapa);
        Assert.Equal([("MODIFICADA", D("2026-11-14")), ("NUEVA", D("2026-12-20"))], p.Actividades.Select(a => (a.Accion, a.FechaFin)));
    }

    [Fact]
    public async Task C6_ConFinAcortado_LaNuevaTerminaEnElNuevoFin()
    {
        var p = await Vista(S(fin: "2026-11-20", actividad: "DEV.02", desde: "2026-11-15"));
        Assert.Equal([("MODIFICADA", D("2026-11-14")), ("NUEVA", D("2026-11-20"))], p.Actividades.Select(a => (a.Accion, a.FechaFin)));
    }

    // ------------------------------------------------------------------ registro (C7, C8, P3)

    [Fact]
    public async Task Registrar_SoloActividad_CambioActividad_YActividadDeLaEtapaP3()
    {
        var e = Crear(new RepositorioCabeceraFalso(Proyecto()) { UltimaVersion = 2 });

        var r = await e.Servicio.RegistrarAsync(ProyectoId, S(actividad: "DEV.02", desde: "2026-11-15").ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal(new CabeceraActualizadaDto(ProyectoId, 3), r.Realizado);
        var c = e.Repo.Aplicado!;
        Assert.Equal(("CAMBIO_ACTIVIDAD", 3, Inicio, Fin, Hoy), (c.TipoMovimiento, c.Version, c.FechaInicio, c.FechaFin, c.FechaCorte));
        Assert.Equal("DEV.02", c.ActividadCodigo); // O2 (TAREA-18b): la etapa CAMBIO_ACTIVIDAD lleva la actividad nueva
        Assert.Equal([new ActividadModificada(1, Inicio, D("2026-11-14"))], c.ActividadesModificadas);
        Assert.Empty(c.ActividadesEliminadas);
        Assert.Equal(new ActividadNueva(2, "DEV.02", "ACTIVIDAD DE PRUEBA 2", "PRUEBA", D("2026-11-15"), Fin), c.ActividadNueva);
        Assert.Null(c.Recorte);
        Assert.Null(c.Horario);
        Assert.Equal((1, 1, 0), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task Registrar_P3_ActividadNuevaDesdeElInicio_EsLaDeLaEtapa()
    {
        var e = Crear();
        await e.Servicio.RegistrarAsync(ProyectoId, S(actividad: "DEV.02", desde: "2026-11-01").ConVersion(e.Repo), Ct);

        Assert.Equal("DEV.02", e.Repo.Aplicado!.ActividadCodigo);
        Assert.Equal([1], e.Repo.Aplicado.ActividadesEliminadas);
    }

    [Fact]
    public async Task Registrar_AcortarYHorario_EdicionCabecera_ConRecorteYSnapshot()
    {
        var e = Crear();
        var r = await e.Servicio.RegistrarAsync(ProyectoId, S(fin: "2026-11-11", horario: 2).ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        var c = e.Repo.Aplicado!;
        Assert.Equal("EDICION_CABECERA", c.TipoMovimiento);
        Assert.Equal(D("2026-11-11"), c.Recorte!.Fecha);
        Assert.Equal(new HorarioNuevo(2, "08:00 - 17:00 (PRUEBA)", new TimeOnly(8, 0), new TimeOnly(17, 0), 540, 480, "D"), c.Horario);
        Assert.Equal((new TimeOnly(13, 0), new TimeOnly(14, 0)), (c.SalidaAlmuerzo, c.RegresoAlmuerzo)); // sin cambio: los guardados
        Assert.Equal([new ActividadModificada(1, Inicio, D("2026-11-11"))], c.ActividadesModificadas);
        Assert.Contains("2026-11-11", c.SnapshotPersonal);
        Assert.DoesNotContain("2026-11-30", c.SnapshotPersonal);
    }

    [Fact]
    public async Task Relectura_EstadoCambiado_409()
    {
        var e = Crear(new RepositorioCabeceraFalso(Proyecto(), Proyecto("SUSPENDIDO")));
        Assert.Equal(EstadoEdicion.Cambiado, (await e.Servicio.RegistrarAsync(ProyectoId, S(horario: 2).ConVersion(e.Repo), Ct)).Estado);
        Assert.Null(e.Repo.Aplicado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task Relectura_RowVerOFechasCambiadas_409()
    {
        var rowVer = Crear(new RepositorioCabeceraFalso(Proyecto(), Proyecto(rowVer: 2)));
        Assert.Equal(EstadoEdicion.Cambiado, (await rowVer.Servicio.RegistrarAsync(ProyectoId, S(horario: 2).ConVersion(rowVer.Repo), Ct)).Estado);

        var fechas = Crear(new RepositorioCabeceraFalso(Proyecto(), Proyecto(fin: D("2026-12-15"))));
        Assert.Equal(EstadoEdicion.Cambiado, (await fechas.Servicio.RegistrarAsync(ProyectoId, S(horario: 2).ConVersion(fechas.Repo), Ct)).Estado);
        Assert.Null(fechas.Repo.Aplicado);
    }

    [Fact]
    public async Task ConflictoAlGuardar_409_YRevierte()
    {
        var e = Crear(new RepositorioCabeceraFalso(Proyecto()) { LanzarConflicto = true });
        Assert.Equal(EstadoEdicion.Cambiado, (await e.Servicio.RegistrarAsync(ProyectoId, S(horario: 2).ConVersion(e.Repo), Ct)).Estado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task NoActivo_400Proyecto_YNoVisible404()
    {
        var e = await Errores(S(horario: 2), Proyecto("TERMINADO"));
        Assert.Equal(["Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: TERMINADO)."], e["proyecto"]);

        var r = await Crear(new RepositorioCabeceraFalso()).Servicio.RegistrarAsync(5, S(horario: 2).ConVersion(1), Ct);
        Assert.Equal(EstadoEdicion.NoEncontrado, r.Estado);

        var admin = Crear(usuario: new AdminFalso());
        await admin.Servicio.PrevisualizarAsync(ProyectoId, S(horario: 2), Ct);
        Assert.Null(admin.Repo.Lecturas[0].Propietario);
    }

    // ------------------------------------------------------------------ C9: sin cambios

    [Fact]
    public async Task C9_SinCambios_VistaPreviaConAdvertencia_Registro400General()
    {
        // Mismos valores que los guardados = sin cambios.
        var mismos = S(inicio: "2026-11-01", fin: "2026-11-30", horario: 1, salida: "13:00", regreso: "14:00");
        var p = await Vista(mismos);
        Assert.Empty(p.Cambios);
        Assert.Equal(["No hay cambios."], p.Advertencias);

        var e = Crear();
        var r = await e.Servicio.RegistrarAsync(ProyectoId, mismos.ConVersion(e.Repo), Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Assert.Equal(["No hay cambios para registrar."], r.Errores!["general"]);
        Assert.Equal(0, e.Tx.Iniciadas);
    }
}
