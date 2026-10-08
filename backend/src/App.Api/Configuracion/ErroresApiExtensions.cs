namespace App.Api.Configuracion;

/// <summary>Excepciones → ProblemDetails (común a Program.cs y a App.Api.Tests).</summary>
public static class ErroresApiExtensions
{
    public static IServiceCollection AddErroresApi(this IServiceCollection services)
    {
        services.AddExceptionHandler<ErpNoDisponibleExceptionHandler>(); // ERP caído → 503
        services.AddExceptionHandler<RegistroOcupadoExceptionHandler>(); // applock de asignaciones no obtenido → 503
        services.AddExceptionHandler<SolicitudIlegibleExceptionHandler>(); // TAREA-26d-1: cuerpo ilegible → 400 en su campo
        services.AddProblemDetails();

        // TAREA-26d-1 (defecto V3): el enlace del cuerpo lanza BadHttpRequestException en TODOS los entornos (por defecto
        // solo en Development, donde terminaba en 500; en producción era un 400 sin cuerpo) y el manejador anterior la
        // convierte en 400 con el mensaje en español en su campo.
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        return services;
    }
}
