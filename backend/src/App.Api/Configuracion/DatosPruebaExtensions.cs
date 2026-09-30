using App.Api.Seguridad.DevAuth;
using App.Infrastructure.Persistencia.DatosPrueba;

namespace App.Api.Configuracion;

public static class DatosPruebaExtensions
{
    /// <summary>
    /// Crea datos ficticios al iniciar, SOLO en Development y con "DatosPrueba:SembrarAlIniciar" = true.
    /// Es idempotente. No aplica migraciones (eso se hace con "dotnet ef database update").
    /// </summary>
    public static async Task SembrarDatosPruebaAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("DatosPrueba:SembrarAlIniciar"))
        {
            return;
        }

        var devAuth = new DevAuthOptions();
        app.Configuration.GetSection("DevAuth").Bind(devAuth);
        var usuarios = devAuth.Usuarios
            .Select(u => new UsuarioPrueba(u.Clave, u.ObjectId, u.Email, u.Nombre))
            .ToArray();

        await using var scope = app.Services.CreateAsyncScope();
        var sembrador = scope.ServiceProvider.GetRequiredService<DatosPruebaSembrador>();
        await sembrador.SembrarAsync(usuarios);
    }
}
