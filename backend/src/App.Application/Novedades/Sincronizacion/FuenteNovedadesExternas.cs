namespace App.Application.Novedades.Sincronizacion;

// Sincronización manual de PERMISO MEDICO (TAREA-23-3). Ver docs/fases/FASE_5_Novedades.md §8.

/// <summary>
/// Una fila de la lista NOVEDADES ASISTENCIA tal como llega de la fuente, SOLO con las columnas que se usan: ID
/// (LegacyId), Título (Codigo), NOVEDAD_UID, ESTADO_NOVEDAD, DETALLE_JSON y CORREO_PERSONA (respaldo del correo). Nunca
/// «Creado por» ni «Modificado por». Dato potencialmente de salud: no se registra en logs ni viaja en DTO.
/// </summary>
public sealed record FilaNovedadExterna(int? LegacyId, string? Codigo, string? Uid, string? Estado, string? DetalleJson, string? CorreoPersona);

/// <summary>Lectura completa de la fuente. FilasIlegibles: filas del archivo que no se pudieron separar en columnas.</summary>
public sealed record LecturaNovedadesExternas(string Fuente, IReadOnlyList<FilaNovedadExterna> Filas, int FilasIlegibles);

/// <summary>Fuente de las novedades externas (Simulado y ArchivoLocal solo en Development; Graph en la 23-4).</summary>
public interface IFuenteNovedadesExternas
{
    /// <summary>Nombre de la fuente para la respuesta y el log (Simulado, ArchivoLocal, NoConfigurada). Sin rutas.</summary>
    string Fuente { get; }

    /// <summary>P1: solo la fuente Simulada admite escenarios (base, fechas, anuladas).</summary>
    bool AdmiteEscenarios { get; }

    /// <exception cref="FuenteNovedadesNoDisponibleException">No configurada, archivo ausente o ilegible, o más de MaxFilas.</exception>
    Task<LecturaNovedadesExternas> LeerPermisosMedicosAsync(string? escenario, CancellationToken ct);
}

/// <summary>La fuente no está configurada o no se pudo leer (503). Título y detalle sin rutas ni datos personales.</summary>
public sealed class FuenteNovedadesNoDisponibleException(string titulo, string detalle) : Exception(detalle)
{
    public const string TituloNoConfigurada = "Fuente de novedades externas no configurada";
    public const string TituloNoDisponible = "Fuente de novedades externas no disponible";

    public string Titulo { get; } = titulo;
    public string Detalle { get; } = detalle;

    public static FuenteNovedadesNoDisponibleException NoConfigurada() =>
        new(TituloNoConfigurada, "La sincronización de PERMISO MEDICO no tiene una fuente configurada en este entorno.");

    public static FuenteNovedadesNoDisponibleException NoDisponible(string detalle) => new(TituloNoDisponible, detalle);
}

public static class FuenteNovedades
{
    /// <summary>Tope de filas de una lectura (más → 503 «no disponible»).</summary>
    public const int MaxFilas = 5000;

    public const string EscenarioBase = "base", EscenarioFechas = "fechas", EscenarioAnuladas = "anuladas";

    public static readonly IReadOnlyList<string> Escenarios = [EscenarioBase, EscenarioFechas, EscenarioAnuladas];
}
