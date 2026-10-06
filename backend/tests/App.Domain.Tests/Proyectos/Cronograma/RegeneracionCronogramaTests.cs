using App.Domain.Proyectos.Cronograma;
using static App.Domain.Tests.Proyectos.Cronograma.Cron;

namespace App.Domain.Tests.Proyectos.Cronograma;

/// <summary>
/// TAREA-16: MotorCronograma.Regenerar (RN11). Proyecto 01–31/10/2026. La base de los existentes se arma con Generar
/// (equivale a lo guardado). Claves: "P{n}" principales, "K{n}" backs.
/// </summary>
public class RegeneracionCronogramaTests
{
    private const byte T2 = 11, D2 = 4; // TIPO_2
    private const byte T3 = 5, D3 = 2;  // TIPO_3
    private static readonly PersonaProyecto P3 = new(RolCronograma.Principal, 3);

    // ------------------------------------------------------------------ ayudas

    private static string Clave(PersonaProyecto p) => p.Rol == RolCronograma.Principal ? $"P{p.Numero}" : $"K{p.Numero}";

    private static PrincipalEdicion PE(short numero, int empleado, string inicio, string fin, byte trabajo, byte descanso,
        bool nuevo, bool forzar = false) =>
        new($"P{numero}", numero, empleado, F(inicio), F(fin), trabajo, descanso, nuevo, forzar);

    private static BackEdicion BE(short numero, int empleado, string inicio, string fin, TipoRegistroBack tipo, byte descanso,
        bool nuevo, bool forzar = false) =>
        new($"K{numero}", numero, empleado, F(inicio), F(fin), tipo, descanso, nuevo, null, forzar);

    /// <summary>Días guardados = lo que Generar produjo al crear (todas las fechas; el motor usa solo las anteriores al corte).</summary>
    private static List<DiaExistente> Guardado(PrincipalEntrada[]? principales = null, BackEntrada[]? backs = null) =>
        MotorCronograma.Generar(Solicitud(principales, backs)).DiasFinales.Select(d => new DiaExistente(Clave(d.Persona), d)).ToList();

    private static ResultadoRegeneracion Regenerar(string corte, PrincipalEdicion[]? principales = null, BackEdicion[]? backs = null,
        List<DiaExistente>? existentes = null) =>
        MotorCronograma.Regenerar(new SolicitudRegeneracion(F("2026-10-01"), F("2026-10-31"), F(corte),
            principales ?? [], backs ?? [], existentes ?? []));

    private static List<DiaAsignado> Insertar(ResultadoRegeneracion r, string? clave = null) =>
        r.DiasAInsertar.Where(d => clave is null || d.Clave == clave).Select(d => d.Dia).ToList();

    private static PrincipalEntrada P1Tipo2Guardado => Principal(1, 1, "2026-10-01", "2026-10-31", T2, D2);
    private static PrincipalEdicion P1Tipo2Existente => PE(1, 1, "2026-10-01", "2026-10-31", T2, D2, nuevo: false);

    // ------------------------------------------------------------------ X1: invariante
    // B5 (entradas inválidas) no entra: no hay resultado que comparar; sus 5 variantes están en X16 con las mismas entradas.

    private static readonly Dictionary<string, SolicitudCronograma> CasosGenerar = new()
    {
        ["E1"] = Solicitud([Principal(1, 1, "2026-10-01", "2026-10-31", T2, D2)]),
        ["E2"] = Solicitud([Principal(1, 1, "2026-10-01", "2026-10-10", 22, 8)]),
        ["E3"] = Solicitud([Principal(1, 1, "2026-10-01", "2026-10-07", 3, 0)]),
        ["E4"] = Solicitud(backs: [Back(1, 3, "2026-10-12", "2026-10-15", TipoRegistroBack.Jornada, 2)]),
        ["E5"] = Solicitud(backs: [Back(1, 3, "2026-10-12", "2026-10-15", TipoRegistroBack.Descanso, 2)]),
        ["E6"] = Solicitud([P1Tipo2Guardado], [Back(1, 1, "2026-10-12", "2026-10-12", TipoRegistroBack.Jornada, 0)]),
        ["E7"] = Solicitud([P1Tipo2Guardado], [Back(1, 2, "2026-10-05", "2026-10-05", TipoRegistroBack.Jornada, 0)]),
        ["E8"] = Solicitud([P1Tipo2Guardado], [Back(1, 1, "2026-10-05", "2026-10-05", TipoRegistroBack.Jornada, 0)]),
        ["E9"] = Solicitud([P1Tipo2Guardado], [Back(1, 1, "2026-10-05", "2026-10-05", TipoRegistroBack.Descanso, 0)]),
        ["E10"] = Solicitud([Principal(1, 1, "2026-10-01", "2026-10-01", T2, D2)], inicio: "2026-10-01", fin: "2026-10-01"),
        ["B1"] = Solicitud([Principal(1, 1, "2026-10-05", "2026-10-18", T3, D3)]),
        ["B2"] = Solicitud([P1Tipo2Guardado, Principal(2, 2, "2026-10-01", "2026-10-14", T3, D3)]),
        ["B3"] = Solicitud([P1Tipo2Guardado], [Back(1, 1, "2026-10-14", "2026-10-15", TipoRegistroBack.Descanso, 0)]),
        ["B4"] = Solicitud(backs: [Back(1, 3, "2026-10-28", "2026-10-31", TipoRegistroBack.Jornada, 3)]),
        ["B6a"] = Solicitud([P1Tipo2Guardado], backs: []),
        ["B6b"] = Solicitud(),
        ["B6c"] = Solicitud(backs: [Back(1, 3, "2026-10-05", "2026-10-06", TipoRegistroBack.Jornada, 0)]),
        ["B7"] = Solicitud([P1Tipo2Guardado, Principal(2, 1, "2026-10-01", "2026-10-31", T2, D2)]),
    };

    public static TheoryData<string> NombresCasosGenerar => [.. CasosGenerar.Keys];

    [Theory]
    [MemberData(nameof(NombresCasosGenerar))]
    public void X1_Invariante_CorteEnElInicio_BaseVacia_TodosNuevos_IgualAGenerar(string caso)
    {
        var s = CasosGenerar[caso];
        var g = MotorCronograma.Generar(s);

        var r = MotorCronograma.Regenerar(new SolicitudRegeneracion(s.InicioProyecto, s.FinProyecto, s.InicioProyecto,
            s.Principales.Select(p => new PrincipalEdicion($"P{p.Numero}", p.Numero, p.EmpleadoId, p.Inicio, p.Fin, p.DiasTrabajo, p.DiasDescanso, true)).ToList(),
            s.Backs.Select(b => new BackEdicion($"K{b.Numero}", b.Numero, b.EmpleadoId, b.Inicio, b.Fin, b.TipoRegistro, b.DiasDescanso, true, b.PrincipalRelacionado)).ToList(),
            []));

        Assert.Equal(g.Tramos, r.Tramos);
        Assert.Equal(g.DiasFinales, Insertar(r));
        Assert.Equal(Cruces(g.CrucesInternos), Cruces(r.CrucesInternos));
        Assert.Equal(g.DiasBase.Where(d => d.Rol != Des), r.DiasTrabajoRegenerados);
        Assert.Empty(r.CrucesHistoricos);
        Assert.False(r.HayDiasAnterioresAlCorte);
        Assert.All(r.Clases.Values, c => Assert.Equal(ClasePersona.Nueva, c));

        static List<string> Cruces(IEnumerable<CruceInterno> c) =>
            c.Select(x => $"{x.EmpleadoId}|{x.Fecha:yyyy-MM-dd}|{string.Join(",", x.Involucrados)}").ToList();
    }

    // ------------------------------------------------------------------ existentes

    [Fact]
    public void X2_ExistenteConCorteAMitadDeBloque()
    {
        var r = Regenerar("2026-10-20", [P1Tipo2Existente], existentes: Guardado([P1Tipo2Guardado]));

        Tramo[] regenerados =
        [
            T(Pri, Auto, 2, P1, 1, "2026-10-20", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
            T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30"),
        ];
        Assert.Equal(Dias(regenerados), Insertar(r)); // el descanso del bloque 1 (12–15) es anterior al corte
        Assert.Equal(
        [
            T(Pri, Auto, 1, P1, 1, "2026-10-01", "2026-10-11"),
            T(Des, Auto, 1, P1, 1, "2026-10-12", "2026-10-15"),
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-19"),
            .. regenerados,
        ], r.Tramos);
        Assert.Equal(ClasePersona.Vigente, r.Clases["P1"]);
        Assert.False(r.HayDiasAnterioresAlCorte);
        Assert.False(r.TieneCruces);
    }

    [Fact]
    public void X3_M2_ExistenteConCorteAMitadDelDescanso_RegeneraElRestoDelDescanso()
    {
        // Base: P 01–11 y D AUTO 12. El D AUTO de la base no ocupa el "primer día libre" (M2, como el original).
        var r = Regenerar("2026-10-13", [P1Tipo2Existente], existentes: Guardado([P1Tipo2Guardado]));

        Assert.Equal(Dias(
            T(Pri, Auto, 2, P1, 1, "2026-10-16", "2026-10-26"),
            T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31"),
            T(Des, Auto, 1, P1, 1, "2026-10-13", "2026-10-15"),
            T(Des, Auto, 2, P1, 1, "2026-10-27", "2026-10-30")), Insertar(r));
    }

    [Fact]
    public void X4_M3_ExistenteCambiaDeJornada_CicloAncladoASuInicio()
    {
        // Guardado TIPO_2; ahora TIPO_3 desde el 01/10: bloques 01–05, 08–12, 15–19, 22–26, 29–31.
        var r = Regenerar("2026-10-20", [PE(1, 1, "2026-10-01", "2026-10-31", T3, D3, nuevo: false)],
            existentes: Guardado([P1Tipo2Guardado]));

        Assert.Equal(Dias(
            T(Pri, Auto, 4, P1, 1, "2026-10-22", "2026-10-26"),
            T(Pri, Auto, 5, P1, 1, "2026-10-29", "2026-10-31"),
            T(Des, Auto, 2, P1, 1, "2026-10-20", "2026-10-21"), // tras el tramo base 16–19, con el descanso actual (2)
            T(Des, Auto, 4, P1, 1, "2026-10-27", "2026-10-28")), Insertar(r));
    }

    [Fact]
    public void X5_ExistenteAcortaSuFinAntesDelCorte_NoGeneraDias()
    {
        var r = Regenerar("2026-10-20", [PE(1, 1, "2026-10-01", "2026-10-19", T2, D2, nuevo: false)],
            existentes: Guardado([P1Tipo2Guardado]));

        Assert.Equal(ClasePersona.Historica, r.Clases["P1"]);
        Assert.Empty(r.DiasAInsertar);
    }

    // ------------------------------------------------------------------ nuevas y cruces

    [Fact]
    public void X6_M1_NuevoConInicioAnteriorAlCorte_InsertaTodoYAvisa()
    {
        var r = Regenerar("2026-10-20",
            [P1Tipo2Existente, PE(2, 2, "2026-10-15", "2026-10-31", T3, D3, nuevo: true)],
            existentes: Guardado([P1Tipo2Guardado]));

        Assert.Equal(Dias(
            T(Pri, Auto, 1, P2, 2, "2026-10-15", "2026-10-19"),
            T(Pri, Auto, 2, P2, 2, "2026-10-22", "2026-10-26"),
            T(Pri, Auto, 3, P2, 2, "2026-10-29", "2026-10-31"),
            T(Des, Auto, 1, P2, 2, "2026-10-20", "2026-10-21"),
            T(Des, Auto, 2, P2, 2, "2026-10-27", "2026-10-28")), Insertar(r, "P2"));
        Assert.True(r.HayDiasAnterioresAlCorte);
        Assert.Equal(ClasePersona.Nueva, r.Clases["P2"]);
    }

    [Fact]
    public void X7_NuevoQueCruzaConLaBase_HistoricoPropio_YConRegenerados_Interno()
    {
        var r = Regenerar("2026-10-20", [P1Tipo2Existente],
            [BE(1, 1, "2026-10-18", "2026-10-21", TipoRegistroBack.Jornada, 0, nuevo: true)],
            Guardado([P1Tipo2Guardado]));

        Assert.Equal(
            [new CruceHistorico(1, F("2026-10-18"), K1, P1), new CruceHistorico(1, F("2026-10-19"), K1, P1)],
            r.CrucesHistoricos);
        Assert.Equal([F("2026-10-20"), F("2026-10-21")], r.CrucesInternos.Select(c => c.Fecha));
        Assert.All(r.CrucesInternos, c => Assert.Equal([P1, K1], c.Involucrados));
        Assert.True(r.TieneCruces);
    }

    [Fact]
    public void X8_CruceInternoNuevoContraExistente()
    {
        var r = Regenerar("2026-10-20",
            [P1Tipo2Existente, PE(2, 1, "2026-10-22", "2026-10-28", T3, D3, nuevo: true)],
            existentes: Guardado([P1Tipo2Guardado]));

        Assert.Equal(5, r.CrucesInternos.Count); // 22–26
        Assert.All(r.CrucesInternos, c => Assert.Equal([P1, P2], c.Involucrados));
        Assert.Empty(r.CrucesHistoricos);
    }

    [Fact]
    public void X9_BackExistenteConDescansoPosteriorQueCruzaElCorte()
    {
        var r = Regenerar("2026-10-20", backs: [BE(1, 3, "2026-10-15", "2026-10-18", TipoRegistroBack.Jornada, 3, nuevo: false)],
            existentes: Guardado(backs: [Back(1, 3, "2026-10-15", "2026-10-18", TipoRegistroBack.Jornada, 3)]));

        Assert.Equal(ClasePersona.Vigente, r.Clases["K1"]); // 18 + 3 = 21 ≥ 20
        Assert.Equal(Dias(T(Des, Man, 1, K1, 3, "2026-10-20", "2026-10-21")), Insertar(r));
    }

    [Fact]
    public void X10_BackNuevoDescanso_SinDescansoPosterior_IncluyeDiasAnteriores()
    {
        var r = Regenerar("2026-10-20", backs: [BE(1, 4, "2026-10-18", "2026-10-24", TipoRegistroBack.Descanso, 2, nuevo: true)]);

        Assert.Equal(Dias(T(Des, Man, 1, K1, 4, "2026-10-18", "2026-10-24")), Insertar(r));
        Assert.True(r.HayDiasAnterioresAlCorte);
    }

    [Fact]
    public void X18_SoloBacks_SinPrincipales_RegeneraVigentesYNuevos_SinCruces()
    {
        // TAREA-19y (pendiente 33): proyecto sin principales. K1 guardado y vigente en el corte; K2 nuevo con 2 días de descanso.
        var k2 = new PersonaProyecto(RolCronograma.Back, 2);
        var r = Regenerar("2026-10-20", backs:
            [
                BE(1, 6, "2026-10-15", "2026-10-25", TipoRegistroBack.Jornada, 0, nuevo: false),
                BE(2, 7, "2026-10-22", "2026-10-25", TipoRegistroBack.Jornada, 2, nuevo: true),
            ],
            existentes: Guardado(backs: [Back(1, 6, "2026-10-15", "2026-10-25", TipoRegistroBack.Jornada, 0)]));

        Assert.Equal(ClasePersona.Vigente, r.Clases["K1"]);
        Assert.Equal(Dias(T(Bck, Man, 1, K1, 6, "2026-10-20", "2026-10-25")), Insertar(r, "K1"));
        Assert.Equal(
            Dias(T(Bck, Man, 2, k2, 7, "2026-10-22", "2026-10-25")).Concat(Dias(T(Des, Man, 2, k2, 7, "2026-10-26", "2026-10-27"))),
            Insertar(r, "K2"));
        Assert.Empty(r.CrucesInternos);
        Assert.Empty(r.CrucesHistoricos);
        Assert.Contains(T(Bck, Man, 1, K1, 6, "2026-10-15", "2026-10-19"), r.Tramos); // base anterior al corte
    }

    [Fact]
    public void X11_Historicos_NoSeRegeneran_PeroSusTramosSeMuestran()
    {
        var principal = Principal(1, 5, "2026-10-01", "2026-10-10", T3, D3);
        var back = Back(1, 6, "2026-10-05", "2026-10-08", TipoRegistroBack.Jornada, 2);

        var r = Regenerar("2026-10-20",
            [PE(1, 5, "2026-10-01", "2026-10-10", T3, D3, nuevo: false)],
            [BE(1, 6, "2026-10-05", "2026-10-08", TipoRegistroBack.Jornada, 2, nuevo: false)],
            Guardado([principal], [back]));

        Assert.Equal(ClasePersona.Historica, r.Clases["P1"]);
        Assert.Equal(ClasePersona.Historica, r.Clases["K1"]); // 08 + 2 = 10 < 20
        Assert.Empty(r.DiasAInsertar);
        Assert.Contains(T(Pri, Auto, 1, P1, 5, "2026-10-01", "2026-10-05"), r.Tramos);
        Assert.Contains(T(Bck, Man, 1, K1, 6, "2026-10-05", "2026-10-08"), r.Tramos);
    }

    [Fact]
    public void X12_CorteEnLaFechaFinDelProyecto()
    {
        var r = Regenerar("2026-10-31", [P1Tipo2Existente], existentes: Guardado([P1Tipo2Guardado]));

        Assert.Equal(Dias(T(Pri, Auto, 3, P1, 1, "2026-10-31", "2026-10-31")), Insertar(r));
    }

    [Fact]
    public void X13_CorteDespuesDelFinDeLaPersona_NuevaCompleta_ExistenteHistorica()
    {
        var r = Regenerar("2026-10-20",
            [PE(2, 2, "2026-10-01", "2026-10-10", T3, D3, nuevo: true), PE(3, 3, "2026-10-01", "2026-10-15", T3, D3, nuevo: false)],
            existentes: Guardado([Principal(3, 3, "2026-10-01", "2026-10-15", T3, D3)]));

        Assert.Equal(Dias(
            T(Pri, Auto, 1, P2, 2, "2026-10-01", "2026-10-05"),
            T(Pri, Auto, 2, P2, 2, "2026-10-08", "2026-10-10"),
            T(Des, Auto, 1, P2, 2, "2026-10-06", "2026-10-07")), Insertar(r));
        Assert.Equal(ClasePersona.Historica, r.Clases["P3"]);
        Assert.True(r.HayDiasAnterioresAlCorte);
        Assert.DoesNotContain(r.DiasAInsertar, d => d.Dia.Persona == P3);
    }

    [Fact]
    public void X14_Deduplicacion_GanaLaBase_YElDescansoManualOcupaElPrimerDiaLibre()
    {
        // Base: P 01–11 y D AUTO 12. Back nuevo DESCANSO 12–14 de la misma persona.
        // Regla de Generar (E6 / B3): si el primer día tras el bloque está ocupado por cualquier día regenerado, no hay AUTO.
        var r = Regenerar("2026-10-13", [P1Tipo2Existente],
            [BE(1, 1, "2026-10-12", "2026-10-14", TipoRegistroBack.Descanso, 0, nuevo: true)],
            Guardado([P1Tipo2Guardado]));

        var insertar = Insertar(r);
        Assert.DoesNotContain(insertar, d => d.Fecha == F("2026-10-12")); // ya existe en la base (EmpleadoId, Fecha, DESCANSO)
        Assert.Equal(Dias(T(Des, Man, 1, K1, 1, "2026-10-13", "2026-10-14")), Insertar(r, "K1"));
        Assert.DoesNotContain(insertar, d => d.Tipo == Auto && d.Rol == Des && d.Fecha <= F("2026-10-15")); // ningún AUTO del bloque 1
        Assert.Contains(new DiaAsignado(1, F("2026-10-27"), Des, Auto, 2, P1), insertar);
        Assert.Empty(r.CrucesInternos);
        Assert.Empty(r.CrucesHistoricos); // DESCANSO no cuenta
    }

    [Fact]
    public void X15_DiasExistentesPosterioresAlCorte_SeIgnoran()
    {
        // La entrada trae TODOS los días guardados (incluido el P del 25/10): no son base ni impiden insertarlos.
        var r = Regenerar("2026-10-20", [P1Tipo2Existente], existentes: Guardado([P1Tipo2Guardado]));

        Assert.Contains(new DiaAsignado(1, F("2026-10-25"), Pri, Auto, 2, P1), Insertar(r));
        Assert.DoesNotContain(r.Tramos, t => t.Inicio < F("2026-10-20") && t.Fin >= F("2026-10-20"));
    }

    [Fact]
    public void X16_EntradasInvalidas()
    {
        Assert.Throws<ArgumentNullException>(() => MotorCronograma.Regenerar(null!));
        Assert.Throws<ArgumentException>(() => Regenerar("2026-10-20", [PE(1, 1, "2026-10-10", "2026-10-09", T2, D2, nuevo: true)]));
        Assert.Throws<ArgumentException>(() => Regenerar("2026-10-20", [PE(1, 1, "2026-10-01", "2026-10-31", 0, D2, nuevo: true)]));
        Assert.Throws<ArgumentException>(() => Regenerar("2026-10-20", backs: [BE(1, 3, "2026-10-15", "2026-10-12", TipoRegistroBack.Jornada, 0, nuevo: true)]));
        Assert.Throws<ArgumentException>(() => Regenerar("2026-10-20",
            [PE(1, 1, "2026-10-01", "2026-10-31", T2, D2, nuevo: true), PE(1, 2, "2026-10-01", "2026-10-31", T2, D2, nuevo: true)]));
        Assert.Throws<ArgumentException>(() => Regenerar("2026-10-20", [PE(1, 1, "2026-10-01", "2026-10-31", T2, D2, nuevo: true, forzar: true)]));
        Assert.Throws<ArgumentException>(() => MotorCronograma.Regenerar(
            new SolicitudRegeneracion(F("2026-10-31"), F("2026-10-01"), F("2026-10-20"), [], [], [])));
    }

    [Fact]
    public void X17_ForzarHistorica_BackRecortadoNoRegeneraSuDescanso()
    {
        // Back recortado por una suspensión al 15/10 (sus descansos 16–17 se borraron); se reactiva con corte 16/10.
        var guardado = Guardado(backs: [Back(1, 3, "2026-10-10", "2026-10-15", TipoRegistroBack.Jornada, 0)]);

        var sinForzar = Regenerar("2026-10-16", backs: [BE(1, 3, "2026-10-10", "2026-10-15", TipoRegistroBack.Jornada, 2, nuevo: false)],
            existentes: guardado);
        var forzado = Regenerar("2026-10-16", backs: [BE(1, 3, "2026-10-10", "2026-10-15", TipoRegistroBack.Jornada, 2, nuevo: false, forzar: true)],
            existentes: guardado);

        Assert.Equal(ClasePersona.Vigente, sinForzar.Clases["K1"]); // 15 + 2 ≥ 16
        Assert.Equal(Dias(T(Des, Man, 1, K1, 3, "2026-10-16", "2026-10-17")), Insertar(sinForzar));
        Assert.Equal(ClasePersona.Historica, forzado.Clases["K1"]);
        Assert.Empty(forzado.DiasAInsertar);
    }

    [Fact]
    public void X18_M4_CambioDeJornadaConCorte05_ElDescansoNoSeSuperponeConElPrimerBloqueRegenerado()
    {
        // Caso real de la TAREA-17 (Id 9, 05/10/2026): P1 guardado TIPO_3 desde el 21/09 (proyecto 21/09–29/11);
        // ahora TIPO_2. Base < 05/10: P 21–25/09, D 26–27/09, P 28/09–02/10, D AUTO 03–04/10.
        // Sin M4 el descanso del tramo base 28/09–02/10 (4 días: 03–06/10) llegaba al PRINCIPAL regenerado del 06/10.
        var inicio = F("2026-09-21");
        var fin = F("2026-11-29");
        var guardado = MotorCronograma.Generar(new SolicitudCronograma(inicio, fin, [new PrincipalEntrada(1, 6, inicio, fin, T3, D3)], []))
            .DiasFinales.Select(d => new DiaExistente("P1", d)).ToList();

        var r = MotorCronograma.Regenerar(new SolicitudRegeneracion(inicio, fin, F("2026-10-05"),
            [new PrincipalEdicion("P1", 1, 6, inicio, fin, T2, D2, EsNuevo: false)], [], guardado));

        var insertar = Insertar(r);
        Assert.Equal([F("2026-10-05")], insertar.Where(d => d.Rol == Des && d.Fecha <= F("2026-10-06")).Select(d => d.Fecha));
        Assert.Equal(F("2026-10-06"), insertar.Where(d => d.Rol == Pri).Min(d => d.Fecha));
        Assert.Contains(T(Des, Auto, 2, P1, 6, "2026-10-05", "2026-10-05"), r.Tramos);
        Assert.Contains(T(Pri, Auto, 2, P1, 6, "2026-10-06", "2026-10-16"), r.Tramos);
        Assert.DoesNotContain(insertar.GroupBy(d => (d.EmpleadoId, d.Fecha)), g => g.Select(d => d.Rol).Distinct().Count() > 1);
    }
}
