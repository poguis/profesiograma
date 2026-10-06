using App.Application.Proyectos;
using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Tests.Proyectos.Cabecera;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using App.Application.Tests.Proyectos.Personal;
using App.Application.Tests.Proyectos.Reactivacion;

namespace App.Application.Tests.Proyectos;

/// <summary>
/// TAREA-19x (pendiente 32): token de concurrencia `versionProyecto` en los 4 registros (cambio de estado, personal,
/// reactivación y cabecera). Por servicio: (a) sin token → 400 sin leer ni abrir la transacción; (b) token viejo → 409
/// sin transacción; (c) token correcto → 200; (d) el token cambia DENTRO del applock → 409 sin escribir; (e) token
/// viejo con una solicitud que daría 400 o "No hay cambios" → 409 (V15 de la TAREA-19a). Además: los GET y las vistas
/// previas devuelven la versión y la leen ANTES que los datos.
/// </summary>
public class VersionProyectoTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly string[] OrdenEsperado = ["version", "datos"];

    private static void SinLecturasNiTransaccion(List<string> orden, TransaccionFalsa tx)
    {
        Assert.Empty(orden);
        Assert.Equal(0, tx.Iniciadas);
    }

    private static void Falta(IReadOnlyDictionary<string, string[]>? errores)
    {
        Assert.Equal([VersionProyecto.MensajeFalta], Assert.Single(errores!).Value);
        Assert.Equal(VersionProyecto.Clave, errores!.Keys.Single());
    }

    // ------------------------------------------------------------------ cambio de estado

    private static readonly CambioEstadoSolicitud Suspender15 = new("SUSPENDIDO", new DateOnly(2027, 3, 15));

    private static (CambioEstadoServicio Servicio, RepositorioCambioFalso Repo, TransaccionFalsa Tx) Estado(int ultimaVersion = 3)
    {
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto()) { UltimaVersion = ultimaVersion };
        var tx = new TransaccionFalsa();
        return (new CambioEstadoServicio(repo, new CambioEstadoValidador(), tx, new UsuarioFalso(),
            new RelojFijo(new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero))), repo, tx);
    }

    [Fact]
    public async Task Estado_a_SinToken_400_SinLeerNiAbrirTransaccion()
    {
        var (servicio, repo, tx) = Estado();
        var r = await servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);
        Assert.Equal(EstadoCambio.Invalido, r.Estado);
        Falta(r.Errores);
        SinLecturasNiTransaccion(repo.Orden, tx);
    }

    [Fact]
    public async Task Estado_b_TokenViejo_409_SinTransaccion()
    {
        var (servicio, repo, tx) = Estado();
        var r = await servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15.ConVersion(2), Ct);
        Assert.Equal(EstadoCambio.Cambiado, r.Estado);
        Assert.Equal(0, tx.Iniciadas);
        Assert.Null(repo.Aplicado);
    }

    [Fact]
    public async Task Estado_c_TokenCorrecto_200_VersionSiguiente()
    {
        var (servicio, repo, _) = Estado();
        var r = await servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15.ConVersion(3), Ct);
        Assert.Equal(EstadoCambio.Realizado, r.Estado);
        Assert.Equal((4, 4), (r.Realizado!.Version, repo.Aplicado!.Version));
    }

    [Fact]
    public async Task Estado_d_CambioDentroDelApplock_409_SinEscribir()
    {
        var (servicio, repo, tx) = Estado();
        repo.Versiones.Enqueue(3); // comprobación previa (fuera)
        repo.Versiones.Enqueue(4); // otro registro entró antes del bloqueo
        var r = await servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15.ConVersion(3), Ct);
        Assert.Equal(EstadoCambio.Cambiado, r.Estado);
        Assert.Null(repo.Aplicado);
        Assert.Equal((1, 0, 1), (tx.Iniciadas, tx.Confirmadas, tx.Revertidas));
    }

    [Fact]
    public async Task Estado_e_TokenViejo_ConFechaInvalida_409EnLugarDe400()
    {
        var (servicio, _, tx) = Estado();
        var fueraDeRango = new CambioEstadoSolicitud("SUSPENDIDO", new DateOnly(2030, 1, 1), 2);
        Assert.Equal(EstadoCambio.Cambiado, (await servicio.AplicarAsync(DoblesEstados.ProyectoId, fueraDeRango, Ct)).Estado);
        Assert.Equal(EstadoCambio.Invalido, (await servicio.AplicarAsync(DoblesEstados.ProyectoId, fueraDeRango with { VersionProyecto = 3 }, Ct)).Estado);
        Assert.Equal(0, tx.Iniciadas);
    }

    [Fact]
    public async Task Estado_VistaPrevia_DevuelveLaVersion_LeidaAntesQueLosDatos()
    {
        var (servicio, repo, _) = Estado();
        var r = await servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);
        Assert.Equal(3, r.Previsualizacion!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repo.Orden);
    }

    // ------------------------------------------------------------------ cabecera

    private static readonly EditarCabeceraSolicitud Horario2 = new(null, null, 2, null, null, null);

    private static (EdicionCabeceraServicio Servicio, RepositorioCabeceraFalso Repo, TransaccionFalsa Tx) Cabecera(int ultimaVersion = 3)
    {
        var repo = new RepositorioCabeceraFalso(DoblesCabecera.Proyecto()) { UltimaVersion = ultimaVersion };
        var tx = new TransaccionFalsa();
        return (new EdicionCabeceraServicio(repo, new EdicionCabeceraValidador(new ErpCabeceraFalso()), tx, new UsuarioFalso(),
            new RelojFijo(DoblesCabecera.Ahora)), repo, tx);
    }

    [Fact]
    public async Task Cabecera_a_SinToken_400_SinLeerNiAbrirTransaccion()
    {
        var (servicio, repo, tx) = Cabecera();
        var r = await servicio.RegistrarAsync(DoblesCabecera.ProyectoId, Horario2, Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Falta(r.Errores);
        SinLecturasNiTransaccion(repo.Orden, tx);
    }

    [Fact]
    public async Task Cabecera_b_TokenViejo_409_SinTransaccion()
    {
        var (servicio, repo, tx) = Cabecera();
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesCabecera.ProyectoId, Horario2.ConVersion(2), Ct)).Estado);
        Assert.Equal(0, tx.Iniciadas);
        Assert.Null(repo.Aplicado);
    }

    [Fact]
    public async Task Cabecera_c_TokenCorrecto_200_VersionSiguiente()
    {
        var (servicio, repo, _) = Cabecera();
        var r = await servicio.RegistrarAsync(DoblesCabecera.ProyectoId, Horario2.ConVersion(3), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal((4, 4), (r.Realizado!.Version, repo.Aplicado!.Version));
    }

    [Fact]
    public async Task Cabecera_d_CambioDentroDelApplock_409_SinEscribir()
    {
        var (servicio, repo, tx) = Cabecera();
        repo.Versiones.Enqueue(3);
        repo.Versiones.Enqueue(4);
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesCabecera.ProyectoId, Horario2.ConVersion(3), Ct)).Estado);
        Assert.Null(repo.Aplicado);
        Assert.Equal((1, 0, 1), (tx.Iniciadas, tx.Confirmadas, tx.Revertidas));
    }

    [Fact]
    public async Task Cabecera_e_V15_TokenViejo_SinCambios_409EnLugarDe400()
    {
        var (servicio, _, tx) = Cabecera();
        var mismoHorario = new EditarCabeceraSolicitud(null, null, 1, null, null, null); // el guardado: "No hay cambios"
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesCabecera.ProyectoId, mismoHorario.ConVersion(2), Ct)).Estado);
        var vigente = await servicio.RegistrarAsync(DoblesCabecera.ProyectoId, mismoHorario.ConVersion(3), Ct);
        Assert.Equal(["No hay cambios para registrar."], vigente.Errores!["general"]);
        Assert.Equal(0, tx.Iniciadas);
    }

    [Fact]
    public async Task Cabecera_GetYVistaPrevia_DevuelvenLaVersion_LeidaAntesQueLosDatos()
    {
        var (servicio, repo, _) = Cabecera();
        Assert.Equal(3, (await servicio.ObtenerAsync(DoblesCabecera.ProyectoId, Ct))!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repo.Orden);

        var (otro, repoOtro, _) = Cabecera(5);
        Assert.Equal(5, (await otro.PrevisualizarAsync(DoblesCabecera.ProyectoId, Horario2, Ct)).Previsualizacion!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repoOtro.Orden);
    }

    // ------------------------------------------------------------------ actualización de personal

    private static readonly ActualizarPersonalSolicitud CambioJornada = new([DoblesPersonal.SolP1("TIPO_2")], [DoblesPersonal.SolK1()]);
    private static readonly ActualizarPersonalSolicitud SinCambiosPersonal = new([DoblesPersonal.SolP1()], [DoblesPersonal.SolK1()]);

    private static (EdicionPersonalServicio Servicio, RepositorioEdicionFalso Repo, TransaccionFalsa Tx) Personal(int ultimaVersion = 3)
    {
        var repo = new RepositorioEdicionFalso(DoblesPersonal.Proyecto()) { UltimaVersion = ultimaVersion };
        var tx = new TransaccionFalsa();
        return (new EdicionPersonalServicio(repo, new DatosFalsos(), new CrucesEdicionFalsos(), new EdicionPersonalValidador(), tx,
            new UsuarioFalso(), new RelojFijo(DoblesPersonal.Ahora)), repo, tx);
    }

    [Fact]
    public async Task Personal_a_SinToken_400_SinLeerNiAbrirTransaccion()
    {
        var (servicio, repo, tx) = Personal();
        var r = await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, CambioJornada, Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Falta(r.Errores);
        SinLecturasNiTransaccion(repo.Orden, tx);
    }

    [Fact]
    public async Task Personal_b_TokenViejo_409_SinTransaccion()
    {
        var (servicio, repo, tx) = Personal();
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, CambioJornada.ConVersion(2), Ct)).Estado);
        Assert.Equal(0, tx.Iniciadas);
        Assert.Null(repo.Aplicado);
    }

    [Fact]
    public async Task Personal_c_TokenCorrecto_200_VersionSiguiente()
    {
        var (servicio, repo, _) = Personal();
        var r = await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, CambioJornada.ConVersion(3), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal((4, 4), (r.Realizado!.Version, repo.Aplicado!.Version));
    }

    [Fact]
    public async Task Personal_d_CambioDentroDelApplock_409_SinEscribir()
    {
        var (servicio, repo, tx) = Personal();
        repo.Versiones.Enqueue(3);
        repo.Versiones.Enqueue(4);
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, CambioJornada.ConVersion(3), Ct)).Estado);
        Assert.Null(repo.Aplicado);
        Assert.Equal((1, 0, 1), (tx.Iniciadas, tx.Confirmadas, tx.Revertidas));
    }

    [Fact]
    public async Task Personal_e_TokenViejo_SinCambios_409EnLugarDe400()
    {
        var (servicio, _, tx) = Personal();
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, SinCambiosPersonal.ConVersion(2), Ct)).Estado);
        var vigente = await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, SinCambiosPersonal.ConVersion(3), Ct);
        Assert.Equal(["No hay cambios para registrar."], vigente.Errores!["general"]);
        Assert.Equal(0, tx.Iniciadas);
    }

    [Fact]
    public async Task Personal_GetYVistaPrevia_DevuelvenLaVersion_LeidaAntesQueLosDatos()
    {
        var (servicio, repo, _) = Personal();
        Assert.Equal(3, (await servicio.ObtenerEdicionAsync(DoblesPersonal.ProyectoId, Ct))!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repo.Orden);

        var (otro, repoOtro, _) = Personal(5);
        Assert.Equal(5, (await otro.PrevisualizarAsync(DoblesPersonal.ProyectoId, CambioJornada, Ct)).Previsualizacion!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repoOtro.Orden);
    }

    // ------------------------------------------------------------------ reactivación

    private static (ReactivacionServicio Servicio, RepositorioEdicionFalso Repo, TransaccionFalsa Tx) Reactivacion(int ultimaVersion = 3)
    {
        var repo = new RepositorioEdicionFalso(DoblesReactivacion.Proyecto()) { UltimaVersion = ultimaVersion };
        var tx = new TransaccionFalsa();
        return (new ReactivacionServicio(repo, new RepositorioReactivacionFalso(new ActividadParaReactivar("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA")), new DatosFalsos(),
            new CrucesEdicionFalsos(), new ReactivacionValidador(), tx, new UsuarioFalso(), new RelojFijo(DoblesReactivacion.Ahora)), repo, tx);
    }

    [Fact]
    public async Task Reactivacion_a_SinToken_400_SinLeerNiAbrirTransaccion()
    {
        var (servicio, repo, tx) = Reactivacion();
        var r = await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, DoblesReactivacion.Cuerpo(), Ct);
        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Falta(r.Errores);
        SinLecturasNiTransaccion(repo.Orden, tx);
    }

    [Fact]
    public async Task Reactivacion_b_TokenViejo_409_SinTransaccion()
    {
        var (servicio, repo, tx) = Reactivacion();
        var r = await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, DoblesReactivacion.Cuerpo().ConVersion(2), Ct);
        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
        Assert.Equal(0, tx.Iniciadas);
        Assert.Null(repo.Aplicado);
    }

    [Fact]
    public async Task Reactivacion_c_TokenCorrecto_200_VersionSiguiente()
    {
        var (servicio, repo, _) = Reactivacion();
        var r = await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, DoblesReactivacion.Cuerpo().ConVersion(3), Ct);
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal((4, 4), (r.Realizado!.Version, repo.Aplicado!.Version));
    }

    [Fact]
    public async Task Reactivacion_d_CambioDentroDelApplock_409_SinEscribir()
    {
        var (servicio, repo, tx) = Reactivacion();
        repo.Versiones.Enqueue(3);
        repo.Versiones.Enqueue(4);
        var r = await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, DoblesReactivacion.Cuerpo().ConVersion(3), Ct);
        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
        Assert.Null(repo.Aplicado);
        Assert.Equal((1, 0, 1), (tx.Iniciadas, tx.Confirmadas, tx.Revertidas));
    }

    [Fact]
    public async Task Reactivacion_e_TokenViejo_ConFechaInvalida_409EnLugarDe400()
    {
        var (servicio, _, tx) = Reactivacion();
        var invalida = DoblesReactivacion.Cuerpo(fechaFin: new DateOnly(2026, 10, 10)); // fin antes de R: 400 con el token vigente
        Assert.Equal(EstadoEdicion.Cambiado, (await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, invalida.ConVersion(2), Ct)).Estado);
        Assert.Equal(EstadoEdicion.Invalido, (await servicio.RegistrarAsync(DoblesReactivacion.ProyectoId, invalida.ConVersion(3), Ct)).Estado);
        Assert.Equal(0, tx.Iniciadas);
    }

    [Fact]
    public async Task Reactivacion_GetYVistaPrevia_DevuelvenLaVersion_LeidaAntesQueLosDatos()
    {
        var (servicio, repo, _) = Reactivacion();
        Assert.Equal(3, (await servicio.ObtenerAsync(DoblesReactivacion.ProyectoId, Ct))!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repo.Orden);

        var (otro, repoOtro, _) = Reactivacion(5);
        Assert.Equal(5, (await otro.PrevisualizarAsync(DoblesReactivacion.ProyectoId, DoblesReactivacion.Cuerpo(), Ct)).Previsualizacion!.VersionProyecto);
        Assert.Equal(OrdenEsperado, repoOtro.Orden);
    }
}
