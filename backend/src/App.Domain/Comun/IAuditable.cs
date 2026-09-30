namespace App.Domain.Comun;

/// <summary>
/// Campos de auditoría estándar. Las marcas de tiempo se guardan en UTC.
/// Los asigna automáticamente AuditoriaInterceptor (Infrastructure).
/// </summary>
public interface IAuditable
{
    int CreadoPorId { get; set; }
    DateTime FechaCreacion { get; set; }
    int? ModificadoPorId { get; set; }
    DateTime? FechaModificacion { get; set; }
}
