using App.Domain.Proyectos.Cronograma;
using static App.Domain.Tests.Proyectos.Cronograma.Cron;

namespace App.Domain.Tests.Proyectos.Cronograma;

/// <summary>Casos E1–E6, E10 y bordes B1–B7 de FASE_5 §5.5 (año 2026; proyecto 01/10–31/10 salvo indicación).</summary>
public class MotorCronogramaTests
{
    // Jornadas del catálogo (RN03)
    private const byte Tipo1Trabajo = 22, Tipo1Descanso = 8;
    private const byte Tipo2Trabajo = 11, Tipo2Descanso = 4;
    private const byte Tipo3Trabajo = 5, Tipo3Descanso = 2;
    private const byte EspecialTrabajo = 3, EspecialDescanso = 0;

    // ------------------------------------------------------------------ E1–E6, E10

    [Fact]
    public void E1_PrincipalTipo2_TresBloquesYDescansosAutomaticos_SinDescansoFueraDeSuFecha()
    {
        var r = MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso)]));

        Tramo[] principales =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-11"),
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
        ];
        Tramo[] descansos =
        [
            T(Des, Auto, 1, P1, 1, "2026-10-12", "2026-10-15"),
            T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30"),
        ];

        Assert.Equal([.. principales, .. descansos], r.Tramos);
        Assert.Equal(Dias(principales), r.DiasBase);
        Assert.Equal(Dias([.. principales, .. descansos]), r.DiasFinales);
        Assert.DoesNotContain(r.DiasFinales, d => d.Fecha == F("2026-11-01"));
        Assert.Empty(r.CrucesInternos);
    }

    [Fact]
    public void E2_PrincipalTipo1_UnBloqueRecortado_SinDescanso()
    {
        var r = MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-01", "2026-10-10", Tipo1Trabajo, Tipo1Descanso)]));

        var bloque = T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-10");
        Assert.Equal([bloque], r.Tramos);
        Assert.Equal(Dias(bloque), r.DiasFinales);
        Assert.Empty(r.CrucesInternos);
    }

    [Fact]
    public void E3_PrincipalEspecial_BloquesDeTresDias_SinDescansos()
    {
        var r = MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-01", "2026-10-07", EspecialTrabajo, EspecialDescanso)]));

        Tramo[] esperados =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-03"),
            T(Pri, Auto, 2, P1, 1, "2026-10-04", "2026-10-06"),
            T(Pri, Auto, 3, P1, 1, "2026-10-07", "2026-10-07"),
        ];
        Assert.Equal(esperados, r.Tramos);
        Assert.Equal(Dias(esperados), r.DiasFinales);
        Assert.DoesNotContain(r.DiasFinales, d => d.Rol == Des);
    }

    [Fact]
    public void E4_BackJornada_ConDescansoPosteriorManual()
    {
        var r = MotorCronograma.Generar(Solicitud(backs: [Back(1, 3, "2026-10-12", "2026-10-15", TipoRegistroBack.Jornada, 2)]));

        Tramo[] esperados =
        [
            T(Bck, Man, 1, K1, 3, "2026-10-12", "2026-10-15"),
            T(Des, Man, 1, K1, 3, "2026-10-16", "2026-10-17"),
        ];
        Assert.Equal(esperados, r.Tramos);
        Assert.Equal(Dias(esperados), r.DiasBase);
        Assert.Equal(Dias(esperados), r.DiasFinales);
    }

    [Fact]
    public void E5_BackDescanso_SinDescansoPosterior_AunqueTengaDiasDescanso()
    {
        var r = MotorCronograma.Generar(Solicitud(backs: [Back(1, 3, "2026-10-12", "2026-10-15", TipoRegistroBack.Descanso, 2)]));

        var tramo = T(Des, Man, 1, K1, 3, "2026-10-12", "2026-10-15");
        Assert.Equal([tramo], r.Tramos);
        Assert.Equal(Dias(tramo), r.DiasFinales);
        Assert.DoesNotContain(r.DiasFinales, d => d.Fecha > F("2026-10-15"));
    }

    [Fact]
    public void E6_MismaPersonaComoBackElDiaSiguienteAlBloque_NoGeneraDescansoAutomaticoDeEseBloque()
    {
        var r = MotorCronograma.Generar(Solicitud(
            [Principal(1, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso)],
            [Back(1, 1, "2026-10-12", "2026-10-12", TipoRegistroBack.Jornada, 0)]));

        Tramo[] base_ =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-11"),
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
            T(Bck, Man, 1, K1, 1, "2026-10-12", "2026-10-12"),
        ];
        var descansoBloque2 = T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30");

        Assert.Equal([.. base_, descansoBloque2], r.Tramos);
        Assert.Equal(Dias([.. base_, descansoBloque2]), r.DiasFinales);
        // Solo se revisa el primer día: como el 12 está ocupado, tampoco hay descanso el 13, 14 ni 15 (P4).
        Assert.DoesNotContain(r.DiasFinales, d => d.Rol == Des && d.Fecha >= F("2026-10-12") && d.Fecha <= F("2026-10-15"));
        Assert.Empty(r.CrucesInternos);
    }

    [Fact]
    public void E10_RangoDeUnDia_UnBloqueDeUnDia()
    {
        var r = MotorCronograma.Generar(Solicitud(
            [Principal(1, 1, "2026-10-01", "2026-10-01", Tipo2Trabajo, Tipo2Descanso)], inicio: "2026-10-01", fin: "2026-10-01"));

        var bloque = T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-01");
        Assert.Equal([bloque], r.Tramos);
        Assert.Equal(Dias(bloque), r.DiasFinales);
        Assert.Equal(1, r.Tramos[0].Dias);
    }

    // ------------------------------------------------------------------ Bordes B1–B7

    [Fact]
    public void B1_RangoMultiploExactoDelCiclo_UltimoDescansoTerminaJustoEnLaFechaFin()
    {
        var r = MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-05", "2026-10-18", Tipo3Trabajo, Tipo3Descanso)]));

        Tramo[] principales =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-05", "2026-10-09"),
            T(Pri, Auto, 2, P1, 1, "2026-10-12", "2026-10-16"),
        ];
        Tramo[] descansos =
        [
            T(Des, Auto, 1, P1, 1, "2026-10-10", "2026-10-11"),
            T(Des, Auto, 2, P1, 1, "2026-10-17", "2026-10-18"),
        ];
        Assert.Equal([.. principales, .. descansos], r.Tramos);
        Assert.Equal(Dias([.. principales, .. descansos]), r.DiasFinales);
        Assert.Equal(14, r.DiasFinales.Count);
    }

    [Fact]
    public void B2_DosPrincipalesDistintos_CadaUnoConSuCicloYSinCruces()
    {
        var r = MotorCronograma.Generar(Solicitud(
        [
            Principal(1, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso),
            Principal(2, 2, "2026-10-01", "2026-10-14", Tipo3Trabajo, Tipo3Descanso),
        ]));

        Tramo[] principales =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-11"),
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
            T(Pri, Auto, 1, P2, 2, "2026-10-01", "2026-10-05"),
            T(Pri, Auto, 2, P2, 2, "2026-10-08", "2026-10-12"),
        ];
        Tramo[] descansos =
        [
            T(Des, Auto, 1, P1, 1, "2026-10-12", "2026-10-15"),
            T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30"),
            T(Des, Auto, 1, P2, 2, "2026-10-06", "2026-10-07"),
            T(Des, Auto, 2, P2, 2, "2026-10-13", "2026-10-14"),
        ];
        Assert.Equal([.. principales, .. descansos], r.Tramos);
        Assert.Equal(Dias([.. principales, .. descansos]), r.DiasFinales);
        Assert.Empty(r.CrucesInternos);
    }

    [Fact]
    public void B3_DescansoAutomaticoQueCoincideConDescansoDelBackDeLaMismaPersona_UnaSolaFilaPorDia()
    {
        var r = MotorCronograma.Generar(Solicitud(
            [Principal(1, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso)],
            [Back(1, 1, "2026-10-14", "2026-10-15", TipoRegistroBack.Descanso, 0)]));

        Tramo[] principales =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-11"),
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
        ];
        var descansoBack = T(Des, Man, 1, K1, 1, "2026-10-14", "2026-10-15");
        // El 12 está libre → se genera el descanso automático 12–15, pero 14–15 ya existen (mismo empleado, fecha y rol).
        var autoBloque1 = T(Des, Auto, 1, P1, 1, "2026-10-12", "2026-10-15");
        var autoBloque2 = T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30");

        Assert.Equal([.. principales, descansoBack, autoBloque1, autoBloque2], r.Tramos);

        List<DiaAsignado> esperados =
        [
            .. Dias(principales),
            .. Dias(descansoBack),                                   // 14 y 15: se conserva el del back (MANUAL)
            .. D(1, "2026-10-12", "2026-10-13", Des, Auto, 1, P1),   // solo 12 y 13 del automático
            .. Dias(autoBloque2),
        ];
        Assert.Equal(esperados, r.DiasFinales);
        Assert.Single(r.DiasFinales, d => d.Fecha == F("2026-10-14") && d.Rol == Des);
        Assert.Single(r.DiasFinales, d => d.Fecha == F("2026-10-15") && d.Rol == Des);
        Assert.Empty(r.CrucesInternos);
    }

    [Fact]
    public void B4_DescansoPosteriorDelBackQuePasaElFinDelProyecto_SeConservaCompleto()
    {
        var r = MotorCronograma.Generar(Solicitud(backs: [Back(1, 3, "2026-10-28", "2026-10-31", TipoRegistroBack.Jornada, 3)]));

        Tramo[] esperados =
        [
            T(Bck, Man, 1, K1, 3, "2026-10-28", "2026-10-31"),
            T(Des, Man, 1, K1, 3, "2026-11-01", "2026-11-03"), // P3: no se recorta al 31/10
        ];
        Assert.Equal(esperados, r.Tramos);
        Assert.Equal(Dias(esperados), r.DiasFinales);
    }

    [Fact]
    public void B5_FinDelProyectoAnteriorAlInicio_LanzaArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            MotorCronograma.Generar(Solicitud(inicio: "2026-10-31", fin: "2026-10-01")));

    [Fact]
    public void B5_FinDelPrincipalAnteriorAlInicio_LanzaArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-10", "2026-10-09", Tipo2Trabajo, Tipo2Descanso)])));

    [Fact]
    public void B5_FinDelBackAnteriorAlInicio_LanzaArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            MotorCronograma.Generar(Solicitud(backs: [Back(1, 3, "2026-10-15", "2026-10-12", TipoRegistroBack.Jornada, 0)])));

    [Fact]
    public void B5_PrincipalConDiasTrabajoCero_LanzaArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-01", "2026-10-31", 0, 4)])));

    [Fact]
    public void B5_SolicitudNula_LanzaArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => MotorCronograma.Generar(null!));

    [Fact]
    public void B6a_PrincipalSinBacks_SoloDiasDelPrincipal()
    {
        var r = MotorCronograma.Generar(Solicitud([Principal(1, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso)], backs: []));

        Assert.All(r.Tramos, t => Assert.Equal(P1, t.Persona));
        Assert.All(r.DiasFinales, d => Assert.Equal(P1, d.Persona));
        Assert.Equal(31, r.DiasFinales.Count); // 01–31 cubierto: 23 días PRINCIPAL + 8 de DESCANSO automático
        Assert.Empty(r.CrucesInternos);
    }

    [Fact]
    public void B6b_ProyectoSinPrincipalesNiBacks_ResultadoVacioSinError()
    {
        var r = MotorCronograma.Generar(Solicitud());

        Assert.Empty(r.Tramos);
        Assert.Empty(r.DiasBase);
        Assert.Empty(r.DiasFinales);
        Assert.Empty(r.CrucesInternos);
        Assert.False(r.TieneCrucesInternos);
    }

    [Fact]
    public void B6c_ProyectoSinPrincipalesConUnBack_SoloDiasDelBack()
    {
        var r = MotorCronograma.Generar(Solicitud(backs: [Back(1, 3, "2026-10-05", "2026-10-06", TipoRegistroBack.Jornada, 0)]));

        var tramo = T(Bck, Man, 1, K1, 3, "2026-10-05", "2026-10-06");
        Assert.Equal([tramo], r.Tramos);
        Assert.Equal(Dias(tramo), r.DiasFinales);
    }

    [Fact]
    public void B7_MismaPersonaComoDosPrincipales_CruceCadaDiaDeTrabajo_YUnaSolaFilaPorDiaEnDiasFinales()
    {
        var r = MotorCronograma.Generar(Solicitud(
        [
            Principal(1, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso),
            Principal(2, 1, "2026-10-01", "2026-10-31", Tipo2Trabajo, Tipo2Descanso),
        ]));

        Tramo[] p1 =
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-11"),
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
        ];
        Tramo[] descansosP1 =
        [
            T(Des, Auto, 1, P1, 1, "2026-10-12", "2026-10-15"),
            T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30"),
        ];

        Tramo[] p2 =
        [
            T(Pri, Auto, 1, P2, 1, "2026-10-01", "2026-10-11"),
            T(Pri, Auto, 2, P2, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P2, 1, "2026-10-31", "2026-10-31"),
        ];
        Tramo[] descansosP2 =
        [
            T(Des, Auto, 1, P2, 1, "2026-10-12", "2026-10-15"),
            T(Des, Auto, 2, P2, 1, "2026-10-27", "2026-10-30"),
        ];
        Assert.Equal([.. p1, .. p2, .. descansosP1, .. descansosP2], r.Tramos);

        // DiasBase conserva los duplicados (D1): 23 días × 2 principales.
        Assert.Equal(46, r.DiasBase.Count);
        // DiasFinales: se conserva el primero (P1) para cada (empleado, fecha, rol).
        Assert.Equal(Dias([.. p1, .. descansosP1]), r.DiasFinales);

        // Cruce en cada uno de los 23 días PRINCIPAL, con P1 y P2 involucrados.
        var diasTrabajo = Dias(p1).Select(d => d.Fecha).ToList();
        Assert.Equal(diasTrabajo, r.CrucesInternos.Select(c => c.Fecha));
        Assert.All(r.CrucesInternos, c =>
        {
            Assert.Equal(1, c.EmpleadoId);
            Assert.Equal([P1, P2], c.Involucrados);
        });
    }
}
