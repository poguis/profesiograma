namespace App.Api.Configuracion;

/// <summary>
/// TAREA-7d-3 (K1, E2, E3): encabezados de seguridad en TODAS las respuestas (SPA, archivos, API, 401, 404 y 500).
/// Es el PRIMER middleware y usa OnStarting: los encabezados se ponen al empezar la respuesta, después de que
/// UseExceptionHandler la limpia para escribir el 500.
///  - X-Frame-Options SAMEORIGIN y frame-ancestors 'self' (nunca DENY ni 'none'): ssoSilent de MSAL carga nuestra propia
///    página en un iframe oculto del mismo origen.
///  - La CSP lleva SOLO frame-ancestors; la completa (Griffel, MSAL) queda para después (E4).
///  - HSTS solo con Seguridad:Hsts = true (por defecto false). En QA, con el certificado autofirmado, HSTS impediría
///    aceptar la advertencia del navegador; se activa en PRO con un certificado de confianza. UseHsts solo responde en
///    https y excluye localhost.
/// </summary>
public static class EncabezadosSeguridadExtensions
{
    public const string ClaveHsts = "Seguridad:Hsts";

    public static readonly IReadOnlyDictionary<string, string> Encabezados = new Dictionary<string, string>
    {
        ["X-Content-Type-Options"] = "nosniff",
        ["Referrer-Policy"] = "strict-origin-when-cross-origin",
        ["X-Frame-Options"] = "SAMEORIGIN",
        ["Content-Security-Policy"] = "frame-ancestors 'self'",
    };

    public static WebApplication UseEncabezadosSeguridad(this WebApplication app)
    {
        app.Use(async (contexto, siguiente) =>
        {
            contexto.Response.OnStarting(() =>
            {
                foreach (var (nombre, valor) in Encabezados)
                {
                    contexto.Response.Headers[nombre] = valor;
                }

                return Task.CompletedTask;
            });
            await siguiente(contexto);
        });

        if (app.Configuration.GetValue<bool>(ClaveHsts))
        {
            app.UseHsts();
        }

        return app;
    }
}
