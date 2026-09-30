namespace App.Domain.Comun;

/// <summary>
/// Base de catálogos con Id fijo (TINYINT) y Codigo estable usado por el código.
/// </summary>
public abstract class Catalogo : EntidadAuditable
{
    public byte Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public byte Orden { get; set; }
    public bool Activo { get; set; } = true;
}
