using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Seguridad;

namespace App.Application.Tests.Empleados;

/// <summary>Regla soloMisDepartamentos (FASE_5 §7.2, P5) y validación de parámetros.</summary>
public class EmpleadoConsultaServicioTests
{
    private const string Sig = "UNIDAD SISTEMA INTEGRADO DE GESTION";

    [Fact]
    public async Task PorDefecto_SoloMisDepartamentos_UsaLosDepartamentosDelUsuario()
    {
        var consultas = new ConsultasFalsas(departamentosUsuario: [Sig]);
        var servicio = new EmpleadoConsultaServicio(consultas, new UsuarioFalso(usuarioId: 3));

        var resultado = await servicio.BuscarAsync(new EmpleadoBusquedaSolicitud(null, null, null, null), CancellationToken.None);

        Assert.True(resultado.EsValido);
        Assert.Equal([3], consultas.UsuariosConsultados);
        Assert.Equal(new EmpleadoFiltro(null, consultas.DepartamentosUsuario, 1, 20), consultas.UltimoFiltro);
        Assert.Equal([Sig], consultas.UltimoFiltro!.Departamentos);
    }

    [Fact]
    public async Task SoloMisDepartamentosFalse_NoFiltraPorDepartamento_NiConsultaLosDelUsuario()
    {
        var consultas = new ConsultasFalsas(departamentosUsuario: [Sig]);
        var servicio = new EmpleadoConsultaServicio(consultas, new UsuarioFalso(usuarioId: 3));

        await servicio.BuscarAsync(new EmpleadoBusquedaSolicitud(null, "false", null, null), CancellationToken.None);

        Assert.Empty(consultas.UsuariosConsultados);
        Assert.Null(consultas.UltimoFiltro!.Departamentos);
    }

    [Fact]
    public async Task UsuarioSinDepartamentos_VeTodos()
    {
        var consultas = new ConsultasFalsas(departamentosUsuario: []);
        var servicio = new EmpleadoConsultaServicio(consultas, new UsuarioFalso(usuarioId: 2));

        await servicio.BuscarAsync(new EmpleadoBusquedaSolicitud(null, "true", null, null), CancellationToken.None);

        Assert.Equal([2], consultas.UsuariosConsultados);
        Assert.Null(consultas.UltimoFiltro!.Departamentos);
    }

    [Fact]
    public async Task SinUsuarioResuelto_VeTodos()
    {
        var consultas = new ConsultasFalsas(departamentosUsuario: [Sig]);
        var servicio = new EmpleadoConsultaServicio(consultas, new UsuarioFalso(usuarioId: null));

        await servicio.BuscarAsync(new EmpleadoBusquedaSolicitud(null, null, null, null), CancellationToken.None);

        Assert.Empty(consultas.UsuariosConsultados);
        Assert.Null(consultas.UltimoFiltro!.Departamentos);
    }

    [Fact]
    public async Task Texto_SeRecortaYPasaComoFiltro_ConPaginaYTamano()
    {
        var consultas = new ConsultasFalsas(departamentosUsuario: []);
        var servicio = new EmpleadoConsultaServicio(consultas, new UsuarioFalso(usuarioId: 3));

        await servicio.BuscarAsync(new EmpleadoBusquedaSolicitud("  DEV003 ", "false", "2", "10"), CancellationToken.None);

        Assert.Equal(new EmpleadoFiltro("DEV003", null, 2, 10), consultas.UltimoFiltro);
    }

    [Fact]
    public async Task ParametrosInvalidos_DevuelveErroresEnEspanol_SinConsultar()
    {
        var consultas = new ConsultasFalsas(departamentosUsuario: [Sig]);
        var servicio = new EmpleadoConsultaServicio(consultas, new UsuarioFalso(usuarioId: 3));

        var resultado = await servicio.BuscarAsync(new EmpleadoBusquedaSolicitud(null, "quizas", "0", "500"), CancellationToken.None);

        Assert.False(resultado.EsValido);
        Assert.Equal(["El valor de 'soloMisDepartamentos' debe ser true o false."], resultado.Errores!["soloMisDepartamentos"]);
        Assert.Equal(["La página debe ser un número entero mayor o igual a 1."], resultado.Errores!["pagina"]);
        Assert.Equal(["El tamaño de página debe ser un número entero entre 1 y 100."], resultado.Errores!["tamano"]);
        Assert.Null(consultas.UltimoFiltro);
        Assert.Empty(consultas.UsuariosConsultados);
    }

    // ------------------------------------------------------------------ dobles

    private sealed class ConsultasFalsas(IReadOnlyList<string> departamentosUsuario) : IEmpleadoConsultas
    {
        public IReadOnlyList<string> DepartamentosUsuario { get; } = departamentosUsuario;
        public EmpleadoFiltro? UltimoFiltro { get; private set; }
        public List<int> UsuariosConsultados { get; } = [];

        public Task<ResultadoBusquedaEmpleadosDto> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            return Task.FromResult(new ResultadoBusquedaEmpleadosDto([], filtro.Pagina, filtro.Tamano, 0, null));
        }

        public Task<IReadOnlyList<string>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct)
        {
            UsuariosConsultados.Add(usuarioId);
            return Task.FromResult(DepartamentosUsuario);
        }
    }

    private sealed class UsuarioFalso(int? usuarioId) : IUsuarioActual
    {
        public int? UsuarioId { get; } = usuarioId;
        public string? Email => null;
        public string? NombreMostrar => null;
        public IReadOnlyCollection<string> Roles => [RolesApp.Gestor];
        public bool EstaAutenticado => UsuarioId.HasValue;
        public bool TieneRol(string rol) => Roles.Contains(rol);
    }
}
