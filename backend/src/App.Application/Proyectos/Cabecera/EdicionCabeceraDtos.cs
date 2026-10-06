using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;

namespace App.Application.Proyectos.Cabecera;

// ------------------------------------------------------------------ GET /api/proyectos/{id}/cabecera

public sealed record HorarioCabeceraDto(int? Codigo, string? Descripcion, TimeOnly? HoraEntrada, TimeOnly? HoraSalida);

public sealed record ActividadCabeceraDto(
    int Version, string Codigo, string? Descripcion, string? Tipo, string TipoMovimiento, DateOnly FechaInicio, DateOnly FechaFin);

/// <param name="FechaInicioEditable">Solo si el inicio actual es posterior a hoy (C2); si no, MotivoFechaInicio.</param>
/// <param name="FechaFinMinima">Hoy (C4, P2).</param>
/// <param name="ActividadEditable">Solo grupos que requieren proyecto ERP (C6).</param>
public sealed record PermisosCabeceraDto(bool FechaInicioEditable, string? MotivoFechaInicio, DateOnly FechaFinMinima, bool ActividadEditable);

public sealed record OpcionesAlmuerzoDto(IReadOnlyList<string> Salida, IReadOnlyList<string> Regreso);

/// <summary>
/// Datos del formulario de cabecera. PuedeEditar = false (con Motivo) si el proyecto no es ACTIVO.
/// ActividadVigente = regla común ActividadVigente con hoy (O3, TAREA-18b). Corte = hoy en Ecuador.
/// </summary>
public sealed record CabeceraDto(
    int Id, string Codigo, string Estado, bool PuedeEditar, string? Motivo, string Grupo,
    DateOnly FechaInicio, DateOnly FechaFin, HorarioCabeceraDto Horario, string? SalidaAlmuerzo, string? RegresoAlmuerzo,
    ActividadCabeceraDto? ActividadVigente, IReadOnlyList<ActividadCabeceraDto> Actividades,
    PermisosCabeceraDto Permisos, OpcionesAlmuerzoDto OpcionesAlmuerzo, DateOnly Corte);

// ------------------------------------------------------------------ vista previa y resultado

/// <summary>Campo que cambia: fechaInicio | fechaFin | horario | salidaAlmuerzo | regresoAlmuerzo | actividad.</summary>
public sealed record CambioCampoDto(string Campo, string? Anterior, string? Nuevo);

/// <summary>H15: días DESCANSO MANUAL que se vuelven a insertar para un back después del recorte (C4).</summary>
public sealed record DiasAgregadosDto(EmpleadoCambioDto Empleado, string Rol, int Cantidad, DateOnly Desde, DateOnly Hasta);

/// <summary>
/// Actividad resultante. Accion = SIN_CAMBIO | MODIFICADA | ELIMINADA | NUEVA. Las fechas son las resultantes
/// (en ELIMINADA, las que tenía); las anteriores solo en MODIFICADA.
/// </summary>
public sealed record ActividadResultanteDto(
    int? Version, string Codigo, string? Descripcion, DateOnly FechaInicio, DateOnly FechaFin,
    DateOnly? FechaInicioAnterior, DateOnly? FechaFinAnterior, string Accion);

/// <summary>
/// 200 de POST /api/proyectos/{id}/cabecera/previsualizar (no guarda). Impacto del recorte (C4) con los DTO de la vista
/// previa del cambio de estado. TipoEtapa = EDICION_CABECERA | CAMBIO_ACTIVIDAD (C7).
/// </summary>
public sealed record PrevisualizacionCabeceraDto(
    DateOnly Corte,
    string TipoEtapa,
    DateOnly FechaInicioNueva,
    DateOnly FechaFinNueva,
    IReadOnlyList<CambioCampoDto> Cambios,
    IReadOnlyList<DiasEliminadosDto> DiasEliminados,
    IReadOnlyList<PersonaEliminadaDto> PersonalEliminado,
    IReadOnlyList<PersonaRecortadaDto> PersonalRecortado,
    IReadOnlyList<DiasAgregadosDto> DiasAgregados,
    IReadOnlyList<ActividadResultanteDto> Actividades,
    IReadOnlyList<string> Advertencias);

/// <summary>200 de POST /api/proyectos/{id}/cabecera.</summary>
public sealed record CabeceraActualizadaDto(int Id, int Version);

public sealed record ResultadoCabecera(
    EstadoEdicion Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    PrevisualizacionCabeceraDto? Previsualizacion = null,
    CabeceraActualizadaDto? Realizado = null)
{
    public static ResultadoCabecera Invalido(IReadOnlyDictionary<string, string[]> e) => new(EstadoEdicion.Invalido, e);
    public static ResultadoCabecera NoEncontrado() => new(EstadoEdicion.NoEncontrado);
    public static ResultadoCabecera Cambiado() => new(EstadoEdicion.Cambiado);
    public static ResultadoCabecera Previsualizado(PrevisualizacionCabeceraDto p) => new(EstadoEdicion.Previsualizado, Previsualizacion: p);
    public static ResultadoCabecera Hecho(CabeceraActualizadaDto r) => new(EstadoEdicion.Realizado, Realizado: r);
}
