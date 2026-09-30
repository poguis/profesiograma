namespace App.Domain.Comun;

public abstract class EntidadAuditable : IAuditable
{
    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int? ModificadoPorId { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
