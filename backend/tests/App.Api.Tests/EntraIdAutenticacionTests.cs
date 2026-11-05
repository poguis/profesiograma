using System.Security.Claims;
using System.Security.Cryptography;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace App.Api.Tests;

/// <summary>
/// Fase 7b: modo EntraId con Microsoft.Identity.Web REAL y tokens firmados con una clave RSA LOCAL, en un pipeline EN MEMORIA.
/// Nunca se llama a Entra ID: los metadatos OpenID se reemplazan por una configuración estática y el validador de emisor
/// de Microsoft.Identity.Web (que consulta login.microsoftonline.com) por ValidIssuer. Tenant, ClientId y oid son FICTICIOS.
/// </summary>
public sealed class EntraIdAutenticacionTests
{
    private const string Tenant = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string ClientId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string Emisor = $"https://login.microsoftonline.com/{Tenant}/v2.0";
    private const string EmisorV1 = $"https://sts.windows.net/{Tenant}/";
    private static readonly Guid OidActivo = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid OidInactivo = Guid.Parse("cccccccc-0000-0000-0000-000000000004");
    private static readonly Guid OidConflicto = Guid.Parse("cccccccc-0000-0000-0000-000000000005");

    private static readonly RsaSecurityKey Clave = new(RSA.Create(2048)) { KeyId = "clave-prueba" };
    private static readonly RsaSecurityKey OtraClave = new(RSA.Create(2048)) { KeyId = "clave-prueba" };

    [Fact]
    public async Task E1_SinToken_401()
    {
        var (estado, _, prov) = await EnviarAsync("/api/usuarios/me", token: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, estado);
        Assert.Equal(0, prov.Llamadas);
    }

    [Fact]
    public async Task E2a_AudGuidDelClientId_Admin_200ConRoles()
    {
        var (estado, cuerpo, _) = await EnviarAsync("/api/usuarios/me", Token(roles: ["Admin"]));

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Contains("\"roles\":[\"Admin\"]", cuerpo);
        Assert.Contains("\"email\":\"persona.prueba@sedemi.com\"", cuerpo);
    }

    [Fact]
    public async Task E2b_AudApiClientId_TokenV1_200()
    {
        var (estado, _, _) = await EnviarAsync("/api/usuarios/me",
            Token(roles: ["Gestor"], aud: $"api://{ClientId}", iss: EmisorV1, version: "1.0"));

        Assert.Equal(StatusCodes.Status200OK, estado);
    }

    /// <summary>Microsoft.Identity.Web: un token v2 siempre trae como aud el GUID; con api://ClientId lo rechaza.</summary>
    [Fact]
    public async Task E2c_AudApiClientId_TokenV2_401() =>
        Assert.Equal(StatusCodes.Status401Unauthorized,
            (await EnviarAsync("/api/usuarios/me", Token(roles: ["Gestor"], aud: $"api://{ClientId}"))).Estado);

    [Fact]
    public async Task E3_Gestor_EnEndpointAdmin_403()
    {
        var (estado, _, _) = await EnviarAsync("/api/admin/usuarios", Token(roles: ["Gestor"]));

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
    }

    [Fact]
    public async Task E4_AudAjeno_401() =>
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("/api/usuarios/me", Token(roles: ["Admin"], aud: "api://otra-api"))).Estado);

    [Fact]
    public async Task E5_EmisorDeOtroTenant_401() =>
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("/api/usuarios/me",
            Token(roles: ["Admin"], iss: "https://login.microsoftonline.com/dddddddd-0000-0000-0000-000000000009/v2.0"))).Estado);

    [Fact]
    public async Task E6_Vencido_401() =>
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("/api/usuarios/me",
            Token(roles: ["Admin"], vence: DateTime.UtcNow.AddHours(-1)))).Estado);

    [Fact]
    public async Task E7_FirmadoConOtraClave_401() =>
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("/api/usuarios/me", Token(roles: ["Admin"], clave: OtraClave))).Estado);

    [Fact]
    public async Task E8_TokenDeAplicacion_ConRolesSinScp_403()
    {
        var (estado, _, _) = await EnviarAsync("/api/admin/usuarios", Token(roles: ["Admin"], scp: null));

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
    }

    [Fact]
    public async Task E9_SinRol_403SinRolAsignado_YNoSeDaDeAlta()
    {
        var (estado, cuerpo, prov) = await EnviarAsync("/api/usuarios/me", Token(roles: []));

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Contains("\"title\":\"Sin rol asignado\"", cuerpo);
        Assert.Contains("Pida a TI que le asigne el rol Gestor o Admin en Entra ID.", cuerpo);
        Assert.Equal(0, prov.Llamadas);
    }

    [Fact]
    public async Task E10_Inactivo_403UsuarioNoHabilitado()
    {
        var (estado, cuerpo, _) = await EnviarAsync("/api/usuarios/me", Token(roles: ["Gestor"], oid: OidInactivo));

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Contains("Usuario no habilitado", cuerpo);
        Assert.Contains("El usuario está inactivo en la aplicación.", cuerpo);
    }

    [Fact]
    public async Task E11_SinPreferredUsername_403TokenSinCorreo()
    {
        var (estado, cuerpo, prov) = await EnviarAsync("/api/usuarios/me", Token(roles: ["Gestor"], correo: null));

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Contains("Token sin correo", cuerpo);
        Assert.Equal(0, prov.Llamadas);
    }

    [Fact]
    public async Task E12_CabeceraDevAuth_EnModoEntraId_401()
    {
        var (estado, _, _) = await EnviarAsync("/api/usuarios/me", token: null, devUser: "admin");

        Assert.Equal(StatusCodes.Status401Unauthorized, estado);
    }

    [Fact]
    public void E13_DevAuth_FueraDeDevelopment_NoArranca()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Autenticacion:Modo"] = "DevAuth" });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddSeguridadProfesiograma());
        Assert.Contains("DevAuth solo está permitido en Development", ex.Message);
    }

    /// <summary>TAREA-7d-1: en modo EntraId, TenantId y ClientId deben ser GUID (marcadores de appsettings.json o vacíos: no arranca).</summary>
    [Theory]
    [InlineData("[PENDIENTE: Tenant ID de la empresa]", ClientId, "AzureAd:TenantId")]
    [InlineData(Tenant, null, "AzureAd:ClientId")]
    [InlineData("", "", "AzureAd:TenantId y AzureAd:ClientId")]
    public void E15_EntraId_SinGuid_NoArranca_SinMostrarElValor(string? tenant, string? clientId, string faltan)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "QA" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "EntraId",
            ["AzureAd:TenantId"] = tenant,
            ["AzureAd:ClientId"] = clientId,
        });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddSeguridadProfesiograma());
        Assert.Contains($"requiere un GUID en {faltan} (entorno QA)", ex.Message);
        Assert.DoesNotContain("PENDIENTE", ex.Message);
    }

    [Fact]
    public void E15b_DevAuth_NoExigeAzureAd()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "DevAuth",
            ["AzureAd:TenantId"] = "[PENDIENTE: Tenant ID de la empresa]",
        });

        builder.AddSeguridadProfesiograma(); // no lanza
    }

    [Fact]
    public async Task E14_DevAuth_EmiteScp_YPasaLasMismasPoliticas()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "DevAuth",
            ["DevAuth:Usuarios:0:Clave"] = "admin",
            ["DevAuth:Usuarios:0:ObjectId"] = OidActivo.ToString(),
            ["DevAuth:Usuarios:0:Email"] = "admin.dev@profesiograma.local",
            ["DevAuth:Usuarios:0:Roles:0"] = "Admin",
        });

        var (estado, _, _) = await EjecutarAsync(builder, "/api/usuarios/me", token: null, devUser: "admin");

        Assert.Equal(StatusCodes.Status200OK, estado);
    }

    [Fact]
    public void Claims_CortosYMapeados_SeLeenIgual()
    {
        var corto = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("oid", OidActivo.ToString()), new Claim("preferred_username", "a@sedemi.com"), new Claim("roles", "Admin")], "x"));
        var mapeado = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimNames.ObjectIdLargo, OidActivo.ToString()), new Claim(ClaimTypes.Upn, "a@sedemi.com"), new Claim(ClaimTypes.Role, "Admin")], "x"));

        foreach (var p in new[] { corto, mapeado })
        {
            Assert.Equal(OidActivo, p.ObtenerObjectId());
            Assert.Equal("a@sedemi.com", p.ObtenerUpn());
            Assert.Equal("a@sedemi.com", p.ObtenerCorreo()); // sin claim email: el UPN
            Assert.Equal(["Admin"], p.ObtenerRoles());
        }
    }

    // ------------------------------------------------------------------ TAREA-7b-2: UPN distinto del correo

    [Fact]
    public async Task UpnDistintoDelCorreo_MeDevuelveElCorreo_YAlAltaVanLosDos()
    {
        var (estado, cuerpo, prov) = await EnviarAsync("/api/usuarios/me",
            Token(roles: ["Gestor"], correo: "upn.prueba@sedemi.com", email: "persona.prueba@sedemi.com"));

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Contains("\"email\":\"persona.prueba@sedemi.com\"", cuerpo);
        Assert.Equal(("upn.prueba@sedemi.com", "persona.prueba@sedemi.com"), (prov.UltimoUpn, prov.UltimoCorreo));
    }

    [Fact]
    public async Task SinClaimEmail_ElCorreoEsElUpn()
    {
        var (estado, cuerpo, prov) = await EnviarAsync("/api/usuarios/me", Token(roles: ["Gestor"], correo: "upn.prueba@sedemi.com"));

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Contains("\"email\":\"upn.prueba@sedemi.com\"", cuerpo);
        Assert.Equal(("upn.prueba@sedemi.com", "upn.prueba@sedemi.com"), (prov.UltimoUpn, prov.UltimoCorreo));
    }

    [Fact]
    public async Task SoloEmail_SinPreferredUsername_200ConUpnNulo()
    {
        var (estado, _, prov) = await EnviarAsync("/api/usuarios/me", Token(roles: ["Gestor"], correo: null, email: "persona.prueba@sedemi.com"));

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Equal((null, "persona.prueba@sedemi.com"), (prov.UltimoUpn, prov.UltimoCorreo));
    }

    [Fact]
    public async Task UpnDeOtraIdentidad_403ConElMotivo()
    {
        var (estado, cuerpo, _) = await EnviarAsync("/api/usuarios/me", Token(roles: ["Gestor"], oid: OidConflicto));

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Contains(ReglasProvisionamiento.MotivoUpnOtroOid, cuerpo);
    }

    [Fact]
    public async Task DevAuth_EmiteEmail()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "DevAuth",
            ["DevAuth:Usuarios:0:Clave"] = "gestor",
            ["DevAuth:Usuarios:0:ObjectId"] = OidActivo.ToString(),
            ["DevAuth:Usuarios:0:Email"] = "gestor.dev@profesiograma.local",
            ["DevAuth:Usuarios:0:Roles:0"] = "Gestor",
        });

        var (estado, _, prov) = await EjecutarAsync(builder, "/api/usuarios/me", token: null, devUser: "gestor");

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Equal(("gestor.dev@profesiograma.local", "gestor.dev@profesiograma.local"), (prov.UltimoUpn, prov.UltimoCorreo));
    }

    // ------------------------------------------------------------------ tokens y pipeline

    private static string Token(string[] roles, string? aud = ClientId, string iss = Emisor, string? scp = ScopesApi.AccederComoUsuario,
        string? correo = "persona.prueba@sedemi.com", Guid? oid = null, DateTime? vence = null, RsaSecurityKey? clave = null,
        string version = "2.0", string? email = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["oid"] = (oid ?? OidActivo).ToString(),
            ["tid"] = Tenant,
            ["ver"] = version,
            ["name"] = "PERSONA PRUEBA",
        };
        if (roles.Length > 0) claims["roles"] = roles;
        if (scp is not null) claims["scp"] = scp;
        if (correo is not null) claims["preferred_username"] = correo; // UPN
        if (email is not null) claims["email"] = email;               // claim opcional (TAREA-7b-2)

        var expira = vence ?? DateTime.UtcNow.AddMinutes(30);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = iss,
            Audience = aud,
            Claims = claims,
            NotBefore = expira.AddHours(-2),
            IssuedAt = expira.AddHours(-2),
            Expires = expira,
            SigningCredentials = new SigningCredentials(clave ?? Clave, SecurityAlgorithms.RsaSha256),
        });
    }

    private static Task<(int Estado, string Cuerpo, ProvisionamientoFalso Prov)> EnviarAsync(string ruta, string? token, string? devUser = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "EntraId",
            ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
            ["AzureAd:TenantId"] = Tenant,
            ["AzureAd:ClientId"] = ClientId,
            ["AzureAd:Audience"] = "", // como appsettings.json (ajuste 1)
        });
        return EjecutarAsync(builder, ruta, token, devUser);
    }

    private static async Task<(int Estado, string Cuerpo, ProvisionamientoFalso Prov)> EjecutarAsync(
        WebApplicationBuilder builder, string ruta, string? token, string? devUser)
    {
        var prov = new ProvisionamientoFalso();
        builder.AddSeguridadProfesiograma();
        builder.Services.AddSingleton<IUsuarioProvisionamiento>(prov);
        // Sin red: metadatos estáticos con la clave local y emisor fijo (después de la configuración de Microsoft.Identity.Web).
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            var configuracion = new OpenIdConnectConfiguration { Issuer = Emisor };
            configuracion.SigningKeys.Add(Clave);
            o.Configuration = configuracion;
            o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuracion);
            o.TokenValidationParameters.ConfigurationManager = null;
            o.TokenValidationParameters.IssuerValidator = null;
            o.TokenValidationParameters.ValidIssuers = [Emisor, EmisorV1]; // el validador de Entra acepta los dos emisores del tenant
        });

        await using var app = builder.Build();
        app.UseRouting();
        app.UseSeguridadProfesiograma();
        app.MapUsuarioEndpoints();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        var contexto = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        contexto.Request.Method = "GET";
        contexto.Request.Path = ruta;
        if (token is not null) contexto.Request.Headers.Authorization = $"Bearer {token}";
        if (devUser is not null) contexto.Request.Headers["X-Dev-User"] = devUser;
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        return (contexto.Response.StatusCode, System.Text.Encoding.UTF8.GetString(salida.ToArray()), prov);
    }

    private sealed class ProvisionamientoFalso : IUsuarioProvisionamiento
    {
        public int Llamadas { get; private set; }
        public string? UltimoUpn { get; private set; }
        public string? UltimoCorreo { get; private set; }

        public Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string? upn, string correo, string nombreMostrar, CancellationToken ct)
        {
            Llamadas++;
            (UltimoUpn, UltimoCorreo) = (upn, correo);
            return Task.FromResult(entraObjectId == OidInactivo ? new UsuarioResuelto(8, false, "El usuario está inactivo en la aplicación.")
                : entraObjectId == OidConflicto ? new UsuarioResuelto(9, false, ReglasProvisionamiento.MotivoUpnOtroOid)
                : new UsuarioResuelto(7, true));
        }

        public void Invalidar(string email, Guid? entraObjectId) { }
    }
}
