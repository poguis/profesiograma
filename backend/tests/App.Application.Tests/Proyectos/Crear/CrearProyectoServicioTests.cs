using App.Application.Proyectos.Crear;

namespace App.Application.Tests.Proyectos.Crear;

/// <summary>Caso de uso con dobles: registrar, cruces internos/externos (también dentro de la transacción), código RN01.</summary>
public class CrearProyectoServicioTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>01/12/2026 10:00 en Ecuador.</summary>
    private static readonly DateTimeOffset Ahora = new(2026, 12, 1, 15, 0, 0, TimeSpan.Zero);

    private sealed record Entorno(CrearProyectoServicio Servicio, CrucesExternosFalsos Cruces, RepositorioFalso Repo, TransaccionFalsa Tx);

    private static Entorno Crear(CrucesExternosFalsos? cruces = null, RepositorioFalso? repo = null, DateTimeOffset? ahora = null)
    {
        cruces ??= new CrucesExternosFalsos();
        repo ??= new RepositorioFalso();
        var tx = new TransaccionFalsa();
        var validador = new CrearProyectoValidador(new DatosFalsos(), new ErpFalso(), new UsuarioFalso(), new EmpleadosErpFalsos());
        return new Entorno(new CrearProyectoServicio(validador, cruces, repo, tx, new RelojFijo(ahora ?? Ahora)), cruces, repo, tx);
    }

    private static AsignacionExistente Existente(int empleadoId, int dia) =>
        new(empleadoId, new DateOnly(2026, 12, dia), 1, 1, "PRY-DEV-0001", "PROYECTO PRUEBA CAMPO", "ACTIVO");

    // ------------------------------------------------------------------ registrar

    [Fact]
    public async Task SinCruces_Registra_EnUnaTransaccionConfirmada()
    {
        var e = Crear();

        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct);

        Assert.Equal(EstadoCrearProyecto.Creado, r.Estado);
        Assert.Equal(99, r.Creado!.Id);
        Assert.Matches("^PRY-20261201-[0-9a-f]{6}$", r.Creado.Codigo);
        Assert.Equal((1, 1, 0), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
        Assert.Equal(2, e.Cruces.Llamadas); // fuera y DENTRO de la transacción (FASE_5 §8.5)

        var (nuevo, codigo, uid) = e.Repo.Agregado!.Value;
        Assert.Equal(r.Creado.Codigo, codigo);
        Assert.EndsWith(uid.ToString("N")[^6..], codigo);
        Assert.Equal("PROYECTO ERP DE PRUEBA", nuevo.NombreVisual);
        Assert.Equal("DEV.01", nuevo.Datos.Actividad!.Id);
        // DiasFinales del motor: P1 TIPO_2 (01–31 dic, 31 días), P2 TIPO_3 (31 días), back 12–15 + descanso 16–17 (6 días).
        Assert.Equal(31 + 31 + 6, nuevo.Dias.Count);
        Assert.Contains("\"ekon\":\"DEV006\"", nuevo.SnapshotPersonal);
        Assert.Contains("\"rol\":\"BACK\"", nuevo.SnapshotPersonal);
        Assert.DoesNotContain("cedula", nuevo.SnapshotPersonal, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo", nuevo.SnapshotPersonal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Planta_NombreVisualConDimension_YSinActividad()
    {
        var e = Crear();
        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudPlanta(), Ct);

        Assert.Equal(EstadoCrearProyecto.Creado, r.Estado);
        Assert.Equal("PLANTA - PLANTA DE PRUEBA", e.Repo.Agregado!.Value.Proyecto.NombreVisual);
        Assert.Null(e.Repo.Agregado.Value.Proyecto.Datos.Actividad);
    }

    [Fact]
    public async Task CodigoUsaLaFechaDeEcuador_NoLaUtc()
    {
        // 02/12/2026 03:00 UTC = 01/12/2026 22:00 en Ecuador.
        var e = Crear(ahora: new DateTimeOffset(2026, 12, 2, 3, 0, 0, TimeSpan.Zero));
        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct);
        Assert.StartsWith("PRY-20261201-", r.Creado!.Codigo);
    }

    [Fact]
    public async Task CruceInterno_NoRegistra_NiAbreTransaccion()
    {
        var e = Crear();
        var s = Dobles.SolicitudCampo();
        // El back es la misma persona que el principal 1 (emp 6) en días de trabajo de este.
        var r = await e.Servicio.RegistrarAsync(s with { Backs = [s.Backs![0] with { EmpleadoId = 6, FechaInicio = new DateOnly(2026, 12, 2), FechaFin = new DateOnly(2026, 12, 3) }] }, Ct);

        Assert.Equal(EstadoCrearProyecto.Conflicto, r.Estado);
        Assert.All(r.Previsualizacion!.Cruces, c => Assert.Equal(CalculadorCruces.OrigenInterno, c.Origen));
        Assert.Contains(r.Previsualizacion.Cruces, c => c.Fecha == new DateOnly(2026, 12, 2) && c.Rol == "PRINCIPAL" && c.Proyecto == "MISMO PROYECTO");
        Assert.Contains(r.Previsualizacion.Cruces, c => c.Fecha == new DateOnly(2026, 12, 2) && c.Rol == "BACK");
        Assert.Equal(0, e.Tx.Iniciadas);
        Assert.Null(e.Repo.Agregado);
    }

    [Fact]
    public async Task CruceExterno_Conflicto_ConCodigoNombreYEstadoDelExistente()
    {
        var e = Crear(new CrucesExternosFalsos([Existente(6, 5), Existente(6, 6)]));

        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct);

        Assert.Equal(EstadoCrearProyecto.Conflicto, r.Estado);
        Assert.Equal(
            [
                new CruceDto("EXTERNO", 6, "DEV006", "EMPLEADO PRUEBA 06", new DateOnly(2026, 12, 5), "PRINCIPAL", "PROYECTO PRUEBA CAMPO", "PRY-DEV-0001", "ACTIVO"),
                new CruceDto("EXTERNO", 6, "DEV006", "EMPLEADO PRUEBA 06", new DateOnly(2026, 12, 6), "PRINCIPAL", "PROYECTO PRUEBA CAMPO", "PRY-DEV-0001", "ACTIVO"),
            ],
            r.Previsualizacion!.Cruces);
        Assert.Equal([new ResumenCruceDto("EMPLEADO PRUEBA 06", "PRINCIPAL", "PRY-DEV-0001 · PROYECTO PRUEBA CAMPO", "diciembre 2026", "5, 6")],
            r.Previsualizacion.Resumen);
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    [Fact]
    public async Task CruceExterno_EnDiaDeDescansoDelNuevo_NoEsCruce()
    {
        // El principal 1 (TIPO_2) descansa del 12 al 15/12 (DESCANSO/AUTO): ese día no cuenta.
        var e = Crear(new CrucesExternosFalsos([Existente(6, 13)]));
        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct);
        Assert.Equal(EstadoCrearProyecto.Creado, r.Estado);
    }

    [Fact]
    public async Task CruceExternoQueApareceDentroDeLaTransaccion_Conflicto_YRollback()
    {
        // 1.ª consulta (fuera): sin cruces. 2.ª (dentro, tras el applock): otro usuario registró el día 5.
        var e = Crear(new CrucesExternosFalsos([], [Existente(7, 5)]));

        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct);

        Assert.Equal(EstadoCrearProyecto.Conflicto, r.Estado);
        Assert.Equal((1, 0, 1), (e.Tx.Iniciadas, e.Tx.Confirmadas, e.Tx.Revertidas));
        Assert.Null(e.Repo.Agregado);
        Assert.Empty(e.Repo.CodigosConsultados);
        Assert.Single(r.Previsualizacion!.Cruces);
    }

    [Fact]
    public async Task CodigoRepetido_SeReintentaConOtroUid()
    {
        var e = Crear(repo: new RepositorioFalso(true, true, false));

        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct);

        Assert.Equal(EstadoCrearProyecto.Creado, r.Estado);
        Assert.Equal(3, e.Repo.CodigosConsultados.Count);
        Assert.Equal(3, e.Repo.CodigosConsultados.Distinct().Count());
        Assert.Equal(e.Repo.CodigosConsultados[2], r.Creado!.Codigo);
    }

    [Fact]
    public async Task CodigoRepetidoTresVeces_Error_YRollback()
    {
        var e = Crear(repo: new RepositorioFalso(true, true, true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.Servicio.RegistrarAsync(Dobles.SolicitudCampo(), Ct));

        Assert.Equal(CrearProyectoServicio.IntentosCodigo, e.Repo.CodigosConsultados.Count);
        Assert.Equal(1, e.Tx.Revertidas);
        Assert.Null(e.Repo.Agregado);
    }

    [Fact]
    public async Task Invalido_NoCalculaNiGuarda()
    {
        var e = Crear();
        var r = await e.Servicio.RegistrarAsync(Dobles.SolicitudCampo() with { CompaniaId = null }, Ct);

        Assert.Equal(EstadoCrearProyecto.Invalido, r.Estado);
        Assert.Equal(0, e.Cruces.Llamadas);
        Assert.Equal(0, e.Tx.Iniciadas);
    }

    // ------------------------------------------------------------------ previsualizar

    [Fact]
    public async Task Previsualizar_NoGuarda_YDevuelveTramosDiasYCruces()
    {
        var e = Crear(new CrucesExternosFalsos([Existente(8, 13)]));

        var r = await e.Servicio.PrevisualizarAsync(Dobles.SolicitudCampo(), Ct);

        Assert.Equal(EstadoCrearProyecto.Previsualizado, r.Estado);
        Assert.Equal(0, e.Tx.Iniciadas);
        Assert.Null(e.Repo.Agregado);

        var p = r.Previsualizacion!;
        Assert.Contains(p.Tramos, t => t is { Rol: "PRINCIPAL", Tipo: "AUTO", Bloque: 1, CodigoEkon: "DEV006" }
                                      && t.Inicio == new DateOnly(2026, 12, 1) && t.Fin == new DateOnly(2026, 12, 11));
        Assert.Contains(p.Tramos, t => t is { Rol: "DESCANSO", Tipo: "AUTO", CodigoEkon: "DEV006" } && t.Inicio == new DateOnly(2026, 12, 12));
        Assert.Contains(p.Tramos, t => t is { Rol: "DESCANSO", Tipo: "MANUAL", CodigoEkon: "DEV008" } && t.Inicio == new DateOnly(2026, 12, 16));
        Assert.Equal(["PRINCIPAL 1", "PRINCIPAL 2", "BACK 1"], p.DiasPorPersona.Select(x => $"{x.Persona.Rol} {x.Persona.Numero}"));
        Assert.Equal(6, p.DiasPorPersona[2].Dias.Count);
        Assert.True(p.DiasPorPersona[0].Dias.SequenceEqual(p.DiasPorPersona[0].Dias.OrderBy(d => d.Fecha)));
        var cruce = Assert.Single(p.Cruces); // el back (emp 8) trabaja el 13/12
        Assert.Equal(("EXTERNO", "BACK", "PRY-DEV-0001"), (cruce.Origen, cruce.Rol, cruce.ProyectoCodigo));
        Assert.Equal("13", Assert.Single(p.Resumen).Dias);
    }

    [Fact]
    public async Task Previsualizar_Invalido_DevuelveErrores()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(Dobles.SolicitudCampo() with { SalidaAlmuerzo = "10:00" }, Ct);
        Assert.Equal(EstadoCrearProyecto.Invalido, r.Estado);
        Assert.True(r.Errores!.ContainsKey("salidaAlmuerzo"));
    }
}
