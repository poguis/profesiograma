using App.Domain.Proyectos.Cabecera;
using App.Domain.Proyectos.Estados;

namespace App.Domain.Tests.Proyectos.Cabecera;

/// <summary>Reglas puras de actividades de la edición de cabecera (TAREA-18): P1, C3/H14 y C6.</summary>
public class ActividadesCabeceraTests
{
    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture);

    private static ActividadCorte A(int id, int version, string codigo, string inicio, string fin) => new(id, version, codigo, D(inicio), D(fin));

    // ------------------------------------------------------------------ P1: mover el inicio

    [Fact]
    public void MoverInicio_Atrasar_LaActividadDelInicioPasaAlNuevo()
    {
        var r = MovimientoInicioProyecto.Calcular([A(1, 1, "DEV.01", "2026-11-01", "2026-12-31")], D("2026-11-01"), D("2026-11-10"));

        Assert.False(r.TerminanAntes);
        Assert.Equal([A(1, 1, "DEV.01", "2026-11-10", "2026-12-31")], r.Actividades);
    }

    [Fact]
    public void MoverInicio_Adelantar_TambienSeMueve_YLasDemasNo()
    {
        var r = MovimientoInicioProyecto.Calcular(
            [A(1, 1, "DEV.01", "2026-11-01", "2026-11-30"), A(2, 2, "DEV.02", "2026-12-01", "2026-12-31")], D("2026-11-01"), D("2026-10-25"));

        Assert.Equal([A(1, 1, "DEV.01", "2026-10-25", "2026-11-30"), A(2, 2, "DEV.02", "2026-12-01", "2026-12-31")], r.Actividades);
    }

    [Fact]
    public void MoverInicio_ActividadQueTerminaAntesDelNuevoInicio_TerminanAntes()
    {
        var r = MovimientoInicioProyecto.Calcular(
            [A(1, 1, "DEV.01", "2026-11-01", "2026-11-05"), A(2, 2, "DEV.02", "2026-11-06", "2026-12-31")], D("2026-11-01"), D("2026-11-10"));

        Assert.True(r.TerminanAntes);
    }

    // ------------------------------------------------------------------ C3 / H14: ampliar

    [Fact]
    public void Ampliar_ExtiendeSoloLasQueTerminabanEnElFinAnterior()
    {
        var r = AmpliacionProyecto.Calcular(
            [A(1, 1, "DEV.01", "2026-11-01", "2026-11-15"), A(2, 2, "DEV.02", "2026-11-16", "2026-11-30")], D("2026-11-30"), D("2026-12-20"));

        Assert.Equal([A(1, 1, "DEV.01", "2026-11-01", "2026-11-15"), A(2, 2, "DEV.02", "2026-11-16", "2026-12-20")], r);
    }

    [Fact]
    public void Ampliar_ConFinMenor_Lanza() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AmpliacionProyecto.Calcular([], D("2026-11-30"), D("2026-11-29")));

    // ------------------------------------------------------------------ C6: cambio de actividad

    [Fact]
    public void CambioActividad_RecortaLaAnterior_YCreaLaNueva_VersionMaxMasUno()
    {
        var r = CambioActividad.Calcular([A(1, 1, "DEV.01", "2026-11-01", "2026-12-31")], "DEV.02", D("2026-11-20"), D("2026-12-31"), 1);

        Assert.False(r.MismaActividad);
        Assert.Equal([A(1, 1, "DEV.01", "2026-11-01", "2026-11-19")], r.Actividades);
        Assert.Equal([new CambioFechaFin(1, D("2026-12-31"), D("2026-11-19"))], r.Recortadas);
        Assert.Empty(r.Eliminadas);
        Assert.Equal(new ActividadNuevaCabecera(2, "DEV.02", D("2026-11-20"), D("2026-12-31")), r.Nueva);
    }

    [Fact]
    public void CambioActividad_EliminaLasQueEmpiezanEnDesdeODespues()
    {
        var r = CambioActividad.Calcular(
            [A(1, 1, "DEV.01", "2026-11-01", "2026-11-19"), A(2, 2, "DEV.02", "2026-11-20", "2026-12-10"), A(3, 3, "DEV.01", "2026-12-11", "2026-12-31")],
            "DEV.03", D("2026-11-20"), D("2026-12-31"), 3);

        Assert.Equal([2, 3], r.Eliminadas);
        Assert.Empty(r.Recortadas); // la 1 termina el 19, antes de "desde"
        Assert.Equal([A(1, 1, "DEV.01", "2026-11-01", "2026-11-19")], r.Actividades);
        Assert.Equal(4, r.Nueva!.Version);
    }

    [Fact]
    public void CambioActividad_DesdeEnElInicioDeUnaActividad_LaElimina()
    {
        var r = CambioActividad.Calcular([A(1, 1, "DEV.01", "2026-11-01", "2026-12-31")], "DEV.02", D("2026-11-01"), D("2026-12-31"), 1);

        Assert.Equal([1], r.Eliminadas);
        Assert.Empty(r.Actividades);
        Assert.Equal(new ActividadNuevaCabecera(2, "DEV.02", D("2026-11-01"), D("2026-12-31")), r.Nueva);
    }

    [Fact]
    public void CambioActividad_MismaQueLaVigenteEnDesde_SinCambio()
    {
        var actividades = new[] { A(1, 1, "DEV.01", "2026-11-01", "2026-12-31") };
        var r = CambioActividad.Calcular(actividades, "dev.01", D("2026-11-20"), D("2026-12-31"), 1);

        Assert.True(r.MismaActividad);
        Assert.Null(r.Nueva);
        Assert.Equal(actividades, r.Actividades);
    }

    [Fact]
    public void CambioActividad_ConHueco_NoRecortaLaQueTerminaAntes_YVersionSobreTodas()
    {
        // versionMaxima incluye actividades ya eliminadas antes (p. ej. por el recorte de la fecha fin).
        var r = CambioActividad.Calcular([A(1, 1, "DEV.01", "2026-11-01", "2026-11-10")], "DEV.02", D("2026-11-20"), D("2026-11-30"), 5);

        Assert.Empty(r.Recortadas);
        Assert.Equal(6, r.Nueva!.Version);
    }
}
