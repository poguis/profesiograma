using App.Application.Proyectos.Crear;

namespace App.Application.Tests.Proyectos.Crear;

public class OpcionesFormularioProyectoServicioTests
{
    [Fact]
    public async Task DevuelveDepartamentosDelUsuario_OpcionesDeAlmuerzo_YLimitesDelParametro()
    {
        var datos = new DatosFalsos { Limites = new LimitesProyecto(MaxPrincipales: 18, MaxBacks: 12, MaxDiasDescansoBack: 5) };
        var servicio = new OpcionesFormularioProyectoServicio(datos, new UsuarioFalso());

        var opciones = await servicio.ObtenerAsync(TestContext.Current.CancellationToken);

        var departamento = Assert.Single(opciones.Departamentos);
        Assert.Equal(Dobles.DepartamentoSig, departamento.Id);
        Assert.Equal(ReglasAlmuerzo.OpcionesSalida, opciones.AlmuerzoSalidaOpciones);
        Assert.Equal(ReglasAlmuerzo.OpcionesRegreso, opciones.AlmuerzoRegresoOpciones);
        Assert.Equal(18, opciones.MaxPrincipales);
        Assert.Equal(12, opciones.MaxBacks);
        Assert.Equal(5, opciones.BackMaxDiasDescanso);
    }

    [Fact]
    public async Task VariosDepartamentos_SeDevuelvenTodos()
    {
        var datos = new DatosFalsos { DepartamentosUsuario = [new(7, "SIG"), new(8, "OTRO")] };
        var servicio = new OpcionesFormularioProyectoServicio(datos, new UsuarioFalso());

        var opciones = await servicio.ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.Equal([7, 8], opciones.Departamentos.Select(d => d.Id));
    }

    [Fact]
    public async Task SinUsuario_SinDepartamentos()
    {
        var servicio = new OpcionesFormularioProyectoServicio(new DatosFalsos(), new UsuarioFalso(usuarioId: null));

        var opciones = await servicio.ObtenerAsync(TestContext.Current.CancellationToken);

        Assert.Empty(opciones.Departamentos);
    }
}
