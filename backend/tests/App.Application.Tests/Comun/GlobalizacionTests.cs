using App.Application.Comun;

namespace App.Application.Tests.Comun;

/// <summary>
/// TAREA-7d-1 (D7, J1): NLS en toda la solución (Directory.Build.props) y la zona de Ecuador con el identificador de
/// Windows, que no requiere ICU (Windows Server 2019 no lo trae).
/// </summary>
public class GlobalizacionTests
{
    [Fact]
    public void UseNls_Activo()
    {
        // El host entrega las opciones de runtimeconfig.json como texto ("true").
        Assert.Equal("true", AppContext.GetData("System.Globalization.UseNls")?.ToString(), ignoreCase: true);
    }

    [Fact]
    public void ZonaEcuador_SaPacific_UtcMenos5_SinHorarioDeVerano()
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(FechaNegocio.ZonaNegocio);

        Assert.Equal("SA Pacific Standard Time", FechaNegocio.ZonaNegocio);
        Assert.Equal(TimeSpan.FromHours(-5), zona.BaseUtcOffset);
        Assert.False(zona.SupportsDaylightSavingTime);
        Assert.Equal(new DateOnly(2026, 11, 4),
            FechaNegocio.Hoy(new RelojFijo(new DateTimeOffset(2026, 11, 5, 4, 59, 0, TimeSpan.Zero)))); // 23:59 en Ecuador
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
