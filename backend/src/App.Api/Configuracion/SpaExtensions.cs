using Microsoft.AspNetCore.StaticFiles;

namespace App.Api.Configuracion;

/// <summary>
/// TAREA-7d-1 (D2): la SPA (build de Vite copiado a wwwroot por publicar-qa) se sirve desde la API, con un mismo origen.
///  - UseSpaEstaticos va ANTES de UseRouting y de la seguridad: los archivos no pasan por la política Fallback (que exige
///    token) ni por los endpoints de fallback.
///  - MapSpa: /api/** desconocido → 404 sin HTML; cualquier otra ruta sin archivo → index.html (rutas de React Router).
///  - Caché (J2, las mismas opciones en los dos): /assets/* (nombres con hash) un año e immutable; el resto (index.html)
///    no-cache, para que una publicación nueva se vea sin borrar la caché.
/// En DEV no hay wwwroot: los archivos no existen y / responde 404 (la SPA la sirve Vite).
/// </summary>
public static class SpaExtensions
{
    public const string CacheAssets = "public, max-age=31536000, immutable";
    public const string CacheSinCache = "no-cache";

    public static StaticFileOptions OpcionesArchivos() => new()
    {
        OnPrepareResponse = contexto =>
            contexto.Context.Response.Headers.CacheControl =
                contexto.Context.Request.Path.StartsWithSegments("/assets") ? CacheAssets : CacheSinCache,
    };

    public static WebApplication UseSpaEstaticos(this WebApplication app)
    {
        app.UseStaticFiles(OpcionesArchivos());
        return app;
    }

    public static IEndpointRouteBuilder MapSpa(this IEndpointRouteBuilder app)
    {
        app.MapFallback("/api/{**ruta}", () => Results.NotFound()).AllowAnonymous(); // J3
        // TAREA-7d-3: un archivo inexistente (último segmento con punto, p. ej. /assets/no-existe.js) → 404 anónimo; sin
        // esto la política Fallback respondía 401. Los archivos existentes ya los sirvió UseSpaEstaticos, que va ANTES de
        // UseRouting (si no, este endpoint taparía los archivos reales).
        app.MapFallback("{**ruta:file}", () => Results.NotFound()).AllowAnonymous();
        app.MapFallbackToFile("index.html", OpcionesArchivos()).AllowAnonymous();
        return app;
    }
}
