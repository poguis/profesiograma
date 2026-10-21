using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Domain.Catalogos;

namespace App.Application.Novedades;

// Novedades (TAREA-23-1): lectura, opciones y creación. Ver docs/fases/FASE_5_Novedades.md.

/// <summary>Códigos de TipoAplicacionNovedad (Ids fijos de CatalogoIds).</summary>
public static class AplicacionNovedad
{
    public const string Persona = "PERSONA", Proyecto = "PROYECTO", General = "GENERAL";

    public static string Codigo(byte id) => id switch
    {
        CatalogoIds.TipoAplicacionNovedad.Persona => Persona,
        CatalogoIds.TipoAplicacionNovedad.Proyecto => Proyecto,
        CatalogoIds.TipoAplicacionNovedad.General => General,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Tipo de aplicación desconocido."),
    };

    /// <summary>Id por código (sin distinguir mayúsculas ni espacios extremos); null si no es PERSONA, PROYECTO ni GENERAL.</summary>
    public static byte? Id(string? codigo) => LectorParametros.Normalizar(codigo)?.ToUpperInvariant() switch
    {
        Persona => CatalogoIds.TipoAplicacionNovedad.Persona,
        Proyecto => CatalogoIds.TipoAplicacionNovedad.Proyecto,
        General => CatalogoIds.TipoAplicacionNovedad.General,
        _ => null,
    };
}

// ------------------------------------------------------------------ solicitudes

/// <summary>
/// Cuerpo de POST /api/novedades. tipoNovedad = código de TipoNovedad (p. ej. "VACACIONES"). PERSONA: codigoEkon;
/// PROYECTO: proyectoId; GENERAL: ninguno. Fechas yyyy-MM-dd (un valor ilegible lo responde SolicitudIlegibleExceptionHandler).
/// </summary>
public sealed record CrearNovedadSolicitud(
    string? TipoAplicacion,
    string? TipoNovedad,
    string? CodigoEkon,
    int? ProyectoId,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? Observacion);

/// <summary>Query string del listado, como texto para devolver los errores de formato en español.</summary>
public sealed record NovedadListadoSolicitud(
    string? Aplicacion, string? Tipo, string? Origen, string? Texto,
    string? Desde, string? Hasta, string? IncluirAnuladas,
    string? Pagina, string? Tamano);

/// <summary>Filtro ya validado. Null = sin filtro.</summary>
/// <param name="VisibleParaUsuarioId">J2: null = todas (Admin); si no, solo las visibles para ese usuario (<see cref="PermisosNovedad.VisiblePara"/>).</param>
public sealed record NovedadFiltro(
    byte? AplicacionId, string? TipoCodigo, string? OrigenCodigo, string? Texto,
    DateOnly? Desde, DateOnly? Hasta, bool IncluirAnuladas,
    int Pagina, int Tamano,
    int? VisibleParaUsuarioId);

// ------------------------------------------------------------------ DTO

public sealed record TipoNovedadResumenDto(string Codigo, string Nombre, string Sigla, string Color);

public sealed record EmpleadoSujetoDto(string CodigoEkon, string Nombre);

public sealed record ProyectoSujetoDto(int Id, string Codigo, string Nombre);

/// <summary>Sujeto de la novedad: el empleado (PERSONA) o el proyecto (PROYECTO). GENERAL: el sujeto es null.</summary>
public sealed record SujetoNovedadDto(EmpleadoSujetoDto? Empleado, ProyectoSujetoDto? Proyecto);

public sealed record NovedadResumenDto(
    int Id,
    string Codigo,
    string Aplicacion,
    TipoNovedadResumenDto Tipo,
    string Origen,
    SujetoNovedadDto? Sujeto,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int TotalDias,
    bool Anulada,
    DateTime FechaCreacion);

/// <summary>Registrante (RegistradoPorNombre/Correo). Solo lo ve Admin.</summary>
public sealed record RegistroNovedadDto(string? Nombre, string? Correo);

/// <summary>
/// Detalle. Version = RowVer en Base64 (token de concurrencia de la 23-2). PuedeEditar/PuedeAnular/MotivoNoEditable:
/// N3 (<see cref="PermisosNovedad.Modificacion"/>). DetalleOrigenJson no viaja nunca.
/// </summary>
public sealed record NovedadDetalleDto(
    int Id,
    string Codigo,
    string Aplicacion,
    TipoNovedadResumenDto Tipo,
    string Origen,
    SujetoNovedadDto? Sujeto,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<DateOnly> Dias,
    string? Observacion,
    bool Anulada,
    DateTime? FechaAnulacion,
    string? MotivoAnulacion,
    RegistroNovedadDto? Registro,
    DateTime FechaCreacion,
    string Version,
    bool PuedeEditar,
    bool PuedeAnular,
    string? MotivoNoEditable);

public sealed record NovedadCreadaDto(int Id, string Codigo, string Version);

/// <summary>Opciones del formulario. TiposPorAplicacion: PERSONA, PROYECTO y GENERAL (vacío sin permiso).</summary>
public sealed record OpcionesFormularioNovedadDto(
    bool PuedeRegistrarGeneral,
    IReadOnlyDictionary<string, IReadOnlyList<TipoNovedadResumenDto>> TiposPorAplicacion,
    int MaxDias);

// ------------------------------------------------------------------ datos internos

/// <summary>Tipo de novedad activo con sus banderas (RN16).</summary>
public sealed record TipoNovedadRef(
    byte Id, string Codigo, string Nombre, string Sigla, string Color,
    bool AplicaPersona, bool AplicaProyecto, bool AplicaGeneral, bool SeleccionableManual)
{
    /// <summary>RN16: elegible a mano para esa aplicación (SeleccionableManual y la bandera de la aplicación).</summary>
    public bool EsElegible(byte aplicacionId) => SeleccionableManual && aplicacionId switch
    {
        CatalogoIds.TipoAplicacionNovedad.Persona => AplicaPersona,
        CatalogoIds.TipoAplicacionNovedad.Proyecto => AplicaProyecto,
        CatalogoIds.TipoAplicacionNovedad.General => AplicaGeneral,
        _ => false,
    };

    public TipoNovedadResumenDto Resumen() => new(Codigo, Nombre, Sigla, Color);
}

/// <summary>Detalle tal como sale de la base; Application calcula los permisos y decide qué se expone.</summary>
public sealed record NovedadDetalleDatos(
    int Id,
    string Codigo,
    byte AplicacionId,
    TipoNovedadResumenDto Tipo,
    string OrigenCodigo,
    bool OrigenEditable,
    SujetoNovedadDto? Sujeto,
    int? PropietarioProyectoId,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<DateOnly> Dias,
    string? Observacion,
    bool Anulada,
    DateTime? FechaAnulacion,
    string? MotivoAnulacion,
    string? RegistradoPorNombre,
    string? RegistradoPorCorreo,
    int CreadoPorId,
    DateTime FechaCreacion,
    byte[] RowVer);

/// <summary>
/// Solicitud validada. Empleado (PERSONA): Id real si tiene fila; temporal negativo si no (la alta puntual lo crea).
/// Cedulas (D15): solo de la lectura fresca, para la alta puntual; su ToString no muestra valores.
/// </summary>
public sealed record NovedadValidada(
    byte AplicacionId,
    TipoNovedadRef Tipo,
    EmpleadoAsignable? Empleado,
    int? ProyectoId,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion,
    CedulasEmpleados Cedulas);

/// <summary>Lo que se guarda: la novedad, su código y Uid, los días y el registrante (usuario actual).</summary>
public sealed record NuevaNovedad(
    NovedadValidada Datos, string Codigo, Guid Uid, IReadOnlyList<DateOnly> Dias,
    string? RegistradoPorNombre, string? RegistradoPorCorreo);

/// <summary>
/// N7: mismo sujeto, misma aplicación, mismo tipo y al menos un día en común. Creación: días = rango completo [Desde, Hasta].
/// Edición (TAREA-23-2): <paramref name="Dias"/> = días resultantes (con huecos) y <paramref name="ExcluirNovedadId"/> = la propia.
/// </summary>
public sealed record CriterioDuplicado(byte AplicacionId, byte TipoNovedadId, int? EmpleadoId, int? ProyectoId, DateOnly Desde, DateOnly Hasta,
    IReadOnlyCollection<DateOnly>? Dias = null, int? ExcluirNovedadId = null);

/// <summary>Novedad existente que duplica; Visible = la ve quien registra (R6: solo entonces se informa su código).</summary>
public sealed record DuplicadoNovedad(string Codigo, bool Visible);

// ------------------------------------------------------------------ puertos (Infrastructure)

public interface INovedadConsultas
{
    Task<PaginaResultado<NovedadResumenDto>> ListarAsync(NovedadFiltro filtro, CancellationToken ct);

    /// <summary>Null si no existe o no es visible (<paramref name="visibleParaUsuarioId"/> null = Admin).</summary>
    Task<NovedadDetalleDatos?> ObtenerDetalleAsync(int id, int? visibleParaUsuarioId, CancellationToken ct);

    /// <summary>Tipos de novedad activos, por Orden.</summary>
    Task<IReadOnlyList<TipoNovedadRef>> ObtenerTiposAsync(CancellationToken ct);

    /// <summary>Proyecto no eliminado, en estado vigente y visible (R1: <paramref name="propietarioUsuarioId"/> null = Admin).</summary>
    Task<bool> ProyectoElegibleAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct);
}

/// <summary>Escritura de novedades. Se usa DENTRO de ITransaccionAsignaciones.</summary>
public interface INovedadRepositorio
{
    /// <summary>N7: una novedad NO anulada que duplica la nueva; null si no hay.</summary>
    Task<DuplicadoNovedad?> BuscarDuplicadoAsync(CriterioDuplicado criterio, int? visibleParaUsuarioId, CancellationToken ct);

    /// <summary>R7: ¿ya existe una novedad con ese código o ese Uid?</summary>
    Task<bool> ExisteCodigoOUidAsync(string codigo, Guid uid, CancellationToken ct);

    /// <summary>Alta puntual (PERSONA sin fila o con datos frescos), novedad y sus días. Devuelve Id, código y versión.</summary>
    /// <exception cref="Proyectos.Estados.ConflictoConcurrenciaException">La alta puntual chocó con otro registro (409).</exception>
    Task<NovedadCreadaDto> AgregarAsync(NuevaNovedad nueva, CancellationToken ct);
}

// ------------------------------------------------------------------ resultado

public enum EstadoCrearNovedad
{
    Creado,
    Invalido,
    /// <summary>GENERAL sin permiso (403).</summary>
    Prohibido,
    /// <summary>N7 (409).</summary>
    Duplicado,
    /// <summary>La alta puntual chocó con otro registro simultáneo (409).</summary>
    Cambiado,
}

public sealed record ResultadoCrearNovedad(
    EstadoCrearNovedad Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    NovedadCreadaDto? Creado = null,
    string? CodigoDuplicado = null)
{
    public const string MensajeProhibido = "Solo los administradores pueden registrar novedades GENERAL.";
    public const string MensajeDuplicado = "Ya existe una novedad de ese tipo para ese sujeto en esas fechas.";
    public const string MensajeCambiado = ResultadoCrearProyecto.MensajeCambiado;

    public static ResultadoCrearNovedad Invalido(IReadOnlyDictionary<string, string[]> errores) => new(EstadoCrearNovedad.Invalido, errores);
    public static ResultadoCrearNovedad Prohibido() => new(EstadoCrearNovedad.Prohibido);
    public static ResultadoCrearNovedad Registrado(NovedadCreadaDto creado) => new(EstadoCrearNovedad.Creado, Creado: creado);
    /// <summary>R6: el código solo si la novedad existente es visible para quien registra.</summary>
    public static ResultadoCrearNovedad Duplicado(DuplicadoNovedad d) => new(EstadoCrearNovedad.Duplicado, CodigoDuplicado: d.Visible ? d.Codigo : null);
    public static ResultadoCrearNovedad Cambiado() => new(EstadoCrearNovedad.Cambiado);
}
