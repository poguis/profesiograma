using App.Application.Seguridad;
using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Seguridad;
using App.Infrastructure.Seguridad;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// TAREA-25-1: traducción a SQL de las consultas de usuarios y departamentos, el plan de asignación (sin borrar filas) y la
/// invalidación de la caché de provisionamiento. El ExecuteUpdate (compare-and-set) y el sp_getapplock por usuario se
/// verifican con el script manual backend/tests/manual/tarea25/usuarios.cmd. Datos FICTICIOS.
/// </summary>
public class UsuariosAdminSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void Listado_SinSistema_SoloActivos_TextoEnCorreoONombre_UnaConsultaConDepartamentos()
    {
        var consulta = UsuariosAdminRepositorio.ConsultaListado(Db, new UsuarioAdminFiltro("persona", false, 2, 25));

        var total = Sql(consulta);
        var pagina = Sql(UsuariosAdminRepositorio.PaginaListado(consulta, 25, 25));

        Assert.Contains("[EsSistema] = CAST(0 AS bit)", total);
        Assert.Contains("[Activo] = CAST(1 AS bit)", total);
        Assert.Contains("[Email] LIKE", total);
        Assert.Contains("[NombreMostrar] LIKE", total);
        Assert.Contains("LEFT JOIN", pagina);              // departamentos en la misma consulta (sin N+1)
        Assert.Contains("[UsuarioDepartamento]", pagina);
        Assert.Contains("ORDER BY", pagina);
        Assert.Contains("OFFSET", pagina);
    }

    [Fact]
    public void Listado_IncluirInactivos_NoFiltraActivo()
    {
        var sql = Sql(UsuariosAdminRepositorio.ConsultaListado(Db, new UsuarioAdminFiltro(null, true, 1, 25)));

        Assert.DoesNotContain("[Activo] = CAST(1 AS bit)", sql);
    }

    [Fact]
    public void Detalle_PorId_SinSistema()
    {
        var sql = Sql(UsuariosAdminRepositorio.ConsultaDetalle(Db, 5));

        Assert.Contains("[EsSistema] = CAST(0 AS bit)", sql);
        Assert.Contains("[Departamento]", sql);
    }

    [Fact]
    public void PorEmail_Departamentos_Asignaciones_Traducen()
    {
        Assert.Contains("[Email] = @", Sql(UsuariosAdminRepositorio.ConsultaPorEmail(Db, "persona@prueba.local")));
        Assert.Contains("[Orden]", Sql(UsuariosAdminRepositorio.ConsultaDepartamentos(Db)));
        Assert.Contains("[UsuarioId] = @", Sql(UsuariosAdminRepositorio.ConsultaAsignaciones(Db, 5)));
    }

    [Fact]
    public void UsuarioConValores_FiltraIdNombreYActivoAnteriores()
    {
        var sql = Sql(UsuariosAdminRepositorio.ConsultaUsuarioConValores(Db, 5, "PERSONA", true));

        Assert.Contains("[NombreMostrar] = @", sql); // el nombre anterior viaja como parámetro (compare-and-set)
        Assert.Contains("[Activo] = @", sql);
        Assert.Contains("[EsSistema] = CAST(0 AS bit)", sql);
    }

    [Fact]
    public void Aplicar_RetiraReactivaYCrea_SinTocarLasQueNoCambian()
    {
        var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var sigue = new UsuarioDepartamento { Id = 1, UsuarioId = 5, DepartamentoId = 1, Activo = true, Notificado = true, FechaNotificacion = fecha };
        var sale = new UsuarioDepartamento { Id = 2, UsuarioId = 5, DepartamentoId = 2, Activo = true, Notificado = true, FechaNotificacion = fecha };
        var vuelve = new UsuarioDepartamento { Id = 3, UsuarioId = 5, DepartamentoId = 3, Activo = false, Notificado = true, FechaNotificacion = fecha };

        var (nuevas, agregados, retirados) = UsuariosAdminRepositorio.Aplicar([sigue, sale, vuelve], new HashSet<int> { 1, 3, 4 }, 5);

        Assert.Equal((true, true, fecha), (sigue.Activo, sigue.Notificado, (DateTime)sigue.FechaNotificacion!)); // no se toca
        Assert.False(sale.Activo);
        Assert.Equal((true, false, (DateTime?)null), (vuelve.Activo, vuelve.Notificado, vuelve.FechaNotificacion)); // misma fila
        Assert.Equal((5, 4, true, false), (nuevas.Single().UsuarioId, nuevas.Single().DepartamentoId, nuevas.Single().Activo, nuevas.Single().Notificado));
        Assert.Equal([3, 4], agregados);
        Assert.Equal([2], retirados);
    }

    /// <summary>TAREA-25-2: el 503 del bloqueo por usuario lleva este detail (el handler lo usa; ver BaseDatosNoDisponibleTests).</summary>
    [Fact]
    public void MensajeOcupado_PorUsuario()
    {
        Assert.Equal("Hay otra modificación de este usuario en curso; intente de nuevo.", UsuariosAdminRepositorio.MensajeOcupado);
    }

    [Fact]
    public void Aplicar_ConjuntoVacio_RetiraTodas_SinBorrar()
    {
        var filas = new[] { new UsuarioDepartamento { DepartamentoId = 1, Activo = true }, new UsuarioDepartamento { DepartamentoId = 2, Activo = false } };

        var (nuevas, agregados, retirados) = UsuariosAdminRepositorio.Aplicar(filas, new HashSet<int>(), 5);

        Assert.Empty(nuevas);
        Assert.Empty(agregados);
        Assert.Equal([1], retirados);
        Assert.All(filas, f => Assert.False(f.Activo));
    }

    /// <summary>P6: tras Invalidar, la siguiente solicitud ya no usa la entrada en caché (va a la base y ve la baja).</summary>
    [Fact]
    public void Invalidar_QuitaLasEntradasPorOidYPorCorreo()
    {
        var oid = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var habilitado = new UsuarioResuelto(7, true);
        cache.Set(UsuarioProvisionamiento.ClaveCache(oid.ToString()), habilitado);
        cache.Set(UsuarioProvisionamiento.ClaveCache("persona@prueba.local"), habilitado);
        cache.Set(UsuarioProvisionamiento.ClaveCache("otra@prueba.local"), habilitado);
        var provisionamiento = new UsuarioProvisionamiento(Db, cache, NullLogger<UsuarioProvisionamiento>.Instance);

        provisionamiento.Invalidar(" Persona@Prueba.local ", oid);

        Assert.False(cache.TryGetValue(UsuarioProvisionamiento.ClaveCache(oid.ToString()), out _));
        Assert.False(cache.TryGetValue(UsuarioProvisionamiento.ClaveCache("persona@prueba.local"), out _));
        Assert.True(cache.TryGetValue(UsuarioProvisionamiento.ClaveCache("otra@prueba.local"), out _));
    }

    [Fact]
    public async Task Resolver_ConEntradaEnCache_NoVaALaBase_HastaInvalidar()
    {
        var oid = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(UsuarioProvisionamiento.ClaveCache(oid.ToString()), new UsuarioResuelto(7, true));
        var provisionamiento = new UsuarioProvisionamiento(Db, cache, NullLogger<UsuarioProvisionamiento>.Instance);

        // La base de BaseSql no existe: si Resolver fuera a la base, fallaría. Con la entrada en caché responde sin ir.
        var antes = await provisionamiento.ResolverAsync(oid, "persona@prueba.local", "X", TestContext.Current.CancellationToken);
        provisionamiento.Invalidar("persona@prueba.local", oid);

        Assert.True(antes.Habilitado);
        Assert.False(cache.TryGetValue(UsuarioProvisionamiento.ClaveCache(oid.ToString()), out _)); // la siguiente lee la base
    }
}
