using App.Application.Comun;
using App.Application.Seguridad;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Tests.Persistencia;

/// <summary>
/// TAREA-22c-b (decisión 3a): con el registro real de AddPersistencia, QueryIterationFailed (10100) y ConnectionError
/// quedan en Debug. Conexión por loopback (127.0.0.1, puerto 1) con datos ficticios: no toca ninguna base. ~2–3 s.
/// </summary>
public sealed class PersistenciaLogTests
{
    [Fact]
    public async Task BaseCaida_EventosDeEfEnDebug_NingunErrorDeEf()
    {
        var log = new LogEnMemoria();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(log));
        services.AddScoped<IUsuarioActual, SinUsuario>();
        services.AddPersistencia("Server=127.0.0.1,1;Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia;Connect Timeout=1;TrustServerCertificate=true");
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfesiogramaDbContext>();

        await Assert.ThrowsAsync<BaseDatosNoDisponibleException>(() => db.Usuarios.AnyAsync(TestContext.Current.CancellationToken));

        var ef = log.Entradas.Where(e => e.Categoria.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)).ToList();
        Assert.Contains(ef, e => e.Evento == CoreEventId.QueryIterationFailed.Id && e.Nivel == LogLevel.Debug);
        Assert.Contains(ef, e => e.Evento == RelationalEventId.ConnectionError.Id && e.Nivel == LogLevel.Debug);
        Assert.DoesNotContain(ef, e => e.Nivel >= LogLevel.Warning);
    }

    private sealed class SinUsuario : IUsuarioActual
    {
        public int? UsuarioId => null;
        public string? Email => null;
        public string? NombreMostrar => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool EstaAutenticado => false;
        public bool TieneRol(string rol) => false;
    }

    private sealed record Entrada(string Categoria, int Evento, LogLevel Nivel);

    private sealed class LogEnMemoria : ILoggerProvider
    {
        public List<Entrada> Entradas { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

        public void Dispose() { }

        private sealed class Registrador(LogEnMemoria destino, string categoria) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (destino.Entradas)
                {
                    destino.Entradas.Add(new Entrada(categoria, eventId.Id, logLevel));
                }
            }
        }
    }
}
