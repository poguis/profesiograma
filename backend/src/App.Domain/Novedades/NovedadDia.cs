using App.Domain.Comun;

namespace App.Domain.Novedades;

public class NovedadDia : EntidadAuditable
{
    public long Id { get; set; }
    public int NovedadId { get; set; }
    public Novedad Novedad { get; set; } = null!;
    public DateOnly Fecha { get; set; }
}
