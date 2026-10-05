using App.Application.Proyectos.Estados;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;

namespace App.Application.Tests.Proyectos.Estados;

/// <summary>Caso de uso con dobles: vista previa, aplicar en la transacción, 409, versión = máx + 1, advertencias.</summary>
public class CambioEstadoServicioTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>01/10/2026 10:00 en Ecuador (la fecha del movimiento, 2027, es futura).</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

    private static readonly CambioEstadoSolicitud Suspender15 = new("SUSPENDIDO", new DateOnly(2027, 3, 15));

    private sealed record Entorno(CambioEstadoServicio Servicio, RepositorioCambioFalso Repo, TransaccionFalsa Tx);

    private static Entorno Crear(RepositorioCambioFalso repo, DateTimeOffset? ahora = null, IUsuarioActual? usuario = null)
    {
        var tx = new TransaccionFalsa();
        var servicio = new CambioEstadoServicio(repo, new CambioEstadoValidador(), tx, usuario ?? new UsuarioFalso(),
            new RelojFijo(ahora ?? Ahora));
        return new Entorno(servicio, repo, tx);
    }

    // ------------------------------------------------------------------ vista previa

    [Fact]
    public async Task Previsualizar_Suspension_ArmaElResumen_SinGuardar()
    {
        var e = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto()));

        var r = await e.Servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);

        Assert.Equal(EstadoCambio.Previsualizado, r.Estado);
        var p = r.Previsualizacion!;
        Assert.Equal(("SUSPENSION", "ACTIVO", "SUSPENDIDO"), (p.Movimiento, p.EstadoActual, p.EstadoNuevo));
        Assert.Equal((new DateOnly(2027, 4, 30), new DateOnly(2027, 3, 15)), (p.FechaFinActual, p.FechaFinNueva));

        var p1 = Assert.Single(p.DiasEliminados, d => d.Empleado.CodigoEkon == "DEV004");
        Assert.Equal(("PRINCIPAL", 46, new DateOnly(2027, 3, 16), new DateOnly(2027, 4, 30)), (p1.Rol, p1.Cantidad, p1.Desde, p1.Hasta));

        var eliminado = Assert.Single(p.PersonalEliminado);
        Assert.Equal(("DEV005", "PRINCIPAL", (short)2), (eliminado.Empleado.CodigoEkon, eliminado.Rol, eliminado.Numero));

        var recortado = Assert.Single(p.PersonalRecortado);
        Assert.Equal(("DEV004", new DateOnly(2027, 4, 30), new DateOnly(2027, 3, 15)),
            (recortado.Empleado.CodigoEkon, recortado.FechaFinAnterior, recortado.FechaFinNueva));

        var actividad = Assert.Single(p.ActividadesAfectadas);
        Assert.Equal(("DEV.01", "RECORTADA", (DateOnly?)new DateOnly(2027, 3, 15)), (actividad.ActividadCodigo, actividad.Accion, actividad.FechaFinNueva));

        // El back 2 cubría a P2 (que se elimina): advertencia (8.3). Sin advertencia de fecha pasada.
        Assert.Equal(["El back 2 (EMPLEADO PRUEBA 06) quedará sin principal relacionado."], p.Advertencias);
        Assert.Equal(0, e.Tx.Iniciadas);
        Assert.Null(e.Repo.Aplicado);
    }

    [Fact]
    public async Task Previsualizar_FechaPasada_AdvierteDiasTranscurridos()
    {
        // Hoy en Ecuador: 20/03/2027 (la fecha del movimiento, 15/03, ya pasó).
        var e = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto()), ahora: new DateTimeOffset(2027, 3, 20, 15, 0, 0, TimeSpan.Zero));

        var r = await e.Servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);

        Assert.Contains(CambioEstadoServicio.AdvertenciaDiasTranscurridos, r.Previsualizacion!.Advertencias);
        Assert.Equal("Se eliminarán días ya transcurridos.", r.Previsualizacion.Advertencias[0]);
    }

    [Fact]
    public async Task Previsualizar_FechaDeHoy_NoAdvierte()
    {
        // Hoy en Ecuador: 15/03/2027 (00:30 UTC del 16/03 = 19:30 del 15/03 en Ecuador).
        var e = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto()), ahora: new DateTimeOffset(2027, 3, 16, 0, 30, 0, TimeSpan.Zero));

        var r = await e.Servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);

        Assert.DoesNotContain(CambioEstadoServicio.AdvertenciaDiasTranscurridos, r.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task Reactivacion_400_SinAbrirTransaccion()
    {
        var e = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto("SUSPENDIDO")));

        var r = await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, new CambioEstadoSolicitud("ACTIVO", new DateOnly(2027, 3, 15)), Ct);

        Assert.Equal(EstadoCambio.Invalido, r.Estado);
        Assert.Equal(["La reactivación se registra con la opción Reactivar."], r.Errores!["estadoDestino"]);
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    [Fact]
    public async Task NoVisibleOInexistente_404()
    {
        var e = Crear(new RepositorioCambioFalso());

        Assert.Equal(EstadoCambio.NoEncontrado, (await e.Servicio.PrevisualizarAsync(99, Suspender15, Ct)).Estado);
        Assert.Equal(EstadoCambio.NoEncontrado, (await e.Servicio.AplicarAsync(99, Suspender15, Ct)).Estado);
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    [Fact]
    public async Task Visibilidad_GestorFiltraPorPropietario_AdminNo()
    {
        var gestor = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto()));
        await gestor.Servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);
        Assert.Equal(3, gestor.Repo.Lecturas[0].Propietario);

        var admin = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto()), usuario: new AdminFalso());
        await admin.Servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);
        Assert.Null(admin.Repo.Lecturas[0].Propietario);
    }

    [Fact]
    public async Task SinFecha_400_LeeSinDias()
    {
        var e = Crear(new RepositorioCambioFalso(DoblesEstados.Proyecto()));

        var r = await e.Servicio.PrevisualizarAsync(DoblesEstados.ProyectoId, new CambioEstadoSolicitud("SUSPENDIDO", null), Ct);

        Assert.Equal(["La fecha del movimiento es obligatoria."], r.Errores!["fecha"]);
        Assert.Equal(DateOnly.MaxValue, e.Repo.Lecturas[0].Fecha);
    }

    // ------------------------------------------------------------------ aplicar

    [Fact]
    public async Task Aplicar_Suspension_VersionMaxMasUno_EnTransaccionConfirmada()
    {
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto()) { UltimaVersion = 3 };
        var e = Crear(repo);

        var r = await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);

        Assert.Equal(EstadoCambio.Realizado, r.Estado);
        Assert.Equal(new CambioEstadoRealizadoDto(DoblesEstados.ProyectoId, "SUSPENDIDO", 4), r.Realizado);
        Assert.Equal((1, 1, 0), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
        Assert.Equal(2, repo.Lecturas.Count); // fuera y DENTRO de la transacción

        var a = repo.Aplicado!;
        Assert.Equal((4, "SUSPENDIDO", "SUSPENSION", DoblesEstados.Inicio), (a.Version, a.EstadoDestino, a.TipoMovimiento, a.FechaInicioProyecto));
        Assert.Equal(new DateOnly(2027, 3, 15), a.Plan.Fecha);
        Assert.Equal([72], a.Plan.PersonalEliminado);
        Assert.Equal([73], a.Plan.BacksSinPrincipal);
        Assert.Equal("DEV.01", a.Plan.ActividadVigenteEnF);

        // Snapshot del personal RESULTANTE: sin DEV005 (eliminado), DEV004 hasta el 15/03; sin cédula ni correo.
        Assert.Contains("\"ekon\":\"DEV004\"", a.SnapshotPersonal);
        Assert.Contains("\"fechaFin\":\"2027-03-15\"", a.SnapshotPersonal);
        Assert.DoesNotContain("DEV005", a.SnapshotPersonal);
        Assert.DoesNotContain("cedula", a.SnapshotPersonal, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo", a.SnapshotPersonal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Aplicar_Cierre_DesdeSuspendido()
    {
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto("SUSPENDIDO"));
        var e = Crear(repo);

        var r = await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, new CambioEstadoSolicitud("TERMINADO", new DateOnly(2027, 3, 15)), Ct);

        Assert.Equal(new CambioEstadoRealizadoDto(DoblesEstados.ProyectoId, "TERMINADO", 2), r.Realizado);
        Assert.Equal("CIERRE", repo.Aplicado!.TipoMovimiento);
    }

    [Fact]
    public async Task Aplicar_EstadoCambioDentroDeLaTransaccion_409_YRevierte()
    {
        // Fuera: ACTIVO; dentro del applock otro usuario ya lo suspendió.
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto("ACTIVO"), DoblesEstados.Proyecto("SUSPENDIDO"));
        var e = Crear(repo);

        var r = await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);

        Assert.Equal(EstadoCambio.Conflicto, r.Estado);
        Assert.Equal("El proyecto cambió de estado; vuelve a cargarlo.", ResultadoCambioEstado.MensajeConflicto);
        Assert.Null(repo.Aplicado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task Aplicar_FechaFinCambioDentroDeLaTransaccion_409()
    {
        // Dentro del applock la fecha fin ya es el 10/03: la fecha 15/03 dejó de ser válida.
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto(), DoblesEstados.Proyecto(fin: new DateOnly(2027, 3, 10)));
        var e = Crear(repo);

        Assert.Equal(EstadoCambio.Conflicto, (await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15, Ct)).Estado);
        Assert.Null(repo.Aplicado);
    }

    [Fact]
    public async Task Aplicar_ConcurrenciaAlGuardar_409_YRevierte()
    {
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto()) { LanzarConflicto = true };
        var e = Crear(repo);

        var r = await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15, Ct);

        Assert.Equal(EstadoCambio.Conflicto, r.Estado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }

    [Fact]
    public async Task Aplicar_EliminadoDentroDeLaTransaccion_404()
    {
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto(), null);
        var e = Crear(repo);

        Assert.Equal(EstadoCambio.NoEncontrado, (await e.Servicio.AplicarAsync(DoblesEstados.ProyectoId, Suspender15, Ct)).Estado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
    }
}
