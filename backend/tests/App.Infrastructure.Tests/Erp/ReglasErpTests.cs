using App.Infrastructure.Erp;

namespace App.Infrastructure.Tests.Erp;

/// <summary>Conversión de horas del ERP (formato real verificado: "HHmm", 4 dígitos) y reglas de filtro/nombre.</summary>
public class ReglasErpTests
{
    [Theory]
    [InlineData("0700", 7, 0)]
    [InlineData("1830", 18, 30)]
    [InlineData("0000", 0, 0)]
    [InlineData("07:00", 7, 0)]
    [InlineData("7:05", 7, 5)]
    [InlineData(" 1800 ", 18, 0)]
    public void AHora_Valida(string valor, int hora, int minuto) => Assert.Equal(new TimeOnly(hora, minuto), ReglasErp.AHora(valor));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2400")]
    [InlineData("0760")]
    [InlineData("700")]
    [InlineData("ab00")]
    [InlineData("07-00")]
    public void AHora_Invalida_Null(string? valor) => Assert.Null(ReglasErp.AHora(valor));

    [Theory]
    [InlineData("1100", 660)]
    [InlineData("1000", 600)]
    [InlineData("0930", 570)]
    [InlineData("08:00", 480)]
    public void AMinutos_Valido(string valor, int minutos) => Assert.Equal((short)minutos, ReglasErp.AMinutos(valor));

    [Theory]
    [InlineData(null)]
    [InlineData("1160")]
    [InlineData("x")]
    public void AMinutos_Invalido_Null(string? valor) => Assert.Null(ReglasErp.AMinutos(valor));

    [Theory]
    [InlineData("Activo", true)]
    [InlineData(" ACTIVO ", true)]
    [InlineData("Inactivo", false)]
    [InlineData(null, false)]
    public void EsProyectoActivo(string? status, bool esperado) => Assert.Equal(esperado, ReglasErp.EsProyectoActivo(status));

    [Theory]
    [InlineData("A", true)]
    [InlineData("a", true)]
    [InlineData("I", false)]
    [InlineData(null, false)]
    public void EsHorarioActivo(string? status, bool esperado) => Assert.Equal(esperado, ReglasErp.EsHorarioActivo(status));

    [Theory]
    [InlineData("CORTO", "RAZON SOCIAL S.A.", "RAZON SOCIAL S.A.")]
    [InlineData("CORTO", null, "CORTO")]
    [InlineData("CORTO", "  ", "CORTO")]
    public void NombreCompania_TradeNameOName(string nombre, string? comercial, string esperado) =>
        Assert.Equal(esperado, ReglasErp.NombreCompania(nombre, comercial));
}
