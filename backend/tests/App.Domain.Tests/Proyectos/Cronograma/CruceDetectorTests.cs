using App.Domain.Proyectos.Cronograma;
using static App.Domain.Tests.Proyectos.Cronograma.Cron;

namespace App.Domain.Tests.Proyectos.Cronograma;

/// <summary>Cruces internos: casos E7–E9 de FASE_5 §5.5 (a través del motor) y reglas de §6 (detector directo).</summary>
public class CruceDetectorTests
{
    private static PrincipalEntrada PrincipalTipo2(int empleadoId) =>
        Principal(1, empleadoId, "2026-10-01", "2026-10-31", 11, 4);

    [Fact]
    public void E7_PrincipalYBackDistintosElMismoDia_SinCruce()
    {
        var r = MotorCronograma.Generar(Solicitud(
            [PrincipalTipo2(empleadoId: 1)],
            [Back(1, 2, "2026-10-05", "2026-10-05", TipoRegistroBack.Jornada, 0)]));

        Assert.Empty(r.CrucesInternos);
        Assert.Contains(new DiaAsignado(1, F("2026-10-05"), Pri, Auto, 1, P1), r.DiasFinales);
        Assert.Contains(new DiaAsignado(2, F("2026-10-05"), Bck, Man, 1, K1), r.DiasFinales);
    }

    [Fact]
    public void E8_MismaPersonaPrincipalYBackElMismoDia_CruceInterno()
    {
        var r = MotorCronograma.Generar(Solicitud(
            [PrincipalTipo2(empleadoId: 1)],
            [Back(1, 1, "2026-10-05", "2026-10-05", TipoRegistroBack.Jornada, 0)]));

        var cruce = Assert.Single(r.CrucesInternos);
        Assert.Equal(1, cruce.EmpleadoId);
        Assert.Equal(F("2026-10-05"), cruce.Fecha);
        Assert.Equal([P1, K1], cruce.Involucrados);
        Assert.True(r.TieneCrucesInternos);

        // Roles distintos: ambos días se conservan en DiasFinales.
        Assert.Contains(new DiaAsignado(1, F("2026-10-05"), Pri, Auto, 1, P1), r.DiasFinales);
        Assert.Contains(new DiaAsignado(1, F("2026-10-05"), Bck, Man, 1, K1), r.DiasFinales);
    }

    [Fact]
    public void E9_MismaPersonaPrincipalYDescansoElMismoDia_SinCruce()
    {
        var r = MotorCronograma.Generar(Solicitud(
            [PrincipalTipo2(empleadoId: 1)],
            [Back(1, 1, "2026-10-05", "2026-10-05", TipoRegistroBack.Descanso, 0)]));

        Assert.Empty(r.CrucesInternos);
        Assert.Contains(new DiaAsignado(1, F("2026-10-05"), Pri, Auto, 1, P1), r.DiasFinales);
        Assert.Contains(new DiaAsignado(1, F("2026-10-05"), Des, Man, 1, K1), r.DiasFinales);
    }

    [Fact]
    public void Detector_IgnoraDescansos_YAgrupaPorPersonaYFecha_OrdenadoPorFechaYEmpleado()
    {
        DiaAsignado[] dias =
        [
            new(2, F("2026-10-06"), Pri, Auto, 1, P1),
            new(2, F("2026-10-06"), Bck, Man, 1, K1),
            new(1, F("2026-10-05"), Pri, Auto, 1, P1),
            new(1, F("2026-10-05"), Bck, Man, 1, K1),
            new(1, F("2026-10-05"), Des, Man, 2, new PersonaProyecto(RolCronograma.Back, 2)), // DESCANSO: no cuenta
            new(3, F("2026-10-05"), Pri, Auto, 1, P2),
            new(3, F("2026-10-05"), Des, Auto, 1, P2),                                       // solo 1 no-descanso: sin cruce
        ];

        var cruces = CruceDetector.DetectarInternos(dias);

        Assert.Collection(cruces,
            c =>
            {
                Assert.Equal((1, F("2026-10-05")), (c.EmpleadoId, c.Fecha));
                Assert.Equal([P1, K1], c.Involucrados);
            },
            c =>
            {
                Assert.Equal((2, F("2026-10-06")), (c.EmpleadoId, c.Fecha));
                Assert.Equal([P1, K1], c.Involucrados);
            });
    }

    [Fact]
    public void Detector_ListaVacia_SinCruces() => Assert.Empty(CruceDetector.DetectarInternos([]));
}
