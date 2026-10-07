using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using static App.Application.Tests.Proyectos.Personal.DoblesPersonal;

namespace App.Application.Tests.Proyectos.Personal;

/// <summary>"Actualizar personal" (TAREA-17) con dobles sobre el "Proyecto A" (corte 02/10/2026). Casos P1–P15 del plan.</summary>
public class EdicionPersonalServicioTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed record Entorno(EdicionPersonalServicio Servicio, RepositorioEdicionFalso Repo, CrucesEdicionFalsos Cruces, TransaccionFalsa Tx);

    private static Entorno Crear(RepositorioEdicionFalso? repo = null, CrucesEdicionFalsos? cruces = null, IUsuarioActual? usuario = null)
    {
        repo ??= new RepositorioEdicionFalso(Proyecto());
        cruces ??= new CrucesEdicionFalsos();
        var tx = new TransaccionFalsa();
        var servicio = new EdicionPersonalServicio(repo, new DatosFalsos(), cruces, new EdicionPersonalValidador(), tx,
            usuario ?? new UsuarioFalso(), new RelojFijo(Ahora), new EmpleadosErpFalsos());
        return new Entorno(servicio, repo, cruces, tx);
    }

    private static ActualizarPersonalSolicitud Cuerpo(PrincipalEdicionSolicitud[]? principales = null, BackEdicionSolicitud[]? backs = null) =>
        new(principales ?? [SolP1()], backs ?? [SolK1()]);

    private static bool TieneTramo(PrevisualizacionPersonalDto p, string rol, string tipo, string desde, string hasta, string ekon) =>
        p.Tramos.Any(t => t.Rol == rol && t.Tipo == tipo && t.CodigoEkon == ekon
                          && t.Inicio == DateOnly.Parse(desde, System.Globalization.CultureInfo.InvariantCulture)
                          && t.Fin == DateOnly.Parse(hasta, System.Globalization.CultureInfo.InvariantCulture));

    // ------------------------------------------------------------------ P1: GET edicion

    [Fact]
    public async Task P1_Edicion_CorteClasesPermisosYLimites()
    {
        var e = Crear();

        var d = (await e.Servicio.ObtenerEdicionAsync(ProyectoId, Ct))!;

        Assert.Equal(Corte, d.Corte);
        Assert.True(d.PuedeEditar);
        Assert.Null(d.Motivo);
        var p1 = d.Personal.Single(p => p.Id == 91);
        Assert.Equal("VIGENTE", p1.Clase);
        Assert.Equal(new PermisosEdicionDto(false, new DateOnly(2026, 10, 1), true, false), p1.Permisos);
        Assert.Equal("HISTORICO", d.Personal.Single(p => p.Id == 92).Clase);
        Assert.Equal(new PermisosEdicionDto(false, null, false, false), d.Personal.Single(p => p.Id == 92).Permisos);
        Assert.Equal(new PermisosEdicionDto(true, new DateOnly(2026, 10, 1), false, true), d.Personal.Single(p => p.Id == 93).Permisos);
        Assert.Equal(new LimitesEdicionDto(20, 20, 20, false), d.Limites);
        Assert.Equal(3, e.Repo.Lecturas[0].Propietario); // gestor: visibilidad R1
    }

    [Fact]
    public async Task Edicion_NoActivo_PuedeEditarFalse_ConMotivo()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto("SUSPENDIDO")));
        var d = (await e.Servicio.ObtenerEdicionAsync(ProyectoId, Ct))!;
        Assert.False(d.PuedeEditar);
        Assert.Equal("Solo se puede modificar el personal de un proyecto ACTIVO (estado actual: SUSPENDIDO).", d.Motivo);
    }

    [Fact]
    public async Task Edicion_NoVisible_Null_YAdminSinFiltro()
    {
        Assert.Null(await Crear(new RepositorioEdicionFalso()).Servicio.ObtenerEdicionAsync(5, Ct));

        var admin = Crear(usuario: new AdminFalso());
        await admin.Servicio.ObtenerEdicionAsync(ProyectoId, Ct);
        Assert.Null(admin.Repo.Lecturas[0].Propietario);
    }

    // ------------------------------------------------------------------ P2–P7: vista previa

    [Fact]
    public async Task P2_CambiarJornadaDeUnVigente_M3_SinCruces()
    {
        var e = Crear();

        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1("TIPO_2")]), Ct);

        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        var p = r.Previsualizacion!;
        Assert.Empty(p.Cruces);
        Assert.Empty(p.Advertencias);
        Assert.True(TieneTramo(p, "DESCANSO", "AUTO", "2026-10-02", "2026-10-05", "DEV006")); // tras el tramo base 28/09–01/10
        Assert.True(TieneTramo(p, "PRINCIPAL", "AUTO", "2026-10-06", "2026-10-16", "DEV006"));
        Assert.True(TieneTramo(p, "PRINCIPAL", "AUTO", "2026-11-20", "2026-11-29", "DEV006"));
        Assert.True(TieneTramo(p, "BACK", "MANUAL", "2026-10-20", "2026-10-23", "DEV007"));
        Assert.Equal("MODIFICADO", p.Personal.Single(x => x.Id == 91).Accion);
        Assert.Equal("SIN_CAMBIO", p.Personal.Single(x => x.Id == 93).Accion);
        Assert.Equal(EdicionPersonalValidador.SinCambio, p.Personal.Single(x => x.Id == 92).Accion);
        Assert.Equal([9], e.Cruces.Excluidos); // cruces externos excluyendo este proyecto
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    [Fact]
    public async Task P3_AcortarUnVigenteAlDiaAnteriorAlCorte_SinDiasDesdeElCorte()
    {
        var e = Crear();

        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1(fin: new DateOnly(2026, 10, 1))]), Ct);

        var p = r.Previsualizacion!;
        Assert.DoesNotContain(p.Tramos, t => t.CodigoEkon == "DEV006" && t.Fin >= Corte);
        Assert.Equal("MODIFICADO", p.Personal.Single(x => x.Id == 91).Accion);
    }

    [Fact]
    public async Task P3b_FinAnteriorAlDiaAnteriorAlCorte_400()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1(fin: new DateOnly(2026, 9, 30))]), Ct);

        Assert.Equal(["La fecha fin no puede ser anterior al 01/10/2026 (día anterior al corte)."], r.Errores!["principales[0].fechaFin"]);
    }

    [Fact]
    public async Task P4_OmitirUnVigenteQueAunNoEmpieza_SeElimina()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(backs: []), Ct);

        var p = r.Previsualizacion!;
        Assert.Equal("ELIMINADO", p.Personal.Single(x => x.Id == 93).Accion);
        Assert.DoesNotContain(p.Tramos, t => t.CodigoEkon == "DEV007" && t.Inicio >= Corte);
        Assert.True(TieneTramo(p, "PRINCIPAL", "AUTO", "2026-10-02", "2026-10-02", "DEV006"));
        Assert.True(TieneTramo(p, "DESCANSO", "AUTO", "2026-10-03", "2026-10-04", "DEV006"));
    }

    [Fact]
    public async Task P5_NuevoConInicioPasado_M1_Advierte()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId,
            Cuerpo(backs: [SolK1(), BackNuevo("k2", 8, "2026-09-28", "2026-10-04")]), Ct);

        var p = r.Previsualizacion!;
        Assert.Equal(["Se generarán días anteriores al corte (02/10/2026) para el personal nuevo."], p.Advertencias);
        Assert.True(TieneTramo(p, "BACK", "MANUAL", "2026-09-28", "2026-10-04", "DEV008"));
        Assert.Empty(p.Cruces);
        var nuevo = p.Personal.Single(x => x.Clave == "k2");
        Assert.Equal(("NUEVO", "NUEVO", (short)2), (nuevo.Clase, nuevo.Accion, nuevo.Numero)); // máx + 1 (el back 1 existe)
    }

    [Fact]
    public async Task P6_CruceHistoricoPropio_VistaPrevia200_Registro409()
    {
        var cuerpo = Cuerpo(backs: [SolK1(), BackNuevo("k2", 6, "2026-09-29", "2026-09-30")]);
        var e = Crear();

        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, cuerpo, Ct);
        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        Assert.Equal([new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 30)],
            r.Previsualizacion!.Cruces.Where(c => c.Origen == CalculadorCruces.OrigenHistorico).Select(c => c.Fecha));
        Assert.All(r.Previsualizacion.Cruces, c => Assert.Equal(CalculadorCruces.MismoProyecto, c.Proyecto));

        var registro = await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);
        Assert.Equal(EstadoEdicion.ConCruces, registro.Estado);
        Assert.NotEmpty(registro.Previsualizacion!.Resumen);
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    [Fact]
    public async Task P7_CruceExterno_VistaPrevia200_Registro409()
    {
        AsignacionExistente Dia5(int dia) => new(5, new DateOnly(2026, 10, dia), 1, 5, "PRY-20261001-4c34f3", "PROYECTO ERP DE PRUEBA", "ACTIVO");
        var cruces = new CrucesEdicionFalsos([Dia5(5), Dia5(6)]);
        var cuerpo = Cuerpo(backs: [SolK1(), BackNuevo("k2", 5, "2026-10-05", "2026-10-06")]);
        var e = Crear(cruces: cruces);

        var r = await e.Servicio.PrevisualizarAsync(ProyectoId, cuerpo, Ct);
        var externos = r.Previsualizacion!.Cruces.Where(c => c.Origen == CalculadorCruces.OrigenExterno).ToList();
        Assert.Equal(2, externos.Count);
        Assert.All(externos, c => Assert.Equal("PRY-20261001-4c34f3", c.ProyectoCodigo));

        Assert.Equal(EstadoEdicion.ConCruces, (await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct)).Estado);
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    // ------------------------------------------------------------------ P8–P11, P15 y otras validaciones

    [Fact]
    public async Task P8_HistoricoEnviado_400()
    {
        var historico = new PrincipalEdicionSolicitud("p2", 92, 7, "TIPO_2", Inicio, new DateOnly(2026, 9, 30), null);
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1(), historico]), Ct);

        Assert.Equal(["La persona P2 es histórica (terminó el 30/09/2026) y no se puede modificar."], r.Errores!["principales[1].id"]);
    }

    [Fact]
    public async Task P8_HistoricoEnviado_SoloUnError_EnId()
    {
        // Corrección tras la prueba HTTP del 05/10/2026: un Id inválido detiene la validación de su fila.
        var historico = new PrincipalEdicionSolicitud("p2", 92, 7, "TIPO_2", Inicio, new DateOnly(2026, 9, 30), null);
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1(), historico]), Ct);

        Assert.Equal(["principales[1].id"], r.Errores!.Keys);
    }

    [Fact]
    public async Task IdDeLaOtraLista_SoloUnError_EnId()
    {
        var principalComoBack = new BackEdicionSolicitud("k9", 91, 6, "XYZ", null, null, 99, "nadie", null, null);
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(backs: [SolK1(), principalComoBack]), Ct);

        Assert.Equal(["backs[1].id"], r.Errores!.Keys);
        Assert.Equal(["La persona 91 no es un back."], r.Errores["backs[1].id"]);
    }

    [Fact]
    public async Task P9_CambioDeEmpleadoDeUnVigente_400()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1(empleado: 7)]), Ct);

        Assert.Equal(["No se puede cambiar el empleado de una persona vigente. Para reemplazarla, acorte su fecha fin y agregue una persona nueva."],
            r.Errores!["principales[0].empleadoId"]);
    }

    [Fact]
    public async Task P10_ProyectoSuspendido_400()
    {
        var r = await Crear(new RepositorioEdicionFalso(Proyecto("SUSPENDIDO"))).Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(), Ct);

        Assert.Equal(["Solo se puede modificar el personal de un proyecto ACTIVO (estado actual: SUSPENDIDO)."], r.Errores!["proyecto"]);
    }

    [Fact]
    public async Task P11_NoVisible_404()
    {
        var e = Crear(new RepositorioEdicionFalso());
        Assert.Equal(EstadoEdicion.NoEncontrado, (await e.Servicio.PrevisualizarAsync(5, Cuerpo(), Ct)).Estado);
        Assert.Equal(EstadoEdicion.NoEncontrado, (await e.Servicio.RegistrarAsync(5, Cuerpo().ConVersion(e.Repo), Ct)).Estado);
    }

    [Fact]
    public async Task P15_ProyectoQueYaTermino_400()
    {
        var r = await Crear(new RepositorioEdicionFalso(Proyecto(fin: new DateOnly(2026, 10, 1)))).Servicio
            .PrevisualizarAsync(ProyectoId, Cuerpo(), Ct);

        Assert.Equal(["El proyecto finalizó el 01/10/2026; amplía la fecha fin antes de modificar el personal."], r.Errores!["proyecto"]);
    }

    [Fact]
    public async Task FaltaUnVigenteQueYaEmpezo_400()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(principales: []), Ct);

        Assert.Equal(["Falta P1 (EMPLEADO PRUEBA 06): una persona que ya empezó no se puede quitar; acorte su fecha fin al 01/10/2026."],
            r.Errores!["principales"]);
    }

    [Fact]
    public async Task CambiarInicioDeUnVigenteQueYaEmpezo_400_YDeUnoFuturoAntesDelCorte_400()
    {
        var r1 = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo([SolP1(inicio: new DateOnly(2026, 9, 22))]), Ct);
        Assert.Equal(["No se puede cambiar la fecha de inicio de una persona que ya empezó (21/09/2026)."], r1.Errores!["principales[0].fechaInicio"]);

        var r2 = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(backs: [SolK1(new DateOnly(2026, 10, 1))]), Ct);
        Assert.Equal(["La fecha de inicio de una persona que aún no empieza no puede ser anterior al corte (02/10/2026)."],
            r2.Errores!["backs[0].fechaInicio"]);
    }

    [Fact]
    public async Task IdInexistente_Cambiado409()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId,
            Cuerpo(backs: [SolK1() with { Id = 999 }]), Ct);
        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
    }

    [Fact]
    public async Task Relaciones_PrincipalClaveYPrincipalId()
    {
        var e = Crear();
        var cuerpo = Cuerpo(
            [SolP1(), new PrincipalEdicionSolicitud("p3", null, 4, "TIPO_3", new DateOnly(2026, 11, 1), Fin, null)],
            [SolK1(), BackNuevo("k2", 8, "2026-11-02", "2026-11-03", principalClave: "p3"), BackNuevo("k3", 3, "2026-11-04", "2026-11-04", principalId: 92)]);

        // DEV004 (P3 nuevo) sin cruces externos en los dobles; se registra para ver las relaciones.
        var r = await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        var nuevas = e.Repo.Aplicado!.Nuevas.ToDictionary(n => n.Clave);
        Assert.Equal((short)3, nuevas["p3"].Numero);                     // principales 1 y 2 existen
        Assert.Equal(new RelacionPrincipal(null, "p3"), nuevas["k2"].Relacion);
        Assert.Equal(new RelacionPrincipal(92, null), nuevas["k3"].Relacion); // histórico (D3)
        Assert.Equal(new RelacionPrincipal(91, null), e.Repo.Aplicado.Vigentes.Single(v => v.Id == 93).Relacion);
    }

    [Fact]
    public async Task Relaciones_Invalidas_400()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(backs:
        [
            SolK1() with { PrincipalClave = "zz" },
            BackNuevo("k2", 8, "2026-11-02", "2026-11-03", principalId: 91),
            BackNuevo("k3", 3, "2026-11-04", "2026-11-04", principalClave: "p1", principalId: 92),
        ]), Ct);

        Assert.Equal(["El principal relacionado 'zz' no está entre los principales enviados."], r.Errores!["backs[0].principalClave"]);
        Assert.Equal(["El principal 91 no es un principal histórico de este proyecto."], r.Errores["backs[1].principalId"]);
        Assert.Equal(["Indique principalClave o principalId, no ambos."], r.Errores["backs[2].principalClave"]);
    }

    [Fact]
    public async Task ClavesRepetidas_YEmpleadoNuevoInactivo_400()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, Cuerpo(backs: [SolK1(), BackNuevo("k1", 9, "2026-11-02", "2026-11-03")]), Ct);

        Assert.Equal(["La clave 'k1' está repetida."], r.Errores!["backs[1].clave"]);
        Assert.Equal(["El empleado 9 no existe o no está activo."], r.Errores["backs[1].empleadoId"]);
    }

    // ------------------------------------------------------------------ registro (P13–P14)

    [Fact]
    public async Task P13_RegistrarValido_VersionMaxMasUno_YCambioCompleto()
    {
        var e = Crear();
        e.Repo.UltimaVersion = 1;
        var cuerpo = Cuerpo([SolP1("TIPO_2")], [BackNuevo("k2", 8, "2026-09-28", "2026-10-04")]);

        var r = await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);

        Assert.Equal(new PersonalActualizadoDto(ProyectoId, 2), r.Realizado);
        Assert.Equal((1, 1, 0), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
        Assert.Equal(2, e.Repo.Lecturas.Count); // fuera y DENTRO de la transacción

        var c = e.Repo.Aplicado!;
        Assert.Equal((Corte, 2, "ACTUALIZACION_PERSONAL", "DEV.01"), (c.Corte, c.Version, c.TipoMovimiento, c.ActividadCodigo));
        var p1 = Assert.Single(c.Vigentes);
        Assert.Equal((91, (byte?)2, (byte?)11, (byte)4), (p1.Id, p1.JornadaId, p1.DiasTrabajo, p1.DiasDescanso)); // TIPO_2
        Assert.Equal([93], c.Eliminadas);
        var nuevo = Assert.Single(c.Nuevas);
        Assert.Equal(("k2", (short)2, 8), (nuevo.Clave, nuevo.Numero, nuevo.EmpleadoId));
        Assert.Contains(c.Dias, d => d.ClaveNueva == "k2" && d.Dia.Fecha == new DateOnly(2026, 9, 28)); // M1
        Assert.Contains(c.Dias, d => d.PersonalId == 91 && d.Dia.Fecha == new DateOnly(2026, 10, 6));
        Assert.DoesNotContain(c.Dias, d => d.Dia.Fecha < Corte && d.PersonalId is not null); // la base no se reinserta

        Assert.Contains("\"ekon\":\"DEV008\"", c.SnapshotPersonal);
        Assert.Contains("\"jornada\":\"TIPO_2\"", c.SnapshotPersonal);
        Assert.DoesNotContain("\"rol\":\"BACK\",\"ekon\":\"DEV007\"", c.SnapshotPersonal); // K1 eliminado
        Assert.Contains("\"rol\":\"PRINCIPAL\",\"ekon\":\"DEV007\"", c.SnapshotPersonal); // histórico P2 conservado
        Assert.DoesNotContain("cedula", c.SnapshotPersonal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task P14_DentroDeLaTransaccion_ElPersonalCambio_409()
    {
        // Fuera: K1 existe; dentro del applock ya no (otro usuario lo eliminó).
        var sinK1 = Proyecto() with { Personal = [P1, P2] };
        var e = Crear(new RepositorioEdicionFalso(Proyecto(), sinK1));

        // Con un cambio real (P1 a TIPO_2): sin cambios respondería 400 antes de la transacción (C9, TAREA-18).
        var r = await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo([SolP1("TIPO_2")]).ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.Cambiado, r.Estado);
        Assert.Equal("El proyecto cambió; vuelve a cargarlo.", ResultadoEdicionPersonal.MensajeCambiado);
        Assert.Null(e.Repo.Aplicado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task DentroDeLaTransaccion_EstadoCambio_409()
    {
        var e = Crear(new RepositorioEdicionFalso(Proyecto(), Proyecto("SUSPENDIDO")));
        Assert.Equal(EstadoEdicion.Cambiado, (await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo([SolP1("TIPO_2")]).ConVersion(e.Repo), Ct)).Estado); // C9
        Assert.Null(e.Repo.Aplicado);
    }

    [Fact]
    public async Task DentroDeLaTransaccion_AparecenCrucesExternos_409ConCruces()
    {
        AsignacionExistente Dia8(int dia) => new(8, new DateOnly(2026, 10, dia), 1, 5, "PRY-X", "OTRO", "ACTIVO");
        var e = Crear(cruces: new CrucesEdicionFalsos([], [Dia8(3)])); // la 2.ª consulta (dentro) ya tiene el cruce
        var cuerpo = Cuerpo(backs: [SolK1(), BackNuevo("k2", 8, "2026-10-02", "2026-10-04")]);

        var r = await e.Servicio.RegistrarAsync(ProyectoId, cuerpo.ConVersion(e.Repo), Ct);

        Assert.Equal(EstadoEdicion.ConCruces, r.Estado);
        Assert.Null(e.Repo.Aplicado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task ConflictoAlGuardar_409_YRevierte()
    {
        var repo = new RepositorioEdicionFalso(Proyecto()) { LanzarConflicto = true };
        var e = Crear(repo);

        Assert.Equal(EstadoEdicion.Cambiado, (await e.Servicio.RegistrarAsync(ProyectoId, Cuerpo([SolP1("TIPO_2")]).ConVersion(e.Repo), Ct)).Estado); // C9
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    // ------------------------------------------------------------------ CalculadorCruces (nuevas funciones)

    [Fact]
    public void CalculadorCruces_Historicos_YInternosDesdeLista()
    {
        var empleados = new Dictionary<int, EmpleadoAsignable> { [6] = new(6, "DEV006", "EMPLEADO PRUEBA 06", null) };
        var p1 = new App.Domain.Proyectos.Cronograma.PersonaProyecto(App.Domain.Proyectos.Cronograma.RolCronograma.Principal, 1);
        var k2 = new App.Domain.Proyectos.Cronograma.PersonaProyecto(App.Domain.Proyectos.Cronograma.RolCronograma.Back, 2);

        var historico = Assert.Single(CalculadorCruces.Historicos([new(6, new DateOnly(2026, 9, 29), k2, p1)], empleados));
        Assert.Equal(("HISTORICO", "BACK", "MISMO PROYECTO"), (historico.Origen, historico.Rol, historico.Proyecto));

        var internos = CalculadorCruces.Internos([new App.Domain.Proyectos.Cronograma.CruceInterno(6, new DateOnly(2026, 10, 5), [p1, k2])], empleados);
        Assert.Equal(["PRINCIPAL", "BACK"], internos.Select(c => c.Rol));
    }
}
