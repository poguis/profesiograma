using App.Application.Comun;

namespace App.Application.Tests.Comun;

/// <summary>Los mensajes deben ser idénticos a los que devolvía /api/proyectos antes del refactor (TAREA-07).</summary>
public class LectorParametrosTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData(" 3 ", 3)]
    public void LeerPagina_ValoresValidos(string? valor, int esperado)
    {
        var errores = new Dictionary<string, string[]>();
        Assert.Equal(esperado, LectorParametros.LeerPagina(valor, errores));
        Assert.Empty(errores);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void LeerPagina_Invalida_MensajeEnEspanol(string valor)
    {
        var errores = new Dictionary<string, string[]>();
        LectorParametros.LeerPagina(valor, errores);
        Assert.Equal(["La página debe ser un número entero mayor o igual a 1."], errores["pagina"]);
    }

    [Theory]
    [InlineData(null, 20)]
    [InlineData("1", 1)]
    [InlineData("100", 100)]
    public void LeerTamano_ValoresValidos(string? valor, int esperado)
    {
        var errores = new Dictionary<string, string[]>();
        Assert.Equal(esperado, LectorParametros.LeerTamano(valor, 20, 100, errores));
        Assert.Empty(errores);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("500")]
    public void LeerTamano_FueraDeRango_MensajeEnEspanol(string valor)
    {
        var errores = new Dictionary<string, string[]>();
        LectorParametros.LeerTamano(valor, 20, 100, errores);
        Assert.Equal(["El tamaño de página debe ser un número entero entre 1 y 100."], errores["tamano"]);
    }

    [Fact]
    public void LeerFecha_FormatoIso_YErrorEnEspanol()
    {
        var errores = new Dictionary<string, string[]>();
        Assert.Equal(new DateOnly(2026, 10, 1), LectorParametros.LeerFecha("2026-10-01", "desde", errores));
        Assert.Null(LectorParametros.LeerFecha("", "desde", errores));
        Assert.Empty(errores);

        Assert.Null(LectorParametros.LeerFecha("01/10/2026", "hasta", errores));
        Assert.Equal(["La fecha 'hasta' debe tener el formato yyyy-MM-dd."], errores["hasta"]);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData(" false ", false)]
    public void LeerBooleano_ValoresValidos(string? valor, bool esperado)
    {
        var errores = new Dictionary<string, string[]>();
        Assert.Equal(esperado, LectorParametros.LeerBooleano(valor, "soloMisDepartamentos", true, errores));
        Assert.Empty(errores);
    }

    [Fact]
    public void LeerBooleano_Invalido_MensajeEnEspanol()
    {
        var errores = new Dictionary<string, string[]>();
        LectorParametros.LeerBooleano("si", "soloMisDepartamentos", true, errores);
        Assert.Equal(["El valor de 'soloMisDepartamentos' debe ser true o false."], errores["soloMisDepartamentos"]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  DEV003 ", "DEV003")]
    public void Normalizar(string? valor, string? esperado) => Assert.Equal(esperado, LectorParametros.Normalizar(valor));
}
