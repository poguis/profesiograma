namespace App.Application.Novedades;

// Novedades (TAREA-23-2): edición y anulación. Ver docs/fases/FASE_5_Novedades.md §7.

/// <summary>
/// Cuerpo de PUT /api/novedades/{id}. Reemplaza lo editable (N8): tipo (código), rango, días excluidos y observación
/// (null o vacía la borra, P1). Aplicación y sujeto son inmutables. Version = RowVer en Base64 (N9).
/// </summary>
public sealed record EditarNovedadSolicitud(
    string? Version,
    string? TipoNovedad,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    IReadOnlyList<DateOnly>? DiasExcluidos,
    string? Observacion);

/// <summary>Cuerpo de POST /api/novedades/{id}/anular (N10: motivo 5–500, irreversible).</summary>
public sealed record AnularNovedadSolicitud(string? Version, string? Motivo);

public sealed record NovedadEditadaDto(int Id, string Version);

/// <summary>Token de concurrencia de una novedad (N9): RowVer (8 bytes) en Base64.</summary>
public static class VersionNovedad
{
    public const string Clave = "version";
    public const int Largo = 8;
    public const string MensajeFalta = "Falta la versión de la novedad; vuelve a cargarla.";
    public const string MensajeInvalida = "La versión de la novedad no es válida; vuelve a cargarla.";
    public const string MensajeCambio = "La novedad cambió; vuelve a cargarla.";

    /// <summary>Bytes del token; null con el error en <see cref="Clave"/> si falta, no es Base64 o no mide 8 bytes (400, nunca 500).</summary>
    public static byte[]? Leer(string? texto, IDictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            errores[Clave] = [MensajeFalta];
            return null;
        }

        var bytes = new byte[Largo];
        if (!Convert.TryFromBase64String(texto.Trim(), bytes, out var escritos) || escritos != Largo)
        {
            errores[Clave] = [MensajeInvalida];
            return null;
        }

        return bytes;
    }

    public static bool Igual(byte[] token, byte[] rowVer) => token.AsSpan().SequenceEqual(rowVer);
}

/// <summary>Novedad tal como está guardada, para decidir la edición o la anulación (lectura sin seguimiento).</summary>
public sealed record NovedadParaModificar(
    int Id,
    byte AplicacionId,
    byte TipoNovedadId,
    int? EmpleadoId,
    int? ProyectoId,
    string OrigenCodigo,
    bool OrigenEditable,
    int? PropietarioProyectoId,
    bool Anulada,
    int CreadoPorId,
    string? Observacion,
    IReadOnlyList<DateOnly> Dias,
    byte[] RowVer);

/// <summary>Edición validada: tipo, días resultantes (ordenados, ≥ 1) y observación ya normalizada.</summary>
public sealed record EdicionValidada(TipoNovedadRef Tipo, IReadOnlyList<DateOnly> Dias, string? Observacion);

/// <summary>Lo que se escribe al editar. Token = la versión que envió el cliente (OriginalValue de RowVer, R1).</summary>
public sealed record EdicionNovedad(int Id, byte[] Token, byte TipoNovedadId, IReadOnlyList<DateOnly> Dias, string? Observacion);

/// <summary>Lo que se escribe al anular (FechaAnulacion en UTC truncada a segundos).</summary>
public sealed record AnulacionNovedad(int Id, byte[] Token, string Motivo, int AnuladaPorId, DateTime FechaAnulacionUtc);

/// <summary>
/// Escritura de la edición y la anulación (Infrastructure). Se usa DENTRO de ITransaccionAsignaciones, salvo la lectura.
/// </summary>
public interface INovedadModificacion
{
    /// <summary>Null si no existe o no es visible (<paramref name="visibleParaUsuarioId"/> null = Admin). Sin seguimiento.</summary>
    Task<NovedadParaModificar?> ObtenerParaModificarAsync(int id, int? visibleParaUsuarioId, CancellationToken ct);

    /// <summary>Aplica la edición (diff de días y R1) y devuelve la versión nueva.</summary>
    /// <exception cref="Proyectos.Estados.ConflictoConcurrenciaException">Otro proceso modificó la novedad (409).</exception>
    Task<string> EditarAsync(EdicionNovedad edicion, CancellationToken ct);

    /// <summary>Anula y devuelve la versión nueva.</summary>
    /// <exception cref="Proyectos.Estados.ConflictoConcurrenciaException">Otro proceso modificó la novedad (409).</exception>
    Task<string> AnularAsync(AnulacionNovedad anulacion, CancellationToken ct);
}

/// <summary>
/// TAREA-27 (pendiente 67): motivo de un 409 de novedades. La API lo publica en la extensión `tipoConflicto` y el frontend
/// decide con él (nunca con el texto del mensaje).
/// </summary>
public enum TipoConflictoNovedad
{
    /// <summary>N7: otra novedad no anulada con la misma aplicación, sujeto, tipo y algún día en común.</summary>
    Duplicado,
    /// <summary>La versión no coincide u otro proceso la modificó: hay que recargar.</summary>
    Cambio,
    /// <summary>La novedad está (o ya está) anulada.</summary>
    Anulada,
    /// <summary>Otro registro se guardó al mismo tiempo (alta puntual concurrente al crear): basta reintentar.</summary>
    RegistroConcurrente,
}

public enum EstadoModificacionNovedad
{
    Hecho,
    Invalido,
    NoEncontrada,
    Prohibido,
    /// <summary>409 con <see cref="ResultadoModificacionNovedad.MensajeConflicto"/> y su <see cref="TipoConflictoNovedad"/> (anulada o cambió).</summary>
    Conflicto,
    /// <summary>N7 (409, R6).</summary>
    Duplicado,
}

public sealed record ResultadoModificacionNovedad(
    EstadoModificacionNovedad Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    NovedadEditadaDto? Hecho = null,
    string? MensajeConflicto = null,
    string? CodigoDuplicado = null,
    TipoConflictoNovedad? TipoConflicto = null)
{
    public const string MensajeYaAnulada = "La novedad ya está anulada.";

    public static ResultadoModificacionNovedad Exito(NovedadEditadaDto dto) => new(EstadoModificacionNovedad.Hecho, Hecho: dto);
    public static ResultadoModificacionNovedad Invalido(IReadOnlyDictionary<string, string[]> errores) => new(EstadoModificacionNovedad.Invalido, errores);
    public static ResultadoModificacionNovedad Invalido(string clave, string mensaje) => Invalido(new Dictionary<string, string[]> { [clave] = [mensaje] });
    public static ResultadoModificacionNovedad NoEncontrada() => new(EstadoModificacionNovedad.NoEncontrada);
    public static ResultadoModificacionNovedad Prohibido() => new(EstadoModificacionNovedad.Prohibido);
    public static ResultadoModificacionNovedad Anulada(string mensaje) =>
        new(EstadoModificacionNovedad.Conflicto, MensajeConflicto: mensaje, TipoConflicto: TipoConflictoNovedad.Anulada);
    public static ResultadoModificacionNovedad Cambio() =>
        new(EstadoModificacionNovedad.Conflicto, MensajeConflicto: VersionNovedad.MensajeCambio, TipoConflicto: TipoConflictoNovedad.Cambio);
    /// <summary>R6: el código solo si la novedad existente es visible para quien edita.</summary>
    public static ResultadoModificacionNovedad Duplicado(DuplicadoNovedad d) => new(EstadoModificacionNovedad.Duplicado, CodigoDuplicado: d.Visible ? d.Codigo : null);
}
