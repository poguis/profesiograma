using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using App.Application.Tests.Proyectos.Personal;
using App.Domain.Proyectos.Cronograma;
using static App.Application.Tests.Proyectos.Reactivacion.DoblesReactivacion;

namespace App.Application.Tests.Proyectos.Reactivacion;

/// <summary>"Reactivar" (TAREA-17b) con dobles sobre el "Proyecto B" suspendido el 09/10/2026 (R = 11/10/2026).</summary>
public class ReactivacionServicioTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly ActividadParaReactivar Actividad = new("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA");

    private sealed record Entorno(
        ReactivacionServicio Servicio, RepositorioEdicionFalso Repo, RepositorioReactivacionFalso RepoReactivacion,
        CrucesEdicionFalsos Cruces, TransaccionFalsa Tx);

    private static Entorno Crear(RepositorioEdicionFalso? repo = null, CrucesEdicionFalsos? cruces = null, IUsuarioActual? usuario = null,
        DateTimeOffset? ahora = null, bool conActividad = true)
    {
        repo ??= new RepositorioEdicionFalso(Proyecto());
        cruces ??= new CrucesEdicionFalsos();
        var repoReactivacion = new RepositorioReactivacionFalso(conActividad ? Actividad : null);
        var tx = new TransaccionFalsa();
        var servicio = new ReactivacionServicio(repo, repoReactivacion, new DatosFalsos(), cruces, new ReactivacionValidador(), tx,
            usuario ?? new UsuarioFalso(), new RelojFijo(ahora ?? Ahora));
        return new Entorno(servicio, repo, repoReactivacion, cruces, tx);
    }

    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<IReadOnlyDictionary<string, string[]>> Errores(ReactivarProyectoSolicitud cuerpo, DatosEdicion? proyecto = null)
    {
        var r = await Crear(proyecto is null ? null : new RepositorioEdicionFalso(proyecto)).Servicio.PrevisualizarAsync(ProyectoId, cuerpo, Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        return r.Errores!;
    }

    // ------------------------------------------------------------------ GET reactivacion (R1, R7)

    [Fact]
    public async Task Get_Suspendido_PropuestaInicial_FechaMinimaYPersonal()
    {
        var e = Crear();

        var d = (await e.Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.True(d.PuedeReactivar);
        Assert.Null(d.Motivo);
        Assert.Equal("SUSPENDIDO", d.EstadoActual);
        Assert.Equal(F, d.FechaFinActual);
        Assert.Equal(D("2026-10-10"), d.FechaMinima);
        Assert.Equal(new PrincipalPropuestoDto(new EmpleadoPropuestoDto(7, "DEV007", "EMPLEADO PRUEBA 07", true), "TIPO_2", "PUESTO 7"),
            d.PrincipalPropuesto);
        Assert.Equal([101, 102], d.Personal.Select(p => p.Id));
        Assert.True(d.Personal[0].EsPrincipalInicial);
        Assert.Empty(d.Advertencias);
        Assert.Equal(new LimitesEdicionDto(20, 20, 20), d.Limites);
        Assert.Equal(3, e.Repo.Lecturas[0].Propietario); // gestor: visibilidad R1
    }

    [Theory]
    [InlineData("ACTIVO")]
    [InlineData("TERMINADO")]
    public async Task Get_NoSuspendido_PuedeReactivarFalse_SinPropuesta(string estado)
    {
        var d = (await Crear(new RepositorioEdicionFalso(Proyecto(estado))).Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.False(d.PuedeReactivar);
        Assert.Equal($"Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: {estado}).", d.Motivo);
        Assert.Null(d.PrincipalPropuesto);
        Assert.Empty(d.Advertencias);
    }

    [Fact]
    public async Task Get_VariosIniciales_ProponeElDeMayorFechaFin()
    {
        // Proyecto ya reactivado una vez (H4: dos iniciales, uno por periodo).
        var otro = new PersonaGuardada(103, RolCronograma.Principal, 2, 6, "DEV006", "EMPLEADO PRUEBA 06",
            D("2026-10-05"), D("2026-10-08"), "TIPO_3", 5, 2, "JORNADA", null, "PUESTO 6", null, true);
        var proyecto = Proyecto(personal: [P1, K1, otro]);

        var d = (await Crear(new RepositorioEdicionFalso(proyecto)).Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.Equal(7, d.PrincipalPropuesto!.Empleado.Id); // P1: fin 09/10 > 08/10
        Assert.Empty(d.Advertencias);
    }

    [Fact]
    public async Task Get_SinPrincipalInicial_ProponeElUltimoPrincipal_ConAdvertencia()
    {
        var otro = new PersonaGuardada(103, RolCronograma.Principal, 2, 6, "DEV006", "EMPLEADO PRUEBA 06",
            D("2026-10-01"), D("2026-10-08"), "TIPO_3", 5, 2, "JORNADA", null, "PUESTO 6", null, false);
        var proyecto = Proyecto(personal: [P1 with { EsPrincipalInicial = false }, K1, otro]);

        var d = (await Crear(new RepositorioEdicionFalso(proyecto)).Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.Equal(7, d.PrincipalPropuesto!.Empleado.Id);
        Assert.Equal([ReactivacionServicio.AdvertenciaSinInicial], d.Advertencias);
    }

    [Fact]
    public async Task Get_EmpleadoInactivo_SePropone_ConAdvertencia()
    {
        var inactivo = P1 with { EmpleadoId = 9, CodigoEkon = "DEV009", NombreCompleto = "EMPLEADO PRUEBA 09" };

        var d = (await Crear(new RepositorioEdicionFalso(Proyecto(personal: [inactivo, K1]))).Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.Equal(new EmpleadoPropuestoDto(9, "DEV009", "EMPLEADO PRUEBA 09", false), d.PrincipalPropuesto!.Empleado);
        Assert.Equal([ReactivacionServicio.AdvertenciaInactivo], d.Advertencias);
    }

    [Fact]
    public async Task Get_SinPrincipales_PropuestaNull_ConAdvertencia()
    {
        var d = (await Crear(new RepositorioEdicionFalso(Proyecto(personal: [K1]))).Servicio.ObtenerAsync(ProyectoId, Ct))!;

        Assert.True(d.PuedeReactivar);
        Assert.Null(d.PrincipalPropuesto);
        Assert.Equal([ReactivacionServicio.AdvertenciaSinPrincipales], d.Advertencias);
    }

    [Fact]
    public async Task Get_NoVisible_Null_YAdminSinFiltro()
    {
        Assert.Null(await Crear(new RepositorioEdicionFalso()).Servicio.ObtenerAsync(5, Ct));

        var admin = Crear(usuario: new AdminFalso());
        await admin.Servicio.ObtenerAsync(ProyectoId, Ct);
        Assert.Null(admin.Repo.Lecturas[0].Propietario);
    }

    // ------------------------------------------------------------------ validación (R2, R5, R6): 400

    [Fact]
    public async Task R_IgualALaFechaFinActual_400_H9()
    {
        var e = await Errores(Cuerpo(fecha: F, principales: [Principal(inicio: F)]));
        Assert.Equal(["La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (09/10/2026)."], e["fecha"]);
        Assert.Single(e);
    }

    [Fact]
    public async Task FechaYFechaFinObligatorias_400()
    {
        var e = await Errores(new ReactivarProyectoSolicitud(null, null, [Principal()], []));
        Assert.Equal(["La fecha de reactivación es obligatoria."], e["fecha"]);
        Assert.Equal(["La fecha fin es obligatoria."], e["fechaFin"]);
    }

    [Fact]
    public async Task FechaFinAnteriorAR_400()
    {
        var e = await Errores(Cuerpo(fechaFin: D("2026-10-10")));
        Assert.Equal(["La fecha fin no puede ser anterior a la fecha de reactivación (11/10/2026)."], e["fechaFin"]);
    }

    [Theory]
    [InlineData("ACTIVO")]
    [InlineData("TERMINADO")]
    public async Task ProyectoNoSuspendido_400_Proyecto(string estado)
    {
        var e = await Errores(Cuerpo(), Proyecto(estado, fin: FinOriginal));
        Assert.Equal([$"Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: {estado})."], e["proyecto"]);
    }

    [Fact]
    public async Task PrimerPrincipalQueNoEmpiezaEnR_400_R6()
    {
        var e = await Errores(Cuerpo(principales: [Principal(inicio: D("2026-10-12"))]));
        Assert.Equal(["El primer principal debe empezar en la fecha de reactivación (11/10/2026)."], e["principales[0].fechaInicio"]);
    }

    [Fact]
    public async Task PersonaConInicioAnteriorAR_400()
    {
        var e = await Errores(Cuerpo(backs: [Back("k2", 8, D("2026-10-10"), D("2026-10-15"), principalClave: "p1")]));
        Assert.Equal(["La fecha de inicio no puede ser anterior a la fecha de reactivación (11/10/2026)."], e["backs[0].fechaInicio"]);
    }

    [Fact]
    public async Task PersonaConFinPosteriorALaNuevaFechaFin_400_RN08()
    {
        var e = await Errores(Cuerpo(fechaFin: D("2026-10-31"), principales: [Principal(fin: D("2026-11-05"))]));
        Assert.Equal(["Las fechas deben estar dentro del rango del proyecto (28/09/2026 – 31/10/2026)."], e["principales[0].fechaFin"]);
    }

    [Fact]
    public async Task SinPrincipales_400()
    {
        var e = await Errores(Cuerpo(principales: []));
        Assert.Equal(["Se requiere al menos 1 principal(es)."], e["principales"]);
    }

    [Fact]
    public async Task IdPresente_400_UnSoloErrorEnId()
    {
        var e = await Errores(Cuerpo(principales: [Principal(id: 101)],
            backs: [new BackEdicionSolicitud("k1", 102, 8, "JORNADA", R, D("2026-10-15"), 0, "p1", null, null)]));

        Assert.Equal(["principales[0].id", "backs[0].id"], e.Keys.Order(StringComparer.Ordinal).Reverse());
        Assert.Equal([EdicionPersonalValidador.MensajeIdReactivacion], e["principales[0].id"]);
        Assert.Equal([EdicionPersonalValidador.MensajeIdReactivacion], e["backs[0].id"]);
    }

    [Fact]
    public async Task EmpleadoInactivo_400()
    {
        var e = await Errores(Cuerpo(principales: [Principal(empleado: 9)]));
        Assert.Equal(["El empleado 9 no existe o no está activo."], e["principales[0].empleadoId"]);
    }

    [Fact]
    public async Task BackConPrincipalIdQueNoEsPrincipal_400()
    {
        var e = await Errores(Cuerpo(backs: [Back("k2", 8, R, D("2026-10-15"), principalId: 102)]));
        Assert.Equal(["El principal 102 no es un principal histórico de este proyecto."], e["backs[0].principalId"]);
    }

    [Fact]
    public async Task MaximoDePrincipales_SobreLasNuevas_400()
    {
        var principales = Enumerable.Range(1, 21).Select(i => Principal($"p{i}", empleado: 1 + (i % 8))).ToArray();
        var e = await Errores(Cuerpo(principales: principales));
        Assert.Equal(["Se permiten como máximo 20 principales."], e["principales"]);
    }

    // ------------------------------------------------------------------ vista previa: motor, actividad, cruces

    [Fact]
    public async Task Previsualizar_CorteR_TramosDesdeR_Actividad_SinAdvertencias()
    {
        var e = Crear();

        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(), Ct);

        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        var p = r.Previsualizacion!;
        Assert.Equal(R, p.Corte);
        Assert.Equal(F, p.FechaFinActual);
        Assert.Equal(FinNueva, p.FechaFinNueva);
        Assert.Equal(new ActividadReactivacionDto("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA", R, FinNueva), p.Actividad);
        Assert.Equal([F], e.RepoReactivacion.Fechas); // actividad vigente en la fecha de suspensión
        Assert.Empty(p.Advertencias); // R futura: sin advertencia (P3)
        Assert.Empty(p.Cruces);

        // Nuevo P2 (máx + 1) DEV007 TIPO_2 desde R: 11 días de trabajo y 4 de descanso AUTO.
        var nuevo = p.Personal.Single(x => x.Clave == "p1");
        Assert.Equal(("NUEVO", "NUEVO", (short)2), (nuevo.Clase, nuevo.Accion, nuevo.Numero));
        Assert.Contains(p.Tramos, t => t.Rol == "PRINCIPAL" && t.Persona.Numero == 2 && t.Inicio == R && t.Fin == D("2026-10-21"));
        Assert.Contains(p.Tramos, t => t.Rol == "DESCANSO" && t.Tipo == "AUTO" && t.Persona.Numero == 2 && t.Inicio == D("2026-10-22"));
        Assert.DoesNotContain(p.Tramos, t => t.Persona.Numero == 2 && t.Inicio < R);
    }

    [Fact]
    public async Task Pendiente23_BackRecortado_QuedaHistorico_SinRegenerarSuDescanso()
    {
        // K1 terminó el 09/10 con 3 días de descanso: fin + descanso = 12/10 ≥ R = 11/10. Antes era "vigente" (H10).
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(), Ct);

        var p = r.Previsualizacion!;
        Assert.Equal(("HISTORICO", "SIN_CAMBIO"), p.Personal.Where(x => x.Id == 102).Select(x => (x.Clase, x.Accion)).Single());
        Assert.Equal(("HISTORICO", "SIN_CAMBIO"), p.Personal.Where(x => x.Id == 101).Select(x => (x.Clase, x.Accion)).Single());
        Assert.DoesNotContain(p.Tramos, t => t.CodigoEkon == "DEV008" && t.Fin >= F.AddDays(1));
    }

    [Fact]
    public async Task R_Pasada_AdvertenciaDiasTranscurridos_R3()
    {
        var e = Crear(ahora: new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero));

        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(), Ct);

        Assert.Equal(["Se generarán días ya transcurridos."], r.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task SinActividad_NoSeCreaNinguna()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto(conActividad: false)), conActividad: false);

        var p = (await e.Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(), Ct)).Previsualizacion!;
        Assert.Null(p.Actividad);

        var r = await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo().ConVersion(e.Repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Null(e.Repo.Aplicado!.Reactivacion!.Actividad);
        Assert.Null(e.Repo.Aplicado.ActividadCodigo);
    }

    [Fact]
    public async Task CrucesExternos_VistaPrevia200ConCruces_Registro409SinTransaccion()
    {
        var externo = new AsignacionExistente(5, R, (byte)RolCronograma.Principal, 5, "PRY-X", "OTRO PROYECTO", "ACTIVO");
        var e = Crear(cruces: new CrucesEdicionFalsos([externo]));
        var cuerpo = Cuerpo(backs: [Back("k2", 5, R, D("2026-10-12"), principalClave: "p1")]);

        var vista = await e.Servicio.PrevisualizarAsync(ProyectoId, cuerpo, Ct);
        Assert.Equal(EstadoEdicion.Previsualizado, vista.Estado);
        var cruce = Assert.Single(vista.Previsualizacion!.Cruces);
        Assert.Equal("EXTERNO", cruce.Origen);
        Assert.Equal(ProyectoId, e.Cruces.Excluidos[0]); // excluirProyectoId

        var registro = await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);
        Assert.Equal(EstadoEdicion.ConCruces, registro.Estado);
        Assert.Equal(0, e.Tx.Iniciadas);
        Assert.Null(e.Repo.Aplicado);
    }

    // ------------------------------------------------------------------ registro (R4, R6, R8, R9)

    [Fact]
    public async Task Registrar_EtapaReactivacion_ProyectoActivo_ActividadNueva()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto()) { UltimaVersion = 2 });
        var cuerpo = Cuerpo(backs: [Back("k2", 8, D("2026-10-20"), D("2026-10-23"), principalClave: "p1", diasDescanso: 2)]);

        var r = await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal(new ProyectoReactivadoDto(ProyectoId, "ACTIVO", 3), r.Realizado);
        Assert.Equal((1, 1, 0), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));

        var c = e.Repo.Aplicado!;
        Assert.Equal(("REACTIVACION", 3), (c.TipoMovimiento, c.Version));
        Assert.Equal((R, R, FinNueva), (c.Corte, c.FechaInicioProyecto, c.FechaFinProyecto)); // R9: etapa de R a la nueva fecha fin
        Assert.Equal("DEV.01", c.ActividadCodigo); // actividad vigente en R: la nueva
        Assert.Equal(new ReactivacionAplicar("p1", FinNueva, new ActividadNueva(2, "DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA", R, FinNueva)),
            c.Reactivacion);
        Assert.Empty(c.Vigentes);
        Assert.Empty(c.Eliminadas);
        Assert.Equal([("p1", (short)2), ("k2", (short)2)], c.Nuevas.Select(n => (n.Clave, n.Numero)));
        Assert.All(c.Dias, d => Assert.True(d.Dia.Fecha >= R));
        Assert.All(c.Dias, d => Assert.NotNull(d.ClaveNueva));
    }

    [Fact]
    public async Task H4_ElInicialNuevoEsElPrimerPrincipal_YElAnteriorNoSeToca()
    {
        var e = Crear();
        var cuerpo = Cuerpo(principales: [Principal("a"), Principal("b", empleado: 6, inicio: D("2026-10-15"))]);

        await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);

        var c = e.Repo.Aplicado!;
        Assert.Equal("a", c.Reactivacion!.ClavePrincipalInicial);
        Assert.Empty(c.Vigentes); // P1 (inicial anterior) se conserva sin cambios: queda histórico
        Assert.Equal([("a", (short)2), ("b", (short)3)], c.Nuevas.Select(n => (n.Clave, n.Numero)));
    }

    [Fact]
    public async Task Registrar_Relaciones_ConPrincipalNuevo_YConHistorico()
    {
        var e = Crear();
        var cuerpo = Cuerpo(backs:
        [
            Back("k2", 8, D("2026-10-20"), D("2026-10-23"), principalClave: "p1"),
            Back("k3", 6, D("2026-10-24"), D("2026-10-25"), principalId: 101),
        ]);

        await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);

        var nuevas = e.Repo.Aplicado!.Nuevas.ToDictionary(n => n.Clave);
        Assert.Equal(new RelacionPrincipal(null, "p1"), nuevas["k2"].Relacion);
        Assert.Equal(new RelacionPrincipal(101, null), nuevas["k3"].Relacion);
    }

    [Fact]
    public async Task Registrar_SnapshotConHistoricasYNuevas()
    {
        var e = Crear();
        await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo().ConVersion(e.Repo), Ct);

        var snapshot = e.Repo.Aplicado!.SnapshotPersonal;
        Assert.Contains("DEV007", snapshot);
        Assert.Contains("DEV008", snapshot);
        Assert.Contains("2026-10-11", snapshot);
    }

    [Fact]
    public async Task Relectura_EstadoCambiado_409SinEscribir()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto(), Proyecto("ACTIVO", fin: FinNueva)));

        var r = await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo().ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
        Assert.Null(e.Repo.Aplicado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task Relectura_FechaFinCambiada_409SinEscribir()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto(), Proyecto(fin: D("2026-10-08"))));

        var r = await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo().ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
        Assert.Null(e.Repo.Aplicado);
    }

    [Fact]
    public async Task ConflictoDeConcurrencia_409()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto()) { LanzarConflicto = true });

        var r = await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo().ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task NoVisible_404()
    {
        var r = await Crear(new RepositorioEdicionFalso()).Servicio.RegistrarAsync(5, Cuerpo().ConVersion(1), Ct);
        Assert.Equal(EstadoEdicion.NoEncontrado, r.Estado);
    }
}
