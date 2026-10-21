using App.Domain.Novedades;

namespace App.Domain.Tests.Novedades;

/// <summary>TAREA-23-1: código NOV- (R7) y días de la novedad (rango completo, tope, mínimo y máximo).</summary>
public sealed class NovedadDominioTests
{
    [Fact]
    public void Codigo_PrefijoFechaDeNegocioY6HexDelUidEnMinusculas()
    {
        var uid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728ABCD");

        Assert.Equal("NOV-20261021-28abcd", GeneradorCodigoNovedad.Generar(new DateOnly(2026, 10, 21), uid));
    }

    [Fact]
    public void Codigo_UsaLaFechaRecibida_YUidsDistintosDanCodigosDistintos()
    {
        // La fecha la decide quien llama (hoy en Ecuador, FechaNegocio.Hoy): el generador no lee el reloj.
        var a = GeneradorCodigoNovedad.Generar(new DateOnly(2026, 12, 31), Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var b = GeneradorCodigoNovedad.Generar(new DateOnly(2026, 12, 31), Guid.Parse("00000000-0000-0000-0000-000000000002"));

        Assert.Equal("NOV-20261231-000001", a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Codigo_Colision_MismoSufijoConUidsDistintos()
    {
        // Solo cuentan los 6 últimos hex: dos Uid pueden dar el mismo código (por eso Application reintenta, R7).
        var a = GeneradorCodigoNovedad.Generar(new DateOnly(2026, 10, 21), Guid.Parse("11111111-1111-1111-1111-111111abcdef"));
        var b = GeneradorCodigoNovedad.Generar(new DateOnly(2026, 10, 21), Guid.Parse("22222222-2222-2222-2222-222222abcdef"));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Dias_TodosLosDiasCalendarioDelRango_IncluidosFinesDeSemana()
    {
        // 23/10/2026 viernes → 26/10/2026 lunes: incluye sábado y domingo.
        var dias = DiasNovedad.Generar(new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 26));

        Assert.Equal([new(2026, 10, 23), new(2026, 10, 24), new(2026, 10, 25), new(2026, 10, 26)], dias);
    }

    [Fact]
    public void Dias_UnSoloDia_Y_ElTopeDe366()
    {
        var inicio = new DateOnly(2028, 1, 1);

        Assert.Single(DiasNovedad.Generar(inicio, inicio));
        Assert.Equal(366, DiasNovedad.Generar(inicio, inicio.AddDays(365)).Count);
        Assert.Throws<ArgumentException>(() => DiasNovedad.Generar(inicio, inicio.AddDays(366)));
        Assert.Throws<ArgumentException>(() => DiasNovedad.Generar(inicio, inicio.AddDays(-1)));
    }

    [Fact]
    public void Contar_Inclusivo()
    {
        Assert.Equal(1, DiasNovedad.Contar(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)));
        Assert.Equal(31, DiasNovedad.Contar(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Resultantes_RangoMenosExcluidos_Ordenados()
    {
        var inicio = new DateOnly(2026, 11, 1);

        Assert.Equal(5, DiasNovedad.Resultantes(inicio, inicio.AddDays(4), []).Count);
        Assert.Equal([inicio, inicio.AddDays(2)],
            DiasNovedad.Resultantes(inicio, inicio.AddDays(2), [inicio.AddDays(1), inicio.AddDays(1)]));
        // Un excluido fuera del rango no cambia nada aquí (la validación lo rechaza aparte, P2).
        Assert.Equal([inicio, inicio.AddDays(1)], DiasNovedad.Resultantes(inicio, inicio.AddDays(1), [inicio.AddDays(9)]));
        Assert.Empty(DiasNovedad.Resultantes(inicio, inicio.AddDays(1), [inicio, inicio.AddDays(1)]));
        Assert.Equal([inicio], DiasNovedad.Resultantes(inicio, inicio, [])); // inicio = fin
    }

    [Fact]
    public void Limites_MinimoYMaximo_SinDiasLanza()
    {
        var (inicio, fin) = DiasNovedad.Limites([new(2026, 10, 5), new(2026, 10, 1), new(2026, 10, 9)]);

        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 9)), (inicio, fin));
        Assert.Throws<ArgumentException>(() => DiasNovedad.Limites([]));
    }
}
