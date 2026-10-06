using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Personal;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Estados;
using App.Domain.Proyectos.Cronograma;
using static App.Application.Tests.Proyectos.Cabecera.DoblesCabecera;

namespace App.Application.Tests.Proyectos.Cabecera;

/// <summary>
/// H15 (TAREA-18b): al acortar (C4), el descanso posterior de los backs que quedan se vuelve a insertar.
/// Proyecto C: K1 (Id 202) DEV001 JORNADA 10/11–12/11 + 2 días de descanso.
/// </summary>
public class EdicionCabeceraH15Tests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture);

    private static (EdicionCabeceraServicio Servicio, RepositorioCabeceraFalso Repo) Crear()
    {
        var repo = new RepositorioCabeceraFalso(Proyecto());
        return (new EdicionCabeceraServicio(repo, new EdicionCabeceraValidador(new ErpCabeceraFalso()), new TransaccionFalsa(),
            new UsuarioFalso(), new RelojFijo(Ahora)), repo);
    }

    private static EditarCabeceraSolicitud Fin(string fin) => new(null, D(fin), null, null, null, null);

    [Fact]
    public async Task BackRecortado_VistaPrevia_DiasAgregadosDesdeFMasUno()
    {
        var p = (await Crear().Servicio.PrevisualizarAsync(ProyectoId, Fin("2026-11-11"), Ct)).Previsualizacion!;

        Assert.Contains(p.PersonalRecortado, x => x.Empleado.CodigoEkon == "DEV001" && x.FechaFinNueva == D("2026-11-11"));
        var agregado = Assert.Single(p.DiasAgregados);
        Assert.Equal(("DEV001", "DESCANSO", 2, D("2026-11-12"), D("2026-11-13")),
            (agregado.Empleado.CodigoEkon, agregado.Rol, agregado.Cantidad, agregado.Desde, agregado.Hasta));
    }

    [Fact]
    public async Task BackNoRecortado_CuyoDescansoPasaDeF_SoloLosDiasDespuesDeF()
    {
        // K1 termina el 12/11 (no se recorta con F = 13/11); su descanso es 13–14/11: solo el 14/11 queda después de F.
        var p = (await Crear().Servicio.PrevisualizarAsync(ProyectoId, Fin("2026-11-13"), Ct)).Previsualizacion!;

        Assert.DoesNotContain(p.PersonalRecortado, x => x.Empleado.CodigoEkon == "DEV001");
        var agregado = Assert.Single(p.DiasAgregados);
        Assert.Equal((1, D("2026-11-14"), D("2026-11-14")), (agregado.Cantidad, agregado.Desde, agregado.Hasta));
    }

    [Fact]
    public async Task Registro_DescansosAgregadosEnElCambio_YSinRecorteNoHay()
    {
        var (servicio, repo) = Crear();
        await servicio.RegistrarAsync(ProyectoId, Fin("2026-11-11"), Ct);

        Assert.Equal(
            [(202, 1, D("2026-11-12"), RolCronograma.Descanso, TipoAsignacionCronograma.Manual, (short)1),
             (202, 1, D("2026-11-13"), RolCronograma.Descanso, TipoAsignacionCronograma.Manual, (short)1)],
            repo.Aplicado!.DescansosAgregados.Select(d => (d.PersonalId, d.Dia.EmpleadoId, d.Dia.Fecha, d.Dia.Rol, d.Dia.Tipo, d.Dia.Bloque)));

        var (otro, repoOtro) = Crear();
        var r = await otro.RegistrarAsync(ProyectoId, Fin("2026-12-20"), Ct); // ampliar: sin recorte
        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Empty(repoOtro.Aplicado!.DescansosAgregados);
    }
}
