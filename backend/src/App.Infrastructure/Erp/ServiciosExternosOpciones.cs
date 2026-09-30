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
}
