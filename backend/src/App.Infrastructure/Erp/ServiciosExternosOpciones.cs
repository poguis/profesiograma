namespace App.Infrastructure.Erp;

/// <summary>Sección "ServiciosExternos" de appsettings.</summary>
public sealed class ServiciosExternosOpciones
{
    public const string Seccion = "ServiciosExternos";
    public const string ModoHttp = "Http";
    public const string ModoSimulado = "Simulado";

    /// <summary>Http (APIs reales del ERP) o Simulado (datos fijos; solo Development).</summary>
    public string Modo { get; init; } = ModoHttp;

    /// <summary>Base de las APIs del puerto 7048 (compañías, proyectos, dimensiones).</summary>
    public string ErpBase7048 { get; init; } = string.Empty;

    /// <summary>Base de las APIs del puerto 7055 (actividades, horarios).</summary>
    public string ErpBase7055 { get; init; } = string.Empty;

    public int TimeoutSegundos { get; init; } = 15;

    /// <summary>Tiempo de espera de la API EvolutionEmployee (respuesta de ~2,5 MB).</summary>
    public int TimeoutEmpleadosSegundos { get; init; } = 60;

    /// <summary>TAREA-26d (P3): duración de la lista de empleados en memoria (buscador y vista previa).</summary>
    public int CacheEmpleadosMinutos { get; init; } = 10;

    /// <summary>TAREA-26d (P4): si la API no responde, el buscador usa la lista anterior mientras tenga menos de esto.</summary>
    public int CacheEmpleadosMaxAntiguedadMinutos { get; init; } = 60;
}
