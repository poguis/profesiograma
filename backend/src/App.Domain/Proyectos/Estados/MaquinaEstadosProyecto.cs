namespace App.Domain.Proyectos.Estados;

/// <summary>Movimiento que resulta de pasar de un estado a otro (E1).</summary>
public enum MovimientoEstado
{
    SinCambio,
    Suspension,
    Cierre,
    Reactivacion,
    NoPermitido,
}

/// <summary>Códigos del catálogo EstadoProyecto (no dependen de los Id).</summary>
public static class CodigosEstadoProyecto
{
    public const string Activo = "ACTIVO";
    public const string Suspendido = "SUSPENDIDO";
    public const string Inactivo = "INACTIVO";
    public const string Terminado = "TERMINADO";
}

/// <summary>
/// Máquina de estados del proyecto (FASE_1 §2.5, igual que estadoEdit_1.OnChange de la app original):
/// ACTIVO→SUSPENDIDO = SUSPENSION; ACTIVO→TERMINADO y SUSPENDIDO→TERMINADO = CIERRE; SUSPENDIDO→ACTIVO = REACTIVACION;
/// mismo estado = SIN_CAMBIO; cualquier otro caso (INACTIVO como origen o destino, salir de TERMINADO) = NO_PERMITIDO.
/// </summary>
public static class MaquinaEstadosProyecto
{
    /// <summary>Resuelve el movimiento por código (sin distinguir mayúsculas). Un código desconocido es NO_PERMITIDO.</summary>
    public static MovimientoEstado Resolver(string estadoOrigen, string estadoDestino)
    {
        ArgumentNullException.ThrowIfNull(estadoOrigen);
        ArgumentNullException.ThrowIfNull(estadoDestino);
        var origen = estadoOrigen.Trim().ToUpperInvariant();
        var destino = estadoDestino.Trim().ToUpperInvariant();

        return (origen, destino) switch
        {
            (CodigosEstadoProyecto.Activo, CodigosEstadoProyecto.Suspendido) => MovimientoEstado.Suspension,
            (CodigosEstadoProyecto.Activo, CodigosEstadoProyecto.Terminado) => MovimientoEstado.Cierre,
            (CodigosEstadoProyecto.Suspendido, CodigosEstadoProyecto.Terminado) => MovimientoEstado.Cierre,
            (CodigosEstadoProyecto.Suspendido, CodigosEstadoProyecto.Activo) => MovimientoEstado.Reactivacion,
            _ when origen == destino && EsConocido(origen) => MovimientoEstado.SinCambio,
            _ => MovimientoEstado.NoPermitido,
        };
    }

    /// <summary>Código del catálogo TipoMovimiento para un movimiento que cambia el estado.</summary>
    public static string CodigoTipoMovimiento(MovimientoEstado movimiento) => movimiento switch
    {
        MovimientoEstado.Suspension => "SUSPENSION",
        MovimientoEstado.Cierre => "CIERRE",
        MovimientoEstado.Reactivacion => "REACTIVACION",
        _ => throw new ArgumentOutOfRangeException(nameof(movimiento), movimiento, "El movimiento no cambia el estado."),
    };

    public static bool EsConocido(string codigo) => codigo.Trim().ToUpperInvariant() is
        CodigosEstadoProyecto.Activo or CodigosEstadoProyecto.Suspendido or
        CodigosEstadoProyecto.Inactivo or CodigosEstadoProyecto.Terminado;
}
