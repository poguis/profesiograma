using App.Domain.Comun;

namespace App.Domain.Catalogos;

public class EstadoProyecto : Catalogo
{
    /// <summary>Participa en la detección de cruces.</summary>
    public bool EsVigente { get; set; }
}

public class TipoMovimiento : Catalogo { }

public class GrupoProyecto : Catalogo
{
    public bool RequiereProyectoErp { get; set; }
    public bool RequiereDimension { get; set; }
}

public class Jornada : Catalogo
{
    public byte DiasTrabajo { get; set; }
    public byte DiasDescanso { get; set; }
}

public class RolAsignacion : Catalogo
{
    public bool EsDescanso { get; set; }
}

public class TipoAplicacionNovedad : Catalogo { }

public class OrigenNovedad : Catalogo
{
    /// <summary>Las novedades de origen externo son de solo lectura.</summary>
    public bool EsEditable { get; set; }
}

public class TipoNovedad : Catalogo
{
    public string Sigla { get; set; } = string.Empty;
    public string SiglaCronograma { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#000000";
    public int CategoriaId { get; set; }
    public string CategoriaDescripcion { get; set; } = string.Empty;
    public string EstadoReporte { get; set; } = string.Empty;
    public string ObservacionReporte { get; set; } = string.Empty;
    public bool AplicaPersona { get; set; }
    public bool AplicaProyecto { get; set; }
    public bool AplicaGeneral { get; set; }
    /// <summary>false = solo por integración (PERMISO MEDICO) o sistema (DESCANSO).</summary>
    public bool SeleccionableManual { get; set; }
}
