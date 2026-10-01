using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Application;

public static class DependencyInjection
{
    /// <summary>Servicios de aplicación (reglas de consulta y validación). Los contratos se implementan en Infrastructure.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProyectoConsultaServicio>();
        services.AddScoped<EmpleadoConsultaServicio>();
        services.AddScoped<CatalogoErpConsultaServicio>();
        services.AddScoped<CrearProyectoValidador>();
        services.AddScoped<CrearProyectoServicio>();
        services.AddScoped<OpcionesFormularioProyectoServicio>();
        services.AddScoped<CambioEstadoValidador>();
        services.AddScoped<CambioEstadoServicio>();
        return services;
    }
}
