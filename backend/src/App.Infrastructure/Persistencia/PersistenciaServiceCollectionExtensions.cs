using App.Application.Catalogos;
using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Empleados;
using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Proyectos;
using App.Application.Seguridad;
using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.Auditoria;
using App.Infrastructure.Persistencia.DatosPrueba;
using App.Infrastructure.Persistencia.Empleados;
using App.Infrastructure.Persistencia.Proyectos;
using App.Infrastructure.Reportes;
using App.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            // TAREA-22c: base caída → BaseDatosNoDisponibleException (503 en la API).
            opciones.AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>(), FalloBaseDatosInterceptor.Instancia);
            // El evento ConnectionError de EF nombra la base y el servidor: solo en Debug (el 503 se registra en la API).
            opciones.ConfigureWarnings(w => w.Log((RelationalEventId.ConnectionError, LogLevel.Debug)));
        });

        services.AddScoped<ICatalogoConsultas, CatalogoConsultas>();
        services.AddScoped<IProyectoConsultas, ProyectoConsultas>();
        services.AddScoped<IEmpleadoConsultas, EmpleadoConsultas>();
        services.AddScoped<IDatosReferenciaProyecto, DatosReferenciaProyecto>();
        services.AddScoped<IConsultaCrucesExternos, ConsultaCrucesExternos>();
        services.AddScoped<ICronogramaConsultas, CronogramaConsultas>(); // TAREA-20
        services.AddSingleton<IEscritorReporteExcel, EscritorReporteExcel>(); // TAREA-22-2 (sin estado)
        services.AddScoped<IProyectoRepositorio, ProyectoRepositorio>();
        services.AddScoped<ITransaccionAsignaciones, TransaccionAsignaciones>();
        services.AddScoped<ICambioEstadoRepositorio, CambioEstadoRepositorio>();
        services.AddScoped<IEdicionPersonalRepositorio, EdicionPersonalRepositorio>();
        services.AddScoped<IReactivacionRepositorio, EdicionPersonalRepositorio>();
        services.AddScoped<ICabeceraRepositorio, EdicionCabeceraRepositorio>();
        services.AddScoped<IUsuarioProvisionamiento, UsuarioProvisionamiento>();
        services.AddScoped<ICompletarCedulasRepositorio, CompletarCedulasRepositorio>(); // TAREA-22-1
        services.AddScoped<DatosPruebaSembrador>();

        return services;
    }
}
