using App.Application.Proyectos.Crear;

namespace App.Application.Tests.Proyectos.Crear;

public class ReglasAlmuerzoTests
{
    [Fact]
    public void OpcionesSalida_CadaHoraDe11a14() =>
        Assert.Equal(["11:00", "12:00", "13:00", "14:00"], ReglasAlmuerzo.OpcionesSalida);

    [Fact]
    public void OpcionesRegreso_CadaHoraDe12a15() =>
        Assert.Equal(["12:00", "13:00", "14:00", "15:00"], ReglasAlmuerzo.OpcionesRegreso);

    [Theory]
    [InlineData("10:59", false)]
    [InlineData("11:00", true)]
    [InlineData("13:30", true)]
    [InlineData("14:00", true)]
    [InlineData("14:01", false)]
    public void SalidaEnRango(string hora, bool esperado) =>
        Assert.Equal(esperado, ReglasAlmuerzo.SalidaEnRango(TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("11:59", false)]
    [InlineData("12:00", true)]
    [InlineData("15:00", true)]
    [InlineData("15:01", false)]
    public void RegresoEnRango(string hora, bool esperado) =>
        Assert.Equal(esperado, ReglasAlmuerzo.RegresoEnRango(TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void TodasLasOpciones_EstanEnRango()
    {
        Assert.All(ReglasAlmuerzo.OpcionesSalida, o => Assert.True(ReglasAlmuerzo.SalidaEnRango(TimeOnly.Parse(o, System.Globalization.CultureInfo.InvariantCulture))));
        Assert.All(ReglasAlmuerzo.OpcionesRegreso, o => Assert.True(ReglasAlmuerzo.RegresoEnRango(TimeOnly.Parse(o, System.Globalization.CultureInfo.InvariantCulture))));
    }
}
