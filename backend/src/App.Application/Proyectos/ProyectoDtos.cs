namespace App.Application.Proyectos;

// ------------------------------------------------------------------ listado

/// <summary>Fila del listado de proyectos (fuente: vista dbo.vwProyectoResumen).</summary>
public sealed record ProyectoResumenDto(
    int Id, string Codigo, string Nombre, string Grupo, string Estado,
    DateOnly FechaInicio, DateOnly FechaFin,
    string? Responsable, IReadOnlyList<string> Backs,
    TimeOnly? HoraEntrada, TimeOnly? HoraSalida);

// ------------------------------------------------------------------ detalle

public sealed record CodigoNombreDto(string Codigo, string Nombre);

public sealed record ProyectoDetalleDto(
    int Id, Guid Uid, string Codigo, string Nombre,
    CompaniaResumenDto Compania,
    CodigoNombreDto Grupo, CodigoNombreDto Estado,
    DateOnly FechaInicio, DateOnly FechaFin,
    ProyectoErpDto Erp,
    string? Departamento,
    HorarioDto Horario, AlmuerzoDto Almuerzo,
    PropietarioDto Propietario,
    IReadOnlyList<ProyectoPersonalDto> Personal,
    IReadOnlyList<ProyectoEtapaDto> Etapas,
    ProyectoActividadDto? ActividadVigente);

public sealed record CompaniaResumenDto(int Id, string Nombre, string? Ruc);

public sealed record ProyectoErpDto(
    string? ProyectoErpId, string? ProyectoErpNombre, string? ProyectoErpEstado,
    string? DimensionUegpId, string? DimensionDescripcion);

public sealed record HorarioDto(
    int? Codigo, string? Descripcion, TimeOnly? HoraEntrada, TimeOnly? HoraSalida,
    string? Tipo, short? MinutosJornada, short? MinutosTrabajados);

public sealed record AlmuerzoDto(TimeOnly? Salida, TimeOnly? Regreso);

public sealed record PropietarioDto(int Id, string NombreMostrar, string Email);

/// <summary>Personal asignado. Rol = PRINCIPAL | BACK. Cargo = cargo asignado o, si falta, el puesto del empleado.</summary>
public sealed record ProyectoPersonalDto(
    int Id, string Rol, short Numero,
    EmpleadoResumenDto Empleado, string? Cargo,
    CodigoNombreDto? Jornada, byte? DiasTrabajo, byte DiasDescanso,
    DateOnly FechaInicio, DateOnly FechaFin,
    bool EsPrincipalInicial, string TipoRegistro, string? Observacion);

/// <summary>Solo datos no sensibles del empleado (sin cédula ni correo).</summary>
public sealed record EmpleadoResumenDto(int Id, string CodigoEkon, string NombreCompleto);

/// <summary>Etapa del historial. No expone SnapshotPersonal (JSON con datos del personal).</summary>
public sealed record ProyectoEtapaDto(
    int Version, string TipoMovimiento, string Estado,
    DateOnly FechaInicio, DateOnly FechaFin, DateOnly? FechaCorte,
    string? ActividadCodigo, DateTime FechaRegistroUtc);

public sealed record ProyectoActividadDto(
    int Version, string TipoMovimiento, string ActividadCodigo, string? ActividadDescripcion,
    string? ActividadTipo, DateOnly FechaInicio, DateOnly FechaFin);
