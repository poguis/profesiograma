using App.Domain.Comun;

namespace App.Domain.Configuracion;

/// <summary>Mapeo cargo → código Infor (se completa desde la interfaz).</summary>
public class CargoInfor : EntidadAuditable
{
    public int Id { get; set; }
    public string Cargo { get; set; } = string.Empty;
    public string? CodigoInfor { get; set; }
    public bool Activo { get; set; } = true;
}
