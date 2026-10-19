using App.Application.Cronograma;
using App.Domain.Catalogos;
using static App.Application.Tests.Cronograma.DoblesCronograma;

namespace App.Application.Tests.Cronograma;

/// <summary>TAREA-20: precedencia (RN14 + D1, D2, D3, D9), respuesta dispersa (D7) y personas (D4).</summary>
public class ArmadoCronogramaTests
{
    private const byte Principal = CatalogoIds.RolAsignacion.Principal;
    private const byte Back = CatalogoIds.RolAsignacion.Back;
    private const byte Descanso = CatalogoIds.RolAsignacion.Descanso;

    // ------------------------------------------------------------------ precedencia

    [Fact]
    public void Persona_GanaAGeneral_YGeneralAProyecto()
    {
        var c = Armar([Dia(1, 1, 5), Dia(1, 1, 6), Dia(1, 1, 7)],
        [
            Persona(10, 1, 5), General(20, 5), DeProyecto(30, 1, 5),
            General(21, 6), DeProyecto(31, 1, 6),
            DeProyecto(32, 1, 7),
        ]);

        Assert.Equal(("NOVEDAD", "PERSONA"), (Celda(c, 1, 5).Estado, Celda(c, 1, 5).Novedad!.Aplicacion));
        Assert.Equal("GENERAL", Celda(c, 1, 6).Novedad!.Aplicacion);
        Assert.Equal("PROYECTO", Celda(c, 1, 7).Novedad!.Aplicacion);
    }

    [Fact]
    public void Novedad_GanaAlChoque_ChoqueAProyecto_ProyectoADescanso()
    {
        var c = Armar(
        [
            Dia(1, 1, 5), Dia(1, 2, 5), // choque con novedad
            Dia(1, 1, 6), Dia(1, 2, 6), // choque
            Dia(1, 1, 7), Dia(1, 1, 7, Descanso), // trabajo y descanso del mismo proyecto
            Dia(1, 1, 8, Descanso),
        ], [Persona(10, 1, 5)]);

        Assert.Equal("NOVEDAD", Celda(c, 1, 5).Estado);
        Assert.Equal(2, Celda(c, 1, 5).Asignaciones.Count);
        Assert.Equal("CHOQUE", Celda(c, 1, 6).Estado);
        Assert.Equal("PROYECTO", Celda(c, 1, 7).Estado);
        Assert.Equal(["PRINCIPAL", "DESCANSO"], Celda(c, 1, 7).Asignaciones.Select(a => a.Rol));
        Assert.Equal("DESCANSO", Celda(c, 1, 8).Estado);
    }

    [Fact]
    public void BackYPrincipalEnDosProyectos_EsChoque()
    {
        var c = Armar([Dia(1, 1, 5, Principal), Dia(1, 2, 5, Back)]);
        Assert.Equal("CHOQUE", Celda(c, 1, 5).Estado);
    }

    [Fact]
    public void DiasLibres_NoSeDevuelven()
    {
        var c = Armar([Dia(1, 1, 5), Dia(1, 1, 9)]);
        Assert.Equal([D(5), D(9)], c.Personas.Single().Dias.Select(d => d.Fecha));
    }

    [Fact]
    public void General_EnUnDiaSinAsignacion_SeMuestraSinAsignaciones()
    {
        var c = Armar([Dia(1, 1, 5)], [General(20, 12)]);

        var celda = Celda(c, 1, 12);
        Assert.Equal(("NOVEDAD", "FERIADO"), (celda.Estado, celda.Novedad!.Tipo));
        Assert.Empty(celda.Asignaciones);
    }

    // ------------------------------------------------------------------ D1: novedad de proyecto

    [Fact]
    public void D1_NovedadDeOtroProyecto_NoAplica()
    {
        var c = Armar([Dia(1, 1, 5), Dia(1, 2, 6)], [DeProyecto(30, 2, 5)]);
        Assert.Equal("PROYECTO", Celda(c, 1, 5).Estado);
    }

    [Fact]
    public void D1_NovedadDeSuProyecto_EnUnDiaSinAsignacionEnEse_NoAplica()
    {
        // Persona con días en el proyecto 1 (día 5) y en el 2 (día 6): la novedad del proyecto 1 del día 6 no aplica.
        var c = Armar([Dia(1, 1, 5), Dia(1, 2, 6)], [DeProyecto(30, 1, 6)]);
        Assert.Equal("PROYECTO", Celda(c, 1, 6).Estado);
        Assert.Null(Celda(c, 1, 6).Novedad);
    }

    [Fact]
    public void D1_NovedadDeProyecto_AplicaTambienEnElDescanso()
    {
        var c = Armar([Dia(1, 1, 5, Descanso)], [DeProyecto(30, 1, 5)]);
        Assert.Equal("NOVEDAD", Celda(c, 1, 5).Estado);
    }

    // ------------------------------------------------------------------ D2: otros proyectos

    [Fact]
    public void D2_UnoVisibleYUnoNoVisible_EsChoque_SinDatosDelAjeno()
    {
        var c = Armar([Dia(1, 1, 5)], otros: [new DiaOtrosProyectos(1, D(5), 1)]);

        var celda = Celda(c, 1, 5);
        Assert.Equal(("CHOQUE", 1), (celda.Estado, celda.OtrosProyectos));
        Assert.Equal([1], celda.Asignaciones.Select(a => a.ProyectoId));
        Assert.Equal([1], c.Proyectos.Select(p => p.Id));
    }

    [Fact]
    public void D2_SoloEnUnProyectoNoVisible_EsOtroProyecto()
    {
        var c = Armar([Dia(1, 1, 5), Dia(1, 1, 6, Descanso)], otros: [new DiaOtrosProyectos(1, D(6), 1), new DiaOtrosProyectos(1, D(7), 1)]);

        Assert.Equal(("OTRO_PROYECTO", 1), (Celda(c, 1, 6).Estado, Celda(c, 1, 6).OtrosProyectos));
        Assert.Equal("OTRO_PROYECTO", Celda(c, 1, 7).Estado);
        Assert.Empty(Celda(c, 1, 7).Asignaciones);
    }

    // ------------------------------------------------------------------ D4 y D9

    [Fact]
    public void D4_PersonaSoloConNovedad_NoAparece()
    {
        var c = Armar([Dia(1, 1, 5)], [Persona(10, 2, 5)]);
        Assert.Equal(["900001"], c.Personas.Select(p => p.CodigoEkon));
    }

    [Fact]
    public void D9_VariasNovedadesPersona_GanaLaDeIdMayor_ConNovedadesExtra()
    {
        var c = Armar([Dia(1, 1, 5), Dia(1, 1, 6)], [Persona(10, 1, 5, "VACACIONES"), Persona(15, 1, 5, "PERMISO"), Persona(12, 1, 5, "PATERNIDAD"),
            Persona(11, 1, 6)]);

        Assert.Equal(("PERMISO", 2), (Celda(c, 1, 5).Novedad!.Tipo, Celda(c, 1, 5).NovedadesExtra));
        Assert.Equal(0, Celda(c, 1, 6).NovedadesExtra);
    }

    // ------------------------------------------------------------------ armado

    [Fact]
    public void Actividad_VigenteEseDia_LaDeMayorVersion()
    {
        var c = Armar([Dia(1, 1, 5), Dia(1, 1, 20)], actividades:
        [
            new ActividadCronograma(1, 1, 1, "A.01", D(1), D(31)),
            new ActividadCronograma(1, 2, 2, "A.02", D(15), D(31)),
        ]);

        Assert.Equal("A.01", Celda(c, 1, 5).Asignaciones.Single().Actividad);
        Assert.Equal("A.02", Celda(c, 1, 20).Asignaciones.Single().Actividad);
    }

    [Fact]
    public void Personas_PorNombre_CargoDelUltimoDia_YLeyendaSinRepetir()
    {
        var c = Armar([Dia(2, 1, 5), Dia(1, 1, 5, cargo: "AYUDANTE"), Dia(1, 1, 9, cargo: "SUPERVISOR"), Dia(1, 2, 6)],
            [Persona(10, 1, 5), Persona(11, 2, 5), General(20, 6)]);

        Assert.Equal(["900001", "900002"], c.Personas.Select(p => p.CodigoEkon));
        Assert.Equal([(1, "SUPERVISOR"), (2, "CARGO")], c.Personas[0].AsignadoEn.Select(a => (a.ProyectoId, a.Cargo)));
        Assert.Equal(["FERIADO", "VACACIONES"], c.LeyendaNovedades.Select(l => l.Tipo));
    }

    [Fact]
    public void SinDias_RespuestaVacia()
    {
        var c = Armar([]);
        Assert.Empty(c.Personas);
        Assert.Empty(c.Proyectos);
    }

    [Theory]
    [InlineData(22, 6, true)]
    [InlineData(17, 4, true)]
    [InlineData(15, 0, true)] // 00:00 = fin del día
    [InlineData(7, 18, false)]
    public void Horario_CruzaMedianoche(int entrada, int salida, bool esperado) =>
        Assert.Equal(esperado, ArmadoCronograma.Horario(73, null, new TimeOnly(entrada, 0), new TimeOnly(salida, 0)).CruzaMedianoche);

    [Fact]
    public void Horario_SinHoras_NoCruza() => Assert.False(ArmadoCronograma.Horario(null, null, null, null).CruzaMedianoche);
}
