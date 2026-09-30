using App.Domain.Comun;

namespace App.Domain.Maestros;

/// <summary>Caché de compañías ERP. Id = companyId del ERP (no identity).</summary>
public class Compania : EntidadAuditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? NombreComercial { get; set; }
    public string? Ruc { get; set; }
}
