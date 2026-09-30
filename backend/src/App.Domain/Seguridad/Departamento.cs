using App.Domain.Comun;

namespace App.Domain.Seguridad;

public class Departamento : EntidadAuditable
{
    public const string TipoDepartamento = "DEPARTAMENTO";
    public const string TipoUnidad = "UNIDAD";

    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string NombreCorto { get; set; } = string.Empty;
    public string Tipo { get; set; } = TipoDepartamento;
    public string? CodigoErp { get; set; }
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
}
