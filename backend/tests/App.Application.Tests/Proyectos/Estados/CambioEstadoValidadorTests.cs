using App.Application.Proyectos.Estados;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos.Estados;

/// <summary>E1 y E2: mensajes exactos en español por campo.</summary>
public class CambioEstadoValidadorTests
{
    private static readonly DateOnly Inicio = new(2027, 4, 1);
    private static readonly DateOnly Fin = new(2027, 4, 30);
    private static readonly CambioEstadoValidador Validador = new();

    private static ResultadoValidacionCambio Validar(string? destino, DateOnly? fecha, string estadoActual = "ACTIVO") =>
        Validador.Validar(new CambioEstadoSolicitud(destino, fecha), estadoActual, Inicio, Fin);

    [Theory]
    [InlineData("ACTIVO", "SUSPENDIDO", MovimientoEstado.Suspension)]
    [InlineData("ACTIVO", "terminado", MovimientoEstado.Cierre)]
    [InlineData("SUSPENDIDO", "TERMINADO", MovimientoEstado.Cierre)]
    public void Validos(string origen, string destino, MovimientoEstado movimiento)
    {
        var r = Validar(destino, new DateOnly(2027, 4, 15), origen);
        Assert.True(r.EsValido);
        Assert.Equal(movimiento, r.Movimiento);
        Assert.Equal(destino.ToUpperInvariant(), r.EstadoDestino);
    }

    [Fact]
    public void Fecha_EsInclusiva_EnInicioYFin()
    {
        Assert.True(Validar("SUSPENDIDO", Inicio).EsValido);
        Assert.True(Validar("SUSPENDIDO", Fin).EsValido);
    }

    [Theory]
    [InlineData(null, "ACTIVO", "El estado destino es obligatorio.")]
    [InlineData("  ", "ACTIVO", "El estado destino es obligatorio.")]
    [InlineData("xyz", "ACTIVO", "El estado destino 'XYZ' no existe.")]
    [InlineData("ACTIVO", "ACTIVO", "El proyecto ya está en estado ACTIVO.")]
    [InlineData("ACTIVO", "SUSPENDIDO", "La reactivación se registra con la opción Reactivar.")]
    [InlineData("INACTIVO", "ACTIVO", "Este cambio de estado no está permitido desde el estado actual (ACTIVO → INACTIVO).")]
    [InlineData("SUSPENDIDO", "TERMINADO", "Este cambio de estado no está permitido desde el estado actual (TERMINADO → SUSPENDIDO).")]
    [InlineData("ACTIVO", "TERMINADO", "Este cambio de estado no está permitido desde el estado actual (TERMINADO → ACTIVO).")]
    public void EstadoDestino_Invalido(string? destino, string estadoActual, string mensaje)
    {
        var r = Validar(destino, new DateOnly(2027, 4, 15), estadoActual);
        Assert.False(r.EsValido);
        Assert.Equal([mensaje], r.Errores["estadoDestino"]);
    }

    [Fact]
    public void Fecha_Obligatoria() =>
        Assert.Equal(["La fecha del movimiento es obligatoria."], Validar("SUSPENDIDO", null).Errores["fecha"]);

    [Fact]
    public void Fecha_AntesDelInicio() =>
        Assert.Equal(["La fecha del movimiento no puede ser menor a la fecha de inicio del proyecto (01/04/2027)."],
            Validar("SUSPENDIDO", new DateOnly(2027, 3, 31)).Errores["fecha"]);

    [Fact]
    public void Fecha_DespuesDelFin() =>
        Assert.Equal(["La fecha del movimiento no puede ser mayor a la fecha fin del proyecto (30/04/2027)."],
            Validar("SUSPENDIDO", new DateOnly(2027, 5, 1)).Errores["fecha"]);

    [Fact]
    public void Acumula_ErroresDeDestinoYFecha()
    {
        var r = Validar("XYZ", null);
        Assert.Equal(["estadoDestino", "fecha"], r.Errores.Keys.Order());
    }
}
