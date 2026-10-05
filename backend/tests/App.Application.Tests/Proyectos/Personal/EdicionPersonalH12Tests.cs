using App.Application.Proyectos.Personal;
using App.Application.Tests.Proyectos.Crear;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos.Personal;

/// <summary>
/// H12 (TAREA-17b): en la ACTUALIZACION_PERSONAL, un back con FechaFin &lt; corte cuyo descanso posterior se borró
/// (recorte de una suspensión) es histórico y su descanso no se regenera. Si tiene su descanso guardado, nada cambia.
/// Proyecto ya reactivado: 28/09–30/11/2026; P1 (101) DEV007 28/09–09/10 histórico; P2 (103) DEV007 TIPO_2 desde el
/// 11/10, inicial; K1 (102) DEV008 JORNADA 05/10–09/10 + 3 días, relacionado con P1. Corte = hoy = 11/10/2026.
/// </summary>
public class EdicionPersonalH12Tests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Inicio = new(2026, 9, 28), Fin = new(2026, 11, 30), FinK1 = new(2026, 10, 9);
    private static readonly DateOnly Corte = new(2026, 10, 11);
    private static readonly DateTimeOffset Ahora = new(2026, 10, 11, 15, 0, 0, TimeSpan.Zero);

    private static readonly PersonaGuardada P1 = new(101, RolCronograma.Principal, 1, 7, "DEV007", "EMPLEADO PRUEBA 07",
        Inicio, FinK1, "TIPO_2", 11, 4, "JORNADA", null, "PUESTO 7", null, true);
    private static readonly PersonaGuardada P2 = new(103, RolCronograma.Principal, 2, 7, "DEV007", "EMPLEADO PRUEBA 07",
        Corte, Fin, "TIPO_2", 11, 4, "JORNADA", null, "PUESTO 7", null, true);
    private static readonly PersonaGuardada K1 = new(102, RolCronograma.Back, 1, 8, "DEV008", "EMPLEADO PRUEBA 08",
        new DateOnly(2026, 10, 5), FinK1, null, null, 3, "JORNADA", 101, "PUESTO 8", null, false);

    /// <param name="descansoBorrado">true = la suspensión borró el descanso de K1 (10–12/10); false = está guardado.</param>
    private static DatosEdicion Proyecto(bool descansoBorrado)
    {
        var generado = MotorCronograma.Generar(new SolicitudCronograma(Inicio, Fin,
            [new PrincipalEntrada(1, 7, Inicio, FinK1, 11, 4)],
            [new BackEntrada(1, 8, K1.FechaInicio, K1.FechaFin, TipoRegistroBack.Jornada, 3, 1)]));
        var ids = new Dictionary<PersonaProyecto, int> { [new(RolCronograma.Principal, 1)] = 101, [new(RolCronograma.Back, 1)] = 102 };
        var limite = descansoBorrado ? FinK1 : Corte.AddDays(-1);

        return new DatosEdicion(9, "PRY-B", "ACTIVO", Inicio, Fin, [P1, P2, K1 with { SinDescansoPosterior = descansoBorrado }],
            generado.DiasFinales.Where(d => d.Fecha <= limite).Select(d => new DiaGuardado(ids[d.Persona], d)).ToList(),
            [new ActividadCorte(1, 1, "DEV.01", Inicio, FinK1), new ActividadCorte(2, 2, "DEV.01", Corte, Fin)]);
    }

    private static EdicionPersonalServicio Servicio(DatosEdicion proyecto) =>
        new(new RepositorioEdicionFalso(proyecto), new DatosFalsos(), new CrucesEdicionFalsos(), new EdicionPersonalValidador(),
            new TransaccionFalsa(), new UsuarioFalso(), new RelojFijo(Ahora));

    private static PrincipalEdicionSolicitud SolP2() => new("p2", 103, 7, "TIPO_2", Corte, Fin, null);

    [Fact]
    public async Task H12_BackRecortadoSinDescansoGuardado_EsHistorico_YNoSeRegeneraSuDescanso()
    {
        var servicio = Servicio(Proyecto(descansoBorrado: true));

        var edicion = (await servicio.ObtenerEdicionAsync(9, Ct))!;
        Assert.Equal("HISTORICO", edicion.Personal.Single(p => p.Id == 102).Clase);

        // Sin K1 en el cuerpo: no se exige ("Falta Back 1") porque es histórico.
        var r = await servicio.PrevisualizarAsync(9, new ActualizarPersonalSolicitud([SolP2()], []), Ct);

        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        Assert.Equal("HISTORICO", r.Previsualizacion!.Personal.Single(p => p.Id == 102).Clase);
        Assert.DoesNotContain(r.Previsualizacion.Tramos, t => t.CodigoEkon == "DEV008" && t.Fin > FinK1);
    }

    [Fact]
    public async Task H12_BackConDescansoGuardado_SigueVigente_YSeRegeneraComoHoy()
    {
        var servicio = Servicio(Proyecto(descansoBorrado: false));

        var edicion = (await servicio.ObtenerEdicionAsync(9, Ct))!;
        Assert.Equal("VIGENTE", edicion.Personal.Single(p => p.Id == 102).Clase);

        var k1 = new BackEdicionSolicitud("k1", 102, 8, "JORNADA", K1.FechaInicio, K1.FechaFin, 3, null, 101, null);
        var r = await servicio.PrevisualizarAsync(9, new ActualizarPersonalSolicitud([SolP2()], [k1]), Ct);

        Assert.Equal(EstadoEdicion.Previsualizado, r.Estado);
        Assert.Equal(("VIGENTE", "SIN_CAMBIO"), r.Previsualizacion!.Personal.Where(p => p.Id == 102).Select(p => (p.Clase, p.Accion)).Single());
        Assert.Contains(r.Previsualizacion.Tramos, t => t.CodigoEkon == "DEV008" && t.Rol == "DESCANSO"
                                                        && t.Inicio == Corte && t.Fin == new DateOnly(2026, 10, 12));
    }
}
