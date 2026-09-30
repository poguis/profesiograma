using App.Domain.Comun;

namespace App.Domain.Configuracion;

public class Parametro : EntidadAuditable
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    /// <summary>INT | BOOL | TEXT | LIST</summary>
    public string TipoDato { get; set; } = "TEXT";
    public string? Descripcion { get; set; }
}
