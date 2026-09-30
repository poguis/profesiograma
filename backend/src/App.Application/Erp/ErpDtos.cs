namespace App.Application.Erp;

/// <summary>Compañía del ERP.</summary>
/// <param name="Nombre">Razón social: tradeName ?? name (igual que la app original al guardar el proyecto).</param>
/// <param name="NombreCorto">name del ERP (lo que mostraba el desplegable de la app original).</param>
public sealed record CompaniaErp(int Id, string Nombre, string? NombreCorto, string? Ruc);

/// <summary>Proyecto del ERP. Solo se exponen los de status "Activo".</summary>
public sealed record ProyectoErp(string Id, string Nombre, string Estado);

/// <summary>Dimensión (UEGP) del ERP.</summary>
public sealed record DimensionErp(string UegpId, string Descripcion);

/// <summary>Actividad de un proyecto del ERP.</summary>
public sealed record ActividadErp(string Id, string Descripcion, string? Tipo);

/// <summary>Horario de nómina del ERP. Solo se exponen los de status "A".</summary>
/// <param name="MinutosJornada">"horas" del ERP (HHmm) convertido a minutos.</param>
/// <param name="MinutosTrabajados">"horasTrab" del ERP (HHmm) convertido a minutos.</param>
/// <param name="Tipo">tipoHorario del ERP (código, p. ej. "M" o "D").</param>
public sealed record HorarioErp(
    int Codigo,
    string Descripcion,
    TimeOnly? HoraEntrada,
    TimeOnly? HoraSalida,
    short? MinutosJornada,
    short? MinutosTrabajados,
    string? Tipo);
