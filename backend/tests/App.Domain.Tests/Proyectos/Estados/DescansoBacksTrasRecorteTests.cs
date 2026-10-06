using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Domain.Tests.Proyectos.Estados;

/// <summary>H15 (TAREA-18b): descanso posterior de los backs que quedan después de acortar (C4).</summary>
public class DescansoBacksTrasRecorteTests
{
    private static DateOnly D(string fecha) => DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture);

    private static BackTrasRecorte B(int id, short numero, int empleado, string inicio, string fin, byte descanso,
        TipoRegistroBack tipo = TipoRegistroBack.Jornada) => new(id, numero, empleado, D(inicio), D(fin), tipo, descanso);

    [Fact]
    public void BackRecortado_SuDescansoCompletoDespuesDeF()
    {
        var r = DescansoBacksTrasRecorte.Calcular(D("2026-11-10"), [B(1, 1, 7, "2026-11-08", "2026-11-10", 3)]);

        Assert.Equal([D("2026-11-11"), D("2026-11-12"), D("2026-11-13")], r.Select(x => x.Dia.Fecha));
        Assert.All(r, x => Assert.Equal((1, 7, RolCronograma.Descanso, TipoAsignacionCronograma.Manual, (short)1),
            (x.PersonalId, x.Dia.EmpleadoId, x.Dia.Rol, x.Dia.Tipo, x.Dia.Bloque)));
    }

    [Fact]
    public void BackNoRecortado_CuyoDescansoPasaDeF_SoloLosDiasDespuesDeF()
    {
        var r = DescansoBacksTrasRecorte.Calcular(D("2026-11-10"), [B(2, 2, 8, "2026-11-05", "2026-11-08", 4)]);
        Assert.Equal([D("2026-11-11"), D("2026-11-12")], r.Select(x => x.Dia.Fecha));
    }

    [Fact]
    public void SinDescansoPosterior_DescansoTipoDescanso_YDescansoAntesDeF_NoAgregaNada()
    {
        var r = DescansoBacksTrasRecorte.Calcular(D("2026-11-10"),
        [
            B(1, 1, 7, "2026-11-08", "2026-11-10", 0),
            B(2, 2, 8, "2026-11-01", "2026-11-10", 0, TipoRegistroBack.Descanso),
            B(3, 3, 9, "2026-11-01", "2026-11-05", 2),
        ]);
        Assert.Empty(r);
    }

    [Fact]
    public void MismoEmpleadoEnDosBacks_SeDeduplicaPorFecha()
    {
        var r = DescansoBacksTrasRecorte.Calcular(D("2026-11-10"),
            [B(1, 1, 7, "2026-11-01", "2026-11-10", 2), B(2, 2, 7, "2026-11-05", "2026-11-09", 3)]);

        Assert.Equal([D("2026-11-11"), D("2026-11-12")], r.Select(x => x.Dia.Fecha));
        Assert.All(r, x => Assert.Equal(1, x.PersonalId)); // el primero que aparece
    }

    /// <summary>
    /// Equivalencia: el cronograma que queda tras acortar (recorte + descansos agregados) coincide, desde el corte, con lo
    /// que produce MotorCronograma.Regenerar sin cambios sobre el proyecto ya acortado.
    /// </summary>
    [Fact]
    public void Equivalencia_ConRegenerarSinCambios_DesdeElCorte()
    {
        DateOnly inicio = D("2026-11-01"), fin = D("2026-11-30"), f = D("2026-11-10"), corte = D("2026-11-06");
        var generado = MotorCronograma.Generar(new SolicitudCronograma(inicio, fin,
            [new PrincipalEntrada(1, 6, inicio, fin, 5, 2)],
            [
                new BackEntrada(1, 7, D("2026-11-10"), D("2026-11-14"), TipoRegistroBack.Jornada, 3, 1), // se recorta al 10/11
                new BackEntrada(2, 8, D("2026-11-05"), D("2026-11-08"), TipoRegistroBack.Jornada, 4, 1), // no se recorta; su descanso pasa de F
            ]));
        var ids = new Dictionary<PersonaProyecto, int>
        {
            [new(RolCronograma.Principal, 1)] = 100, [new(RolCronograma.Back, 1)] = 101, [new(RolCronograma.Back, 2)] = 102,
        };

        var plan = RecorteProyecto.Calcular(inicio, fin, f,
            [
                new PersonaCorte(100, RolCronograma.Principal, 1, 6, inicio, fin, null),
                new PersonaCorte(101, RolCronograma.Back, 1, 7, D("2026-11-10"), D("2026-11-14"), 100),
                new PersonaCorte(102, RolCronograma.Back, 2, 8, D("2026-11-05"), D("2026-11-08"), 100),
            ],
            generado.DiasFinales.Select(d => new DiaCorte(ids[d.Persona], d.Fecha, d.Rol)).ToList(), []);
        Assert.Equal([new CambioFechaFin(100, fin, f), new CambioFechaFin(101, D("2026-11-14"), f)], plan.PersonalRecortado);

        var agregados = DescansoBacksTrasRecorte.Calcular(f,
            [B(101, 1, 7, "2026-11-10", "2026-11-10", 3), B(102, 2, 8, "2026-11-05", "2026-11-08", 4)]);
        var resultantes = generado.DiasFinales.Where(d => d.Fecha <= f).Concat(agregados.Select(a => a.Dia)).ToList();

        var regenerado = MotorCronograma.Regenerar(new SolicitudRegeneracion(inicio, f, corte,
            [new PrincipalEdicion("p1", 1, 6, inicio, f, 5, 2, EsNuevo: false)],
            [
                new BackEdicion("k1", 1, 7, D("2026-11-10"), f, TipoRegistroBack.Jornada, 3, EsNuevo: false),
                new BackEdicion("k2", 2, 8, D("2026-11-05"), D("2026-11-08"), TipoRegistroBack.Jornada, 4, EsNuevo: false),
            ],
            resultantes.Where(d => d.Fecha < corte)
                .Select(d => new DiaExistente(d.Persona.Rol == RolCronograma.Principal ? "p1" : $"k{d.Persona.Numero}", d)).ToList()));

        static (int, DateOnly, RolCronograma, TipoAsignacionCronograma, short) Clave(DiaAsignado d) =>
            (d.EmpleadoId, d.Fecha, d.Rol, d.Tipo, d.Bloque);
        Assert.Equal(
            resultantes.Where(d => d.Fecha >= corte).Select(Clave).Order().ToList(),
            regenerado.DiasAInsertar.Select(d => Clave(d.Dia)).Order().ToList());
        Assert.Contains(agregados, a => a.Dia.Fecha == D("2026-11-13")); // descanso del back recortado
        Assert.Contains(agregados, a => a.Dia.Fecha == D("2026-11-12") && a.Dia.EmpleadoId == 8); // back no recortado
    }
}
