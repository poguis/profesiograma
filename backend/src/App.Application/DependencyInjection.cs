using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Novedades;
using App.Application.Novedades.Sincronizacion;
using App.Application.Proyectos;
using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
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
        services.AddScoped<EdicionPersonalValidador>();
        services.AddScoped<EdicionPersonalServicio>();
        services.AddScoped<ComparacionErpServicio>(); // TAREA-26d-3
        services.AddScoped<ReactivacionValidador>();
        services.AddScoped<ReactivacionServicio>();
        services.AddScoped<EdicionCabeceraValidador>();
        services.AddScoped<EdicionCabeceraServicio>();
        services.AddScoped<CronogramaConsultaServicio>(); // TAREA-20
        services.AddScoped<CompletarCedulasServicio>(); // TAREA-22-1
        services.AddScoped<CriterioPersonalServicio>(); // TAREA-62
        services.AddScoped<ReporteProyectoServicio>(); // TAREA-22-2
        services.AddScoped<NovedadConsultaServicio>(); // TAREA-23-1
        services.AddScoped<NovedadValidador>();
        services.AddScoped<NovedadEscrituraServicio>();
        services.AddScoped<OpcionesFormularioNovedad>();
        services.AddScoped<NovedadModificacionServicio>(); // TAREA-23-2
        services.AddScoped<SincronizacionPermisosMedicosServicio>(); // TAREA-23-3
        return services;
    }
}
