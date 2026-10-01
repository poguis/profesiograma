using System.Globalization;
using App.Application.Proyectos.Crear;

namespace App.Application.Tests.Proyectos.Crear;

/// <summary>Resumen como la app original: persona / rol / proyecto / mes con días "5, 6, 7" (mes en es-EC, nunca la cultura del servidor).</summary>
public class CalculadorCrucesTests
{
    private static CruceDto C(int dia, int mes = 12, string rol = "PRINCIPAL", string nombre = "EMPLEADO PRUEBA 06",
        string proyecto = "PROYECTO X", string? codigo = "PRY-20261201-abcdef") =>
        new("EXTERNO", 6, "DEV006", nombre, new DateOnly(2026, mes, dia), rol, proyecto, codigo, "ACTIVO");

    [Fact]
    public void Resumen_DiasOrdenadosSinRepetir_YMesEnEspanol()
    {
        var resumen = CalculadorCruces.Resumen([C(7), C(5), C(6), C(5)]);

        Assert.Equal([new ResumenCruceDto("EMPLEADO PRUEBA 06", "PRINCIPAL", "PRY-20261201-abcdef · PROYECTO X", "diciembre 2026", "5, 6, 7")], resumen);
    }

    [Fact]
    public void Resumen_AgrupaPorMesRolYProyecto()
    {
        var resumen = CalculadorCruces.Resumen(
        [
            C(30, 11), C(1), C(2), C(3, rol: "BACK"),
            new CruceDto("INTERNO", 6, "DEV006", "EMPLEADO PRUEBA 06", new DateOnly(2026, 12, 4), "PRINCIPAL", "MISMO PROYECTO", null, null),
        ]);

        Assert.Equal(
            [
                new ResumenCruceDto("EMPLEADO PRUEBA 06", "PRINCIPAL", "PRY-20261201-abcdef · PROYECTO X", "noviembre 2026", "30"),
                new ResumenCruceDto("EMPLEADO PRUEBA 06", "BACK", "PRY-20261201-abcdef · PROYECTO X", "diciembre 2026", "3"),
                new ResumenCruceDto("EMPLEADO PRUEBA 06", "PRINCIPAL", "MISMO PROYECTO", "diciembre 2026", "4"),
                new ResumenCruceDto("EMPLEADO PRUEBA 06", "PRINCIPAL", "PRY-20261201-abcdef · PROYECTO X", "diciembre 2026", "1, 2"),
            ],
            resumen);
    }

    [Fact]
    public void Resumen_NoDependeDeLaCulturaDelServidor()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("diciembre 2026", CalculadorCruces.Resumen([C(5)]).Single().Mes);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
