using App.Domain.Proyectos.Estados;

namespace App.Domain.Tests.Proyectos.Estados;

/// <summary>E1: las 16 combinaciones de los 4 estados (igual que estadoEdit_1.OnChange de la app original).</summary>
public class MaquinaEstadosProyectoTests
{
    [Theory]
    [InlineData("ACTIVO", "ACTIVO", MovimientoEstado.SinCambio)]
    [InlineData("ACTIVO", "SUSPENDIDO", MovimientoEstado.Suspension)]
    [InlineData("ACTIVO", "INACTIVO", MovimientoEstado.NoPermitido)]
    [InlineData("ACTIVO", "TERMINADO", MovimientoEstado.Cierre)]
    [InlineData("SUSPENDIDO", "ACTIVO", MovimientoEstado.Reactivacion)]
    [InlineData("SUSPENDIDO", "SUSPENDIDO", MovimientoEstado.SinCambio)]
    [InlineData("SUSPENDIDO", "INACTIVO", MovimientoEstado.NoPermitido)]
    [InlineData("SUSPENDIDO", "TERMINADO", MovimientoEstado.Cierre)]
    [InlineData("INACTIVO", "ACTIVO", MovimientoEstado.NoPermitido)]
    [InlineData("INACTIVO", "SUSPENDIDO", MovimientoEstado.NoPermitido)]
    [InlineData("INACTIVO", "INACTIVO", MovimientoEstado.SinCambio)]
    [InlineData("INACTIVO", "TERMINADO", MovimientoEstado.NoPermitido)]
    [InlineData("TERMINADO", "ACTIVO", MovimientoEstado.NoPermitido)]
    [InlineData("TERMINADO", "SUSPENDIDO", MovimientoEstado.NoPermitido)]
    [InlineData("TERMINADO", "INACTIVO", MovimientoEstado.NoPermitido)]
    [InlineData("TERMINADO", "TERMINADO", MovimientoEstado.SinCambio)]
    public void Resolver_16Combinaciones(string origen, string destino, MovimientoEstado esperado) =>
        Assert.Equal(esperado, MaquinaEstadosProyecto.Resolver(origen, destino));

    [Fact]
    public void Resolver_SinDistinguirMayusculas() =>
        Assert.Equal(MovimientoEstado.Suspension, MaquinaEstadosProyecto.Resolver(" activo ", "suspendido"));

    [Fact]
    public void Resolver_CodigoDesconocido_NoPermitido()
    {
        Assert.Equal(MovimientoEstado.NoPermitido, MaquinaEstadosProyecto.Resolver("ACTIVO", "XYZ"));
        Assert.Equal(MovimientoEstado.NoPermitido, MaquinaEstadosProyecto.Resolver("XYZ", "XYZ"));
    }

    [Theory]
    [InlineData(MovimientoEstado.Suspension, "SUSPENSION")]
    [InlineData(MovimientoEstado.Cierre, "CIERRE")]
    [InlineData(MovimientoEstado.Reactivacion, "REACTIVACION")]
    public void CodigoTipoMovimiento(MovimientoEstado movimiento, string codigo) =>
        Assert.Equal(codigo, MaquinaEstadosProyecto.CodigoTipoMovimiento(movimiento));

    [Theory]
    [InlineData(MovimientoEstado.SinCambio)]
    [InlineData(MovimientoEstado.NoPermitido)]
    public void CodigoTipoMovimiento_SinCambioDeEstado_Lanza(MovimientoEstado movimiento) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MaquinaEstadosProyecto.CodigoTipoMovimiento(movimiento));
}
