using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using static App.Domain.Tests.Proyectos.Cronograma.Cron;

namespace App.Domain.Tests.Proyectos.Estados;

/// <summary>
/// E3: recorte a la fecha F (inclusiva). Proyecto 01–31/03/2027.
/// P1 (Id 1): principal 01–31/03 con ciclo 5/2 (PRINCIPAL 01–05, DESCANSO AUTO 06–07, PRINCIPAL 08–12…).
/// Back (Id 3): 10–12/03 relacionado con P1, DESCANSO MANUAL 13–14/03.
/// </summary>
public class RecorteProyectoTests
{
    private static readonly DateOnly Inicio = F("2027-03-01");
    private static readonly DateOnly Fin = F("2027-03-31");

    private static readonly PersonaCorte P1 = new(1, Pri, 1, 101, Inicio, Fin, null);
    private static readonly PersonaCorte Back = new(3, Bck, 1, 103, F("2027-03-10"), F("2027-03-12"), 1);

    /// <summary>Ciclo 5/2 desde el inicio de la persona: 5 días PRINCIPAL y 2 DESCANSO.</summary>
    private static IEnumerable<DiaCorte> Ciclo(PersonaCorte p) =>
        Enumerable.Range(0, p.FechaFin.DayNumber - p.FechaInicio.DayNumber + 1)
            .Select(i => new DiaCorte(p.Id, p.FechaInicio.AddDays(i), i % 7 < 5 ? Pri : Des));

    private static IEnumerable<DiaCorte> Rango(int personalId, RolCronograma rol, string desde, string hasta)
    {
        for (var d = F(desde); d <= F(hasta); d = d.AddDays(1))
        {
            yield return new DiaCorte(personalId, d, rol);
        }
    }

    private static List<DiaCorte> DiasBase() =>
        [.. Ciclo(P1), .. Rango(3, Bck, "2027-03-10", "2027-03-12"), .. Rango(3, Des, "2027-03-13", "2027-03-14")];

    private static PlanRecorte Recortar(string fecha, List<PersonaCorte>? personal = null, List<DiaCorte>? dias = null,
        List<ActividadCorte>? actividades = null) =>
        RecorteProyecto.Calcular(Inicio, Fin, F(fecha), personal ?? [P1, Back], dias ?? DiasBase(), actividades ?? []);

    [Fact]
    public void FechaIgualAFechaFin_NoRecortaPersonal_PeroEliminaDescansosPosterioresAlFin()
    {
        // P3: el back 30–31/03 tiene su DESCANSO MANUAL el 01–02/04 (fuera del rango del proyecto). Decisión 8.2 (a).
        var backFinal = new PersonaCorte(4, Bck, 2, 104, F("2027-03-30"), Fin, 1);
        var dias = DiasBase().Concat(Rango(4, Bck, "2027-03-30", "2027-03-31")).Concat(Rango(4, Des, "2027-04-01", "2027-04-02")).ToList();
        var actividad = new ActividadCorte(10, 1, "DEV.01", Inicio, Fin);

        var plan = Recortar("2027-03-31", [P1, Back, backFinal], dias, [actividad]);

        Assert.Empty(plan.PersonalRecortado);
        Assert.Empty(plan.PersonalEliminado);
        Assert.Empty(plan.ActividadesRecortadas);
        Assert.Empty(plan.ActividadesEliminadas);
        var eliminado = Assert.Single(plan.DiasEliminados);
        Assert.Equal(new DiasEliminadosPersona(4, Des, 2, F("2027-04-01"), F("2027-04-02")), eliminado);
        Assert.Equal("DEV.01", plan.ActividadVigenteEnF);
    }

    [Fact]
    public void FechaIgualAFechaInicio_ConservaSoloElPrimerDia()
    {
        var actividad = new ActividadCorte(10, 1, "DEV.01", Inicio, Fin);

        var plan = Recortar("2027-03-01", actividades: [actividad]);

        Assert.Equal([new CambioFechaFin(1, Fin, Inicio)], plan.PersonalRecortado);
        Assert.Equal([3], plan.PersonalEliminado); // el back empieza el 10/03
        Assert.Equal(30, plan.DiasEliminados.Where(d => d.PersonalId == 1).Sum(d => d.Cantidad)); // 02–31/03
        Assert.Equal(5, plan.DiasEliminados.Where(d => d.PersonalId == 3).Sum(d => d.Cantidad));  // 10–14/03
        Assert.DoesNotContain(plan.DiasEliminados, d => d.Desde <= Inicio);
        Assert.Equal([new CambioFechaFin(10, Fin, Inicio)], plan.ActividadesRecortadas);
        Assert.Equal("DEV.01", plan.ActividadVigenteEnF);
    }

    [Fact]
    public void BackConDescansoPosteriorMasAllaDeF_NoSeRecorta_PeroPierdeLosDiasDeDescansoPosteriores()
    {
        var plan = Recortar("2027-03-13");

        Assert.DoesNotContain(plan.PersonalRecortado, r => r.Id == 3); // FechaFin 12/03 ≤ F
        var back = Assert.Single(plan.DiasEliminados, d => d.PersonalId == 3);
        Assert.Equal(new DiasEliminadosPersona(3, Des, 1, F("2027-03-14"), F("2027-03-14")), back);
    }

    [Fact]
    public void BackQueTerminaDespuesDeF_SeRecorta_YSusDescansosSeEliminan()
    {
        var plan = Recortar("2027-03-11");

        Assert.Contains(new CambioFechaFin(3, F("2027-03-12"), F("2027-03-11")), plan.PersonalRecortado);
        Assert.Contains(new DiasEliminadosPersona(3, Bck, 1, F("2027-03-12"), F("2027-03-12")), plan.DiasEliminados);
        Assert.Contains(new DiasEliminadosPersona(3, Des, 2, F("2027-03-13"), F("2027-03-14")), plan.DiasEliminados);
    }

    [Fact]
    public void PrincipalQueEmpiezaDespuesDeF_SeElimina_YSuBackQuedaSinPrincipal()
    {
        var p2 = new PersonaCorte(2, Pri, 2, 102, F("2027-03-20"), Fin, null);
        var backDeP2 = new PersonaCorte(4, Bck, 2, 104, F("2027-03-10"), F("2027-03-12"), 2);
        var dias = DiasBase().Concat(Ciclo(p2)).Concat(Rango(4, Bck, "2027-03-10", "2027-03-12")).ToList();

        var plan = Recortar("2027-03-15", [P1, p2, Back, backDeP2], dias);

        Assert.Equal([2], plan.PersonalEliminado);
        Assert.Equal([4], plan.BacksSinPrincipal); // el back de P1 conserva su relación
        Assert.Equal(12, plan.DiasEliminados.Where(d => d.PersonalId == 2).Sum(d => d.Cantidad)); // 20–31/03
        Assert.DoesNotContain(plan.PersonalRecortado, r => r.Id == 2);
    }

    [Fact]
    public void DescansoAutoQueCruzaF_SeConservaElDiaF_YSeEliminaElSiguiente()
    {
        var plan = Recortar("2027-03-06"); // 06 y 07/03 son DESCANSO AUTO de P1

        var descanso = Assert.Single(plan.DiasEliminados, d => d.PersonalId == 1 && d.Rol == Des);
        Assert.Equal(F("2027-03-07"), descanso.Desde);
        var principal = Assert.Single(plan.DiasEliminados, d => d.PersonalId == 1 && d.Rol == Pri);
        Assert.Equal(F("2027-03-08"), principal.Desde);
        Assert.Equal(25, descanso.Cantidad + principal.Cantidad); // 07–31/03
    }

    [Fact]
    public void Actividades_AntesDuranteYDespuesDeF()
    {
        List<ActividadCorte> actividades =
        [
            new(10, 1, "A1", F("2027-03-01"), F("2027-03-10")),
            new(11, 2, "A2", F("2027-03-11"), F("2027-03-20")),
            new(12, 3, "A3", F("2027-03-21"), F("2027-03-31")),
        ];

        var plan = Recortar("2027-03-15", actividades: actividades);

        Assert.Equal([new CambioFechaFin(11, F("2027-03-20"), F("2027-03-15"))], plan.ActividadesRecortadas);
        Assert.Equal([12], plan.ActividadesEliminadas);
        Assert.Equal("A2", plan.ActividadVigenteEnF);
    }

    [Fact]
    public void SinActividades_VigenteNull() => Assert.Null(Recortar("2027-03-15").ActividadVigenteEnF);

    [Theory]
    [InlineData("2027-02-28")]
    [InlineData("2027-04-01")]
    public void FechaFueraDelRango_Lanza(string fecha) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Recortar(fecha));

    [Fact]
    public void TotalDiasEliminados_SumaTodosLosGrupos()
    {
        var plan = Recortar("2027-03-15");
        Assert.Equal(plan.DiasEliminados.Sum(d => d.Cantidad), plan.TotalDiasEliminados);
        Assert.Equal(16, plan.DiasEliminados.Where(d => d.PersonalId == 1).Sum(d => d.Cantidad)); // 16–31/03
    }
}
