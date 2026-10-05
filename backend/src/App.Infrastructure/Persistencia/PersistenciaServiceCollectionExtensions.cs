using App.Application.Catalogos;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos;
using App.Application.Seguridad;
using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.Auditoria;
using App.Infrastructure.Persistencia.DatosPrueba;
using App.Infrastructure.Persistencia.Proyectos;
using App.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace App.Infrastructure.Persistencia;

public static class PersistenciaServiceCollectionExtensions
{
    /// <summary>
    /// Registra EF Core (SQL Server 2019, nivel 150), auditoría y servicios de datos.
    /// </summary>
    public static IServiceCollection AddPersistencia(this IServiceCollection services, string cadenaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddScoped<AuditoriaInterceptor>();

        services.AddDbContext<ProfesiogramaDbContext>((sp, opciones) =>
        {
            opciones.UseSqlServer(cadenaConexion, sql =>
            {
                sql.UseCompatibilityLevel(150); // NIQUEL\SSDEV: SQL Server 2019, compatibilidad 150
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                sql.CommandTimeout(60);
            });
            opciones.AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>());
        });

        services.AddScoped<ICatalogoConsultas, CatalogoConsultas>();
        services.AddScoped<IProyectoConsultas, ProyectoConsultas>();
        services.AddScoped<IEmpleadoConsultas, EmpleadoConsultas>();
        services.AddScoped<IDatosReferenciaProyecto, DatosReferenciaProyecto>();
        services.AddScoped<IConsultaCrucesExternos, ConsultaCrucesExternos>();
        services.AddScoped<IProyectoRepositorio, ProyectoRepositorio>();
        services.AddScoped<ITransaccionAsignaciones, TransaccionAsignaciones>();
        services.AddScoped<ICambioEstadoRepositorio, CambioEstadoRepositorio>();
        services.AddScoped<IEdicionPersonalRepositorio, EdicionPersonalRepositorio>();
        services.AddScoped<IUsuarioProvisionamiento, UsuarioProvisionamiento>();
        services.AddScoped<DatosPruebaSembrador>();

        return services;
    }
}
