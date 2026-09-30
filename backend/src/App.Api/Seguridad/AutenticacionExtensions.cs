using App.Api.Seguridad.DevAuth;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace App.Api.Seguridad;

public static class AutenticacionExtensions
{
    public const string ModoDevAuth = "DevAuth";
    public const string ModoEntraId = "EntraId";

    /// <summary>
    /// Autenticación según "Autenticacion:Modo":
    ///  - DevAuth: usuarios simulados (SOLO Development; en otro entorno la API no arranca).
    ///  - EntraId (por defecto): tokens de Entra ID validados con Microsoft.Identity.Web (sección "AzureAd").
    /// Autorización: por defecto se exige usuario autenticado (FallbackPolicy).
    /// </summary>
    public static WebApplicationBuilder AddSeguridadProfesiograma(this WebApplicationBuilder builder)
    {
        var modo = builder.Configuration["Autenticacion:Modo"] ?? ModoEntraId;

        if (string.Equals(modo, ModoDevAuth, StringComparison.OrdinalIgnoreCase))
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"DevAuth solo está permitido en Development. Entorno actual: {builder.Environment.EnvironmentName}.");
            }

            builder.Services
                .AddAuthentication(DevAuthDefaults.Esquema)
                .AddScheme<DevAuthOptions, DevAuthHandler>(DevAuthDefaults.Esquema,
                    opciones => builder.Configuration.GetSection("DevAuth").Bind(opciones));
        }
        else
        {
            builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd");
        }

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Admin, p => p.RequireRole(RolesApp.Admin))
            .AddPolicy(Politicas.Gestor, p => p.RequireRole(RolesApp.Admin, RolesApp.Gestor));

        builder.Services.AddScoped<UsuarioActual>();
        builder.Services.AddScoped<IUsuarioActual>(sp => sp.GetRequiredService<UsuarioActual>());

        return builder;
    }

    /// <summary>UseAuthentication → UsuarioActualMiddleware → UseAuthorization.</summary>
    public static WebApplication UseSeguridadProfesiograma(this WebApplication app)
    {
        var modo = app.Configuration["Autenticacion:Modo"] ?? ModoEntraId;
        if (string.Equals(modo, ModoDevAuth, StringComparison.OrdinalIgnoreCase))
        {
            app.Logger.LogWarning("ATENCIÓN: autenticación SIMULADA (DevAuth) activa. Solo para desarrollo local.");
        }

        app.UseAuthentication();
        app.UseMiddleware<UsuarioActualMiddleware>();
        app.UseAuthorization();
        return app;
    }
}
