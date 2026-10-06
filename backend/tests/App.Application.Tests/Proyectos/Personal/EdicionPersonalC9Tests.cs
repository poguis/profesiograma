using App.Application.Proyectos.Personal;
using App.Application.Tests.Proyectos.Crear;
using static App.Application.Tests.Proyectos.Personal.DoblesPersonal;

namespace App.Application.Tests.Proyectos.Personal;

/// <summary>C9 (TAREA-18, pendiente 25): "Actualizar personal" sin cambios.</summary>
public class EdicionPersonalC9Tests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static (EdicionPersonalServicio Servicio, RepositorioEdicionFalso Repo, TransaccionFalsa Tx) Crear()
    {
        var repo = new RepositorioEdicionFalso(Proyecto());
        var tx = new TransaccionFalsa();
        return (new EdicionPersonalServicio(repo, new DatosFalsos(), new CrucesEdicionFalsos(), new EdicionPersonalValidador(), tx,
            new UsuarioFalso(), new RelojFijo(Ahora)), repo, tx);
    }

    [Fact]
    public async Task SinCambios_VistaPrevia200ConAdvertencia()
    {
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, new ActualizarPersonalSolicitud([SolP1()], [SolK1()]), Ct);

        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        Assert.Equal(["No hay cambios."], r.Previsualizacion!.Advertencias);
    }

    [Fact]
    public async Task SinCambios_Registro400General_SinAbrirTransaccion()
    {
        var (servicio, repo, tx) = Crear();

        var r = await servicio.RegistrarAsync(ProyectoId, new ActualizarPersonalSolicitud([SolP1()], [SolK1()]), Ct);

        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Assert.Equal(["No hay cambios para registrar."], r.Errores!["general"]);
        Assert.Equal(0, tx.Iniciadas);
        Assert.Null(repo.Aplicado);
    }

    [Fact]
    public async Task OmitirUnaQueAunNoEmpieza_EsUnCambio()
    {
        // K1 aún no empieza: omitirla la elimina (no es "sin cambios").
        var r = await Crear().Servicio.PrevisualizarAsync(ProyectoId, new ActualizarPersonalSolicitud([SolP1()], []), Ct);
        Assert.DoesNotContain("No hay cambios.", r.Previsualizacion!.Advertencias);
    }
}
