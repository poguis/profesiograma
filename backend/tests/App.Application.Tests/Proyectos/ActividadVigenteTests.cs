using App.Application.Proyectos;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using App.Application.Tests.Proyectos.Personal;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos;

/// <summary>Regla única de actividad vigente (TAREA-18b, O3) y su uso en las etapas.</summary>
public class ActividadVigenteTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture);

    private static ActividadCorte A(int version, string codigo, string inicio, string fin) => new(version, version, codigo, D(inicio), D(fin));

    [Theory]
    [InlineData("2026-10-06", "2026-11-01")] // antes del inicio → inicio
    [InlineData("2026-11-15", "2026-11-15")] // dentro → la fecha
    [InlineData("2026-12-20", "2026-11-30")] // después del fin → fin
    public void FechaReferencia_MaxInicio_MinFechaFin(string fecha, string esperada) =>
        Assert.Equal(D(esperada), ActividadVigente.FechaReferencia(D("2026-11-01"), D("2026-11-30"), D(fecha)));

    [Fact]
    public void Elegir_LaQueCubre_YSiSeSolapanLaDeMayorVersion()
    {
        var actividades = new[] { A(1, "DEV.01", "2026-11-01", "2026-11-30"), A(2, "DEV.02", "2026-11-10", "2026-11-20") };

        Assert.Equal("DEV.01", ActividadVigente.Elegir(actividades, D("2026-11-05"))!.Codigo);
        Assert.Equal("DEV.02", ActividadVigente.Elegir(actividades, D("2026-11-15"))!.Codigo);
    }

    [Fact]
    public void Elegir_SinCobertura_Null_SinRespaldoPorVersion()
    {
        // Antes (detalle, V1 de la vista): sin cobertura se devolvía la de mayor versión (aquí DEV.02).
        var actividades = new[] { A(1, "DEV.01", "2026-11-01", "2026-11-05"), A(2, "DEV.02", "2026-11-20", "2026-11-30") };
        Assert.Null(ActividadVigente.Elegir(actividades, D("2026-11-10")));
    }

    [Fact]
    public void Elegir_ConFechasDelProyecto_ProyectoQueAunNoEmpieza_LaDelInicio()
    {
        // Caso r1/r2 de la prueba manual: hoy 06/10, proyecto 11/10–17/10, v1 DEV.01 11–13/10, v2 DEV.02 14–17/10.
        var actividades = new[] { A(1, "DEV.01", "2026-10-11", "2026-10-13"), A(2, "DEV.02", "2026-10-14", "2026-10-17") };
        Assert.Equal("DEV.01", ActividadVigente.Elegir(actividades, D("2026-10-11"), D("2026-10-17"), D("2026-10-06"))!.Codigo);
    }

    [Fact]
    public async Task EtapaSuspension_UsaLaRegla_MismoResultadoQueRecorteProyecto()
    {
        var repo = new RepositorioCambioFalso(DoblesEstados.Proyecto());
        var servicio = new CambioEstadoServicio(repo, new CambioEstadoValidador(), new TransaccionFalsa(), new UsuarioFalso(),
            new RelojFijo(new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero)));

        await servicio.AplicarAsync(DoblesEstados.ProyectoId, new CambioEstadoSolicitud("SUSPENDIDO", D("2027-03-15")).ConVersion(repo), Ct);

        Assert.Equal("DEV.01", repo.Aplicado!.ActividadCodigo);
        Assert.Equal(repo.Aplicado.Plan.ActividadVigenteEnF, repo.Aplicado.ActividadCodigo);
    }

    [Fact]
    public async Task EtapaActualizacionPersonal_ProyectoQueAunNoEmpieza_ActividadDelInicio()
    {
        // Corte 15/09 (hoy) antes del inicio 21/09: antes era null (vigente en hoy); con la regla, la del inicio.
        var corte = D("2026-09-15");
        var repo = new RepositorioEdicionFalso(DoblesPersonal.Proyecto(corte: corte));
        var servicio = new EdicionPersonalServicio(repo, new DatosFalsos(), new CrucesEdicionFalsos(), new EdicionPersonalValidador(),
            new TransaccionFalsa(), new UsuarioFalso(), new RelojFijo(new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero)));

        var r = await servicio.RegistrarAsync(DoblesPersonal.ProyectoId, new ActualizarPersonalSolicitud([DoblesPersonal.SolP1("TIPO_2")], []).ConVersion(repo), Ct);

        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal((corte, "DEV.01"), (repo.Aplicado!.Corte, repo.Aplicado.ActividadCodigo));
    }
}
