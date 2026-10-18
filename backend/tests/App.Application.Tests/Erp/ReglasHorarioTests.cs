using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Tests.Proyectos.Crear;
using static App.Application.Tests.Proyectos.Crear.HorariosPrueba;

namespace App.Application.Tests.Erp;

/// <summary>TAREA-26c: horarios seleccionables (pendiente 41) y reglas puras del turno nocturno (pendiente 42).</summary>
public class ReglasHorarioTests
{
    private static readonly IReadOnlyCollection<string> Lista = new DatosFalsos().HorariosNoLaborales;
    private static TimeOnly H(string hora) => TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ no laborales

    public static TheoryData<string, HorarioErp, bool> CasosSeleccionable => new()
    {
        { "73 nocturno", Nocturno73, true },
        { "118 nocturno sin almuerzo", Nocturno118, true },
        { "110 hasta 00:00", Tarde110, true },
        { "1 LIBRE (lista y datos)", Libre1, false },
        { "113 VACACIONES (lista)", Vacaciones113, false },
        { "160 PATERNIDAD PROYECTOS (lista)", Paternidad160, false },
        { "por datos: entrada = salida y 0 trabajados", PorDatos9005, false },
        { "por datos: trabajados nulos", PorDatos9005 with { MinutosTrabajados = null }, false },
        { "entrada = salida con horas trabajadas", PorDatos9005 with { MinutosTrabajados = 480 }, true },
        { "lista sin tildes ni mayúsculas y con espacios", Vacaciones113 with { Descripcion = "  permiso   médico " }, false },
        { "contiene una palabra de la lista sin ser igual", Vacaciones113 with { Descripcion = "DIA LIBRE" }, true },
    };

    [Theory]
    [MemberData(nameof(CasosSeleccionable))]
    public void EsSeleccionable(string caso, HorarioErp horario, bool esperado)
    {
        _ = caso;
        Assert.Equal(esperado, ReglasHorario.EsSeleccionable(horario, Lista));
    }

    [Fact]
    public void ListaVacia_SoloAplicaLaReglaPorDatos()
    {
        Assert.True(ReglasHorario.EsSeleccionable(Vacaciones113, []));
        Assert.False(ReglasHorario.EsSeleccionable(Libre1, []));
    }

    [Fact]
    public async Task ListaDeHorarios_ExcluyeNoLaborales_YLeeElParametroEnCadaLlamada()
    {
        var datos = new DatosFalsos();
        var servicio = new CatalogoErpConsultaServicio(new ErpFalso(), datos);

        Assert.Equal([1, 73, 118, 110], (await servicio.ListarHorariosAsync(CancellationToken.None)).Select(h => h.Codigo));

        datos.HorariosNoLaborales = ["HORARIO 73 (B1)"];
        Assert.Equal([1, 118, 110, 113, 160], (await servicio.ListarHorariosAsync(CancellationToken.None)).Select(h => h.Codigo));
        Assert.Equal(2, datos.LecturasNoLaborales);
    }

    // ------------------------------------------------------------------ turno nocturno (ReglasAlmuerzo)

    [Theory]
    [InlineData("22:00", "06:00", true)]
    [InlineData("17:00", "04:00", true)]
    [InlineData("15:30", "00:00", true)] // 00:00 = fin del día
    [InlineData("07:00", "18:00", false)]
    [InlineData("00:00", "08:00", false)] // empieza a medianoche: diurno
    [InlineData("08:00", "08:00", false)]
    public void EsNocturno(string entrada, string salida, bool esperado) =>
        Assert.Equal(esperado, ReglasAlmuerzo.EsNocturno(H(entrada), H(salida)));

    [Fact]
    public void EsNocturno_SinHoras_False()
    {
        Assert.False(ReglasAlmuerzo.EsNocturno(null, H("06:00")));
        Assert.False(ReglasAlmuerzo.EsNocturno(H("22:00"), null));
    }

    [Theory]
    [InlineData((short)480, (short)480, true)]
    [InlineData((short)480, (short)420, false)]
    [InlineData(null, null, false)]
    [InlineData((short)480, null, false)]
    public void EsOpcional(short? jornada, short? trabajados, bool esperado) =>
        Assert.Equal(esperado, ReglasAlmuerzo.EsOpcional(jornada, trabajados));

    [Theory]
    [InlineData("22:00", 0)]
    [InlineData("23:00", 60)]
    [InlineData("00:00", 120)]
    [InlineData("06:00", 480)]
    [InlineData("21:59", 1439)]
    public void Posicion_TiempoCircular(string hora, int minutos) => Assert.Equal(minutos, ReglasAlmuerzo.Posicion(H(hora), H("22:00")));

    [Theory]
    [InlineData("22:00", true)] // desde la entrada
    [InlineData("02:00", true)]
    [InlineData("05:00", true)]
    [InlineData("06:00", false)] // la salida del turno
    [InlineData("21:00", false)]
    [InlineData("13:00", false)]
    public void SalidaEnTurno_22a06(string hora, bool esperado) =>
        Assert.Equal(esperado, ReglasAlmuerzo.SalidaEnTurno(H(hora), H("22:00"), H("06:00")));

    [Theory]
    [InlineData("22:00", false)] // la entrada
    [InlineData("23:00", true)]
    [InlineData("03:00", true)]
    [InlineData("06:00", true)] // hasta la salida del turno
    [InlineData("07:00", false)]
    public void RegresoEnTurno_22a06(string hora, bool esperado) =>
        Assert.Equal(esperado, ReglasAlmuerzo.RegresoEnTurno(H(hora), H("22:00"), H("06:00")));

    [Fact]
    public void Turno15y30Hasta00_ElRegresoPuedeSerLaMedianoche()
    {
        Assert.True(ReglasAlmuerzo.RegresoEnTurno(TimeOnly.MinValue, H("15:30"), TimeOnly.MinValue));
        Assert.False(ReglasAlmuerzo.SalidaEnTurno(TimeOnly.MinValue, H("15:30"), TimeOnly.MinValue));
        Assert.True(ReglasAlmuerzo.SalidaEnTurno(H("23:00"), H("15:30"), TimeOnly.MinValue));
    }
}
