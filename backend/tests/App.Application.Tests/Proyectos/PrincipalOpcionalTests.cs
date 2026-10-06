using System.Globalization;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Personal;
using App.Application.Tests.Proyectos.Reactivacion;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos;

/// <summary>
/// TAREA-19y (pendiente 33): principal opcional. Con PROYECTO_EXIGE_PRINCIPAL = 0 basta 1 persona (principal o back) en la
/// creación, la actualización de personal (sobre el personal resultante, P1) y la reactivación (R5/R6 con solo backs, P4);
/// con 1 vuelve C10. Advertencia "sin principal" (no bloquea), P3 (primer principal nuevo inicial si no queda inicial).
/// </summary>
public class PrincipalOpcionalTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DatosFalsos Exige = new() { Limites = new(20, 20, 20, ExigePrincipal: true) };

    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ MinimoPersonal

    [Theory]
    [InlineData(0, 1, false, null)]
    [InlineData(1, 0, false, null)]
    [InlineData(0, 0, false, "personal")]
    [InlineData(1, 0, true, null)]
    [InlineData(0, 3, true, "principales")]
    public void MinimoPersonal_SegunElParametro(int principales, int backs, bool exige, string? clave)
    {
        var r = MinimoPersonal.Validar(principales, backs, exige);
        Assert.Equal(clave, r?.Clave);
        if (r is { } error)
        {
            Assert.Equal(exige ? "Se requiere al menos 1 principal(es)." : "Se requiere al menos 1 persona (principal o back).", error.Mensaje);
        }
    }

    // ------------------------------------------------------------------ creación

    private static readonly BackSolicitud BackSinRelacion =
        new(8, "JORNADA", 2, new DateOnly(2026, 12, 12), new DateOnly(2026, 12, 15), null, null, null);

    private static CrearProyectoSolicitud SoloBacks() => Dobles.SolicitudCampo() with { Principales = [], Backs = [BackSinRelacion] };

    private static Task<ResultadoValidacionProyecto> ValidarCreacion(CrearProyectoSolicitud s, DatosFalsos? datos = null) =>
        new CrearProyectoValidador(datos ?? new DatosFalsos(), new ErpFalso(), new UsuarioFalso()).ValidarAsync(s, Ct);

    [Fact]
    public async Task Creacion_Parametro0_SoloBacks_Valido()
    {
        var r = await ValidarCreacion(SoloBacks());
        Assert.Empty(r.Errores);
        Assert.Empty(r.Valido!.Principales);
        Assert.Null(Assert.Single(r.Valido.Backs).PrincipalRelacionado); // "Sin relación"
    }

    [Fact]
    public async Task Creacion_Parametro0_UnPrincipalSinBacks_Valido()
    {
        var r = await ValidarCreacion(Dobles.SolicitudCampo() with { Principales = [Dobles.SolicitudCampo().Principales![0]], Backs = [] });
        Assert.Empty(r.Errores);
    }

    [Fact]
    public async Task Creacion_Parametro0_SinNadie_400Personal()
    {
        var r = await ValidarCreacion(Dobles.SolicitudCampo() with { Principales = [], Backs = [] });
        Assert.Equal(["Se requiere al menos 1 persona (principal o back)."], r.Errores["personal"]);
        Assert.False(r.Errores.ContainsKey("principales"));
    }

    [Fact]
    public async Task Creacion_Parametro1_SoloBacks_400Principales()
    {
        var r = await ValidarCreacion(SoloBacks(), Exige);
        Assert.Equal(["Se requiere al menos 1 principal(es)."], r.Errores["principales"]);
        Assert.False(r.Errores.ContainsKey("personal"));
    }

    private static CrearProyectoServicio ServicioCreacion() =>
        new(new CrearProyectoValidador(new DatosFalsos(), new ErpFalso(), new UsuarioFalso()), new CrucesExternosFalsos(),
            new RepositorioFalso(), new TransaccionFalsa(), new RelojFijo(new DateTimeOffset(2026, 12, 1, 15, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Creacion_VistaPrevia_SoloBacks_ConAdvertencia_YConPrincipales_Sin()
    {
        var soloBacks = await ServicioCreacion().PrevisualizarAsync(SoloBacks(), Ct);
        Assert.Equal(EstadoCrearProyecto.Previsualizado, soloBacks.Estado);
        Assert.Equal([MinimoPersonal.AdvertenciaSinPrincipal], soloBacks.Previsualizacion!.Advertencias);
        Assert.Equal("El proyecto no tendrá principal: el responsable quedará vacío.", MinimoPersonal.AdvertenciaSinPrincipal);
        Assert.Empty(soloBacks.Previsualizacion.Cruces);

        var conPrincipales = await ServicioCreacion().PrevisualizarAsync(Dobles.SolicitudCampo(), Ct);
        Assert.Empty(conPrincipales.Previsualizacion!.Advertencias);
    }

    // ------------------------------------------------------------------ actualización de personal (P1, P3)

    private const int IdProyecto = 9;

    private static PersonaGuardada Guardada(int id, RolCronograma rol, short numero, int empleado, string inicio, string fin, bool inicial = false) =>
        new(id, rol, numero, empleado, $"DEV{empleado:000}", $"EMPLEADO PRUEBA {empleado:00}", D(inicio), D(fin),
            rol == RolCronograma.Principal ? "TIPO_3" : null, rol == RolCronograma.Principal ? (byte)5 : null, 2, "JORNADA",
            null, null, null, inicial);

    /// <summary>Proyecto ACTIVO 01/09–30/11/2026 (corte 02/10/2026) con el personal indicado y sin días base.</summary>
    private static DatosEdicion Proyecto(params PersonaGuardada[] personal) =>
        new(IdProyecto, "PRY-F", "ACTIVO", D("2026-09-01"), D("2026-11-30"), personal, [],
            [new ActividadCorte(1, 1, "DEV.01", D("2026-09-01"), D("2026-11-30"))]);

    /// <summary>Back histórico (terminó en septiembre) y personas que aún no empiezan.</summary>
    private static readonly PersonaGuardada BackHistorico = Guardada(301, RolCronograma.Back, 1, 3, "2026-09-01", "2026-09-10");
    private static readonly PersonaGuardada BackFuturo = Guardada(302, RolCronograma.Back, 2, 4, "2026-10-20", "2026-10-23");
    private static readonly PersonaGuardada PrincipalFuturoInicial = Guardada(303, RolCronograma.Principal, 1, 5, "2026-10-15", "2026-11-30", inicial: true);
    private static readonly PersonaGuardada PrincipalFuturoNoInicial = Guardada(304, RolCronograma.Principal, 2, 6, "2026-10-15", "2026-11-30");

    private static BackEdicionSolicitud Enviar(PersonaGuardada b) =>
        new($"k{b.Id}", b.Id, b.EmpleadoId, "JORNADA", b.FechaInicio, b.FechaFin, b.DiasDescanso, null, null, null);

    private static PrincipalEdicionSolicitud EnviarPrincipal(PersonaGuardada p) =>
        new($"p{p.Id}", p.Id, p.EmpleadoId, p.JornadaCodigo, p.FechaInicio, p.FechaFin, null);

    private static readonly PrincipalEdicionSolicitud PrincipalNuevo = new("pn", null, 7, "TIPO_3", D("2026-10-25"), D("2026-11-30"), null);

    private static (EdicionPersonalServicio Servicio, RepositorioEdicionFalso Repo) Edicion(DatosEdicion proyecto, DatosFalsos? datos = null)
    {
        var repo = new RepositorioEdicionFalso(proyecto);
        return (new EdicionPersonalServicio(repo, datos ?? new DatosFalsos(), new CrucesEdicionFalsos(), new EdicionPersonalValidador(),
            new TransaccionFalsa(), new UsuarioFalso(), new RelojFijo(DoblesPersonal.Ahora)), repo);
    }

    [Fact]
    public async Task Personal_Parametro0_QuitarTodasLasQueNoEmpiezan_SinHistoricas_400Personal()
    {
        var (servicio, _) = Edicion(Proyecto(BackFuturo));
        var r = await servicio.PrevisualizarAsync(IdProyecto, new ActualizarPersonalSolicitud([], []), Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Assert.Equal(["Se requiere al menos 1 persona (principal o back)."], r.Errores!["personal"]);
    }

    [Fact]
    public async Task Personal_Parametro0_ConHistoricas_SinPersonalFuturo_Valido_ConAdvertencia()
    {
        var (servicio, _) = Edicion(Proyecto(BackHistorico, BackFuturo));
        var r = await servicio.PrevisualizarAsync(IdProyecto, new ActualizarPersonalSolicitud([], []), Ct);
        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        Assert.Contains(MinimoPersonal.AdvertenciaSinPrincipal, r.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task Personal_Parametro0_QuitarElUnicoPrincipal_DejandoBacks_Valido_ConAdvertencia()
    {
        var (servicio, _) = Edicion(Proyecto(PrincipalFuturoInicial, BackFuturo));
        var r = await servicio.PrevisualizarAsync(IdProyecto, new ActualizarPersonalSolicitud([], [Enviar(BackFuturo)]), Ct);
        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        Assert.Contains(MinimoPersonal.AdvertenciaSinPrincipal, r.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task Personal_ConPrincipalInicial_SinAdvertencia()
    {
        var (servicio, _) = Edicion(Proyecto(PrincipalFuturoInicial, BackFuturo));
        var r = await servicio.PrevisualizarAsync(IdProyecto,
            new ActualizarPersonalSolicitud([EnviarPrincipal(PrincipalFuturoInicial)], [Enviar(BackFuturo)]), Ct);
        Assert.DoesNotContain(MinimoPersonal.AdvertenciaSinPrincipal, r.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task Personal_Parametro1_SinPrincipalesEnElResultante_400Principales()
    {
        var (servicio, _) = Edicion(Proyecto(BackHistorico, BackFuturo), Exige);
        var r = await servicio.PrevisualizarAsync(IdProyecto, new ActualizarPersonalSolicitud([], [Enviar(BackFuturo)]), Ct);
        Assert.Equal(["Se requiere al menos 1 principal(es)."], r.Errores!["principales"]);
    }

    [Fact]
    public async Task P3_SoloBacks_MasPrincipalNuevo_ElNuevoEsInicial()
    {
        var (servicio, repo) = Edicion(Proyecto(BackFuturo));
        var r = await servicio.RegistrarAsync(IdProyecto, new ActualizarPersonalSolicitud([PrincipalNuevo], [Enviar(BackFuturo)]).ConVersion(repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal("pn", repo.Aplicado!.ClaveInicialNueva);
    }

    [Fact]
    public async Task P3_PrincipalesSinInicial_MasPrincipalNuevo_ElNuevoEsInicial()
    {
        var (servicio, repo) = Edicion(Proyecto(PrincipalFuturoNoInicial, BackFuturo));
        var r = await servicio.RegistrarAsync(IdProyecto,
            new ActualizarPersonalSolicitud([EnviarPrincipal(PrincipalFuturoNoInicial), PrincipalNuevo], [Enviar(BackFuturo)]).ConVersion(repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal("pn", repo.Aplicado!.ClaveInicialNueva);
    }

    [Fact]
    public async Task P3_InicialEliminado_MasPrincipalNuevo_ElNuevoEsInicial()
    {
        var (servicio, repo) = Edicion(Proyecto(PrincipalFuturoInicial, BackFuturo));
        var r = await servicio.RegistrarAsync(IdProyecto, new ActualizarPersonalSolicitud([PrincipalNuevo], [Enviar(BackFuturo)]).ConVersion(repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal([PrincipalFuturoInicial.Id], repo.Aplicado!.Eliminadas);
        Assert.Equal("pn", repo.Aplicado.ClaveInicialNueva);
    }

    [Fact]
    public async Task P3_ConInicialGuardado_ElNuevoNoEsInicial_ComoAntes()
    {
        var (servicio, repo) = Edicion(Proyecto(PrincipalFuturoInicial, BackFuturo));
        var r = await servicio.RegistrarAsync(IdProyecto,
            new ActualizarPersonalSolicitud([EnviarPrincipal(PrincipalFuturoInicial), PrincipalNuevo], [Enviar(BackFuturo)]).ConVersion(repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Null(repo.Aplicado!.ClaveInicialNueva);
    }

    // ------------------------------------------------------------------ reactivación (R5/R6 con solo backs, P4)

    private static (ReactivacionServicio Servicio, RepositorioEdicionFalso Repo) Reactivar(DatosEdicion? proyecto = null, DatosFalsos? datos = null)
    {
        var repo = new RepositorioEdicionFalso(proyecto ?? DoblesReactivacion.Proyecto());
        return (new ReactivacionServicio(repo, new RepositorioReactivacionFalso(null), datos ?? new DatosFalsos(), new CrucesEdicionFalsos(),
            new ReactivacionValidador(), new TransaccionFalsa(), new UsuarioFalso(), new RelojFijo(DoblesReactivacion.Ahora)), repo);
    }

    private static readonly BackEdicionSolicitud BackEnR =
        DoblesReactivacion.Back("k2", 6, DoblesReactivacion.R, DoblesReactivacion.R.AddDays(3));

    private static ReactivarProyectoSolicitud SoloBacksReactivacion(params BackEdicionSolicitud[] backs) =>
        DoblesReactivacion.Cuerpo(principales: [], backs: backs);

    [Fact]
    public async Task Reactivacion_Parametro0_SoloBacks_UnoEnR_Valido_SinInicialNuevo_YEscribeSinError()
    {
        var (servicio, repo) = Reactivar();
        var vista = await servicio.PrevisualizarAsync(DoblesReactivacion.ProyectoId, SoloBacksReactivacion(BackEnR), Ct);
        Assert.Equal(EstadoEdicion.Previsualizado, vista.Estado);
        Assert.DoesNotContain(MinimoPersonal.AdvertenciaSinPrincipal, vista.Previsualizacion!.Advertencias); // P1 histórico inicial

        var r = await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, SoloBacksReactivacion(BackEnR).ConVersion(repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Null(repo.Aplicado!.Reactivacion!.ClavePrincipalInicial); // antes: Principales[0] fallaba
    }

    [Fact]
    public async Task Reactivacion_SoloBacks_SinPrincipalInicialHistorico_ConAdvertencia()
    {
        var (servicio, _) = Reactivar(DoblesReactivacion.Proyecto(personal: [DoblesReactivacion.P1 with { EsPrincipalInicial = false }, DoblesReactivacion.K1]));
        var vista = await servicio.PrevisualizarAsync(DoblesReactivacion.ProyectoId, SoloBacksReactivacion(BackEnR), Ct);
        Assert.Contains(MinimoPersonal.AdvertenciaSinPrincipal, vista.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task Reactivacion_Parametro0_SoloBacks_NingunoEnR_400Backs()
    {
        var (servicio, _) = Reactivar();
        var tarde = DoblesReactivacion.Back("k2", 6, DoblesReactivacion.R.AddDays(1), DoblesReactivacion.R.AddDays(3));
        var r = await servicio.PrevisualizarAsync(DoblesReactivacion.ProyectoId, SoloBacksReactivacion(tarde), Ct);
        Assert.Equal(["Al menos un back debe empezar en la fecha de reactivación (11/10/2026)."], r.Errores!["backs"]);
    }

    [Fact]
    public async Task Reactivacion_Parametro0_SinNadie_400Personal()
    {
        var (servicio, _) = Reactivar();
        var r = await servicio.PrevisualizarAsync(DoblesReactivacion.ProyectoId, SoloBacksReactivacion(), Ct);
        Assert.Equal(["Se requiere al menos 1 persona (principal o back)."], r.Errores!["personal"]);
    }

    [Fact]
    public async Task Reactivacion_Parametro1_SoloBacks_400Principales()
    {
        var (servicio, _) = Reactivar(datos: Exige);
        var r = await servicio.PrevisualizarAsync(DoblesReactivacion.ProyectoId, SoloBacksReactivacion(BackEnR), Ct);
        Assert.Equal(["Se requiere al menos 1 principal(es)."], r.Errores!["principales"]);
    }

    [Fact]
    public async Task Reactivacion_ConPrincipal_ElPrimeroEsInicial_ComoAntes()
    {
        var (servicio, repo) = Reactivar();
        var r = await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, DoblesReactivacion.Cuerpo().ConVersion(repo), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal("p1", repo.Aplicado!.Reactivacion!.ClavePrincipalInicial);
    }
}
