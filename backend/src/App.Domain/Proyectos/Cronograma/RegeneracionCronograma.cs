namespace App.Domain.Proyectos.Cronograma;

// Regeneración con fecha de corte (RN11, TAREA-16). Ver docs/fases/FASE_5_Edicion_Cronograma.md.

/// <summary>Principal para regenerar: fechas y jornada ACTUALES (editadas).</summary>
/// <param name="Clave">Clave estable: Id de ProyectoPersonal (existente) o clave local (nueva).</param>
/// <param name="EsNuevo">Persona que aún no está guardada: genera bloques completos, también antes del corte (M1).</param>
/// <param name="ForzarHistorica">Solo existentes: no se regenera aunque sus fechas lleguen al corte (decisión de Application).</param>
public sealed record PrincipalEdicion(
    string Clave,
    short Numero,
    int EmpleadoId,
    DateOnly Inicio,
    DateOnly Fin,
    byte DiasTrabajo,
    byte DiasDescanso,
    bool EsNuevo,
    bool ForzarHistorica = false);

/// <summary>Back para regenerar (ver PrincipalEdicion).</summary>
public sealed record BackEdicion(
    string Clave,
    short Numero,
    int EmpleadoId,
    DateOnly Inicio,
    DateOnly Fin,
    TipoRegistroBack TipoRegistro,
    byte DiasDescanso,
    bool EsNuevo,
    short? PrincipalRelacionado = null,
    bool ForzarHistorica = false);

/// <summary>Día guardado del proyecto con la clave de su persona. Puede tener cualquier fecha: solo Fecha &lt; Corte es base.</summary>
public sealed record DiaExistente(string Clave, DiaAsignado Dia);

/// <summary>
/// Datos para regenerar. Contrato con Infrastructure (TAREA-17): se borran los días del proyecto con Fecha ≥ Corte y se
/// insertan ResultadoRegeneracion.DiasAInsertar en la misma transacción (applock).
/// </summary>
public sealed record SolicitudRegeneracion(
    DateOnly InicioProyecto,
    DateOnly FinProyecto,
    DateOnly Corte,
    IReadOnlyList<PrincipalEdicion> Principales,
    IReadOnlyList<BackEdicion> Backs,
    IReadOnlyList<DiaExistente> DiasExistentes);

/// <summary>R2: Histórica (no se regenera), Vigente (se regenera desde el corte) o Nueva (completa, M1).</summary>
public enum ClasePersona
{
    Historica,
    Vigente,
    Nueva,
}

/// <summary>Día a insertar con la clave de su persona (Infrastructure la traduce a ProyectoPersonalId).</summary>
public sealed record DiaRegenerado(string Clave, DiaAsignado Dia);

/// <summary>Día regenerado (≠ DESCANSO) que repite persona y fecha con un día de la base (≠ DESCANSO): HISTORICO_PROPIO.</summary>
public sealed record CruceHistorico(int EmpleadoId, DateOnly Fecha, PersonaProyecto Regenerada, PersonaProyecto Existente);

/// <summary>Resultado de la regeneración (R8).</summary>
/// <param name="DiasAInsertar">Regenerados con Fecha ≥ Corte y todos los de personas nuevas; deduplicados también contra la base.</param>
/// <param name="HayDiasAnterioresAlCorte">Alguna persona nueva inserta días anteriores al corte (advertencia M1).</param>
/// <param name="Tramos">Tramos de la base (días &lt; Corte, continuos) + regenerados (principales → backs → descansos automáticos).</param>
/// <param name="CrucesInternos">Regenerados contra regenerados, rol ≠ DESCANSO, antes de los descansos automáticos.</param>
/// <param name="CrucesHistoricos">Regenerados contra la base (HISTORICO_PROPIO).</param>
/// <param name="DiasTrabajoRegenerados">Días regenerados con rol ≠ DESCANSO: personas y rango para consultar cruces con otros proyectos.</param>
/// <param name="Clases">Clasificación de cada persona por clave.</param>
public sealed record ResultadoRegeneracion(
    IReadOnlyList<DiaRegenerado> DiasAInsertar,
    bool HayDiasAnterioresAlCorte,
    IReadOnlyList<Tramo> Tramos,
    IReadOnlyList<CruceInterno> CrucesInternos,
    IReadOnlyList<CruceHistorico> CrucesHistoricos,
    IReadOnlyList<DiaAsignado> DiasTrabajoRegenerados,
    IReadOnlyDictionary<string, ClasePersona> Clases)
{
    public bool TieneCruces => CrucesInternos.Count > 0 || CrucesHistoricos.Count > 0;
}

public static partial class MotorCronograma
{
    /// <summary>
    /// Regenera el cronograma desde la fecha de corte (RN11), como Regenerar/Registrar de la app original con las
    /// correcciones M1 (personas nuevas: también sus días anteriores al corte) y M2 (los descansos AUTO de la base no
    /// ocupan el "primer día libre"). Con Corte = InicioProyecto, base vacía y todas las personas nuevas produce
    /// exactamente lo mismo que <see cref="Generar"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">La solicitud o sus listas son null.</exception>
    /// <exception cref="ArgumentException">Fechas inválidas, DiasTrabajo = 0, claves repetidas o nueva marcada como histórica.</exception>
    public static ResultadoRegeneracion Regenerar(SolicitudRegeneracion solicitud)
    {
        ValidarRegeneracion(solicitud);
        var corte = solicitud.Corte;

        // R2: clasificación.
        var clases = new Dictionary<string, ClasePersona>();
        foreach (var p in solicitud.Principales)
        {
            clases[p.Clave] = Clasificar(p.EsNuevo, p.ForzarHistorica, p.Fin, corte);
        }

        foreach (var b in solicitud.Backs)
        {
            clases[b.Clave] = Clasificar(b.EsNuevo, b.ForzarHistorica, b.Fin.AddDays(b.DiasDescanso), corte);
        }

        // R1: base = días guardados anteriores al corte (no se modifican).
        var baseDias = solicitud.DiasExistentes.Where(d => d.Dia.Fecha < corte).ToList();

        // R3/R4: tramos regenerados (vigentes recortados al corte; nuevas completas), en el orden de Generar.
        var tramos = new List<Tramo>();
        var regenerados = new List<DiaRegenerado>();
        foreach (var p in solicitud.Principales.Where(p => clases[p.Clave] != ClasePersona.Historica))
        {
            foreach (var bloque in ReglasCronograma.BloquesPrincipal(Entrada(p)))
            {
                AgregarRecortado(bloque, p.Clave, clases[p.Clave], corte, tramos, regenerados);
            }
        }

        foreach (var b in solicitud.Backs.Where(b => clases[b.Clave] != ClasePersona.Historica))
        {
            foreach (var tramo in ReglasCronograma.TramosBack(Entrada(b)))
            {
                AgregarRecortado(tramo, b.Clave, clases[b.Clave], corte, tramos, regenerados);
            }
        }

        // R5: cruces sobre los regenerados (rol ≠ DESCANSO), antes de los descansos automáticos.
        var crucesInternos = CruceDetector.DetectarInternos(regenerados.Select(d => d.Dia));
        var crucesHistoricos = DetectarHistoricos(regenerados, baseDias);

        // R6: descansos automáticos sobre base + regenerados. "Primer día libre": ocupado por cualquier día de la base
        // (salvo los descansos AUTO de principales, M2 como el original) o por cualquier día regenerado (regla de Generar).
        var ocupados = baseDias
            .Where(d => !(d.Dia.Rol == RolCronograma.Descanso && d.Dia.Tipo == TipoAsignacionCronograma.Auto))
            .Select(d => (d.Dia.EmpleadoId, d.Dia.Fecha))
            .Concat(regenerados.Select(d => (d.Dia.EmpleadoId, d.Dia.Fecha)))
            .ToHashSet();

        // M4: el descanso AUTO se corta en el primer día de trabajo (≠ DESCANSO) del mismo empleado, en la base o regenerado.
        var diasTrabajo = baseDias.Select(d => d.Dia).Concat(regenerados.Select(d => d.Dia))
            .Where(d => d.Rol != RolCronograma.Descanso)
            .Select(d => (d.EmpleadoId, d.Fecha))
            .ToHashSet();

        var automaticos = new List<DiaRegenerado>();
        foreach (var p in solicitud.Principales.Where(p => clases[p.Clave] != ClasePersona.Historica))
        {
            foreach (var bloque in BloquesParaDescanso(p, baseDias, regenerados))
            {
                if (ReglasCronograma.DescansoAutomatico(bloque, p.Fin, p.DiasDescanso, ocupados, diasTrabajo) is { } descanso)
                {
                    AgregarRecortado(descanso, p.Clave, clases[p.Clave], corte, tramos, automaticos);
                }
            }
        }

        // R7: deduplicación (EmpleadoId, Fecha, Rol) con la base primero: nunca se inserta una clave que ya existe.
        var clavesBase = baseDias.Select(d => (d.Dia.EmpleadoId, d.Dia.Fecha, d.Dia.Rol)).ToHashSet();
        var aInsertar = ReglasCronograma.Deduplicar(regenerados.Concat(automaticos), d => d.Dia, clavesBase);

        return new ResultadoRegeneracion(
            aInsertar,
            aInsertar.Any(d => d.Dia.Fecha < corte),
            [.. TramosDeLaBase(baseDias), .. tramos],
            crucesInternos,
            crucesHistoricos,
            regenerados.Select(d => d.Dia).Where(d => d.Rol != RolCronograma.Descanso).ToList(),
            clases);
    }

    private static ClasePersona Clasificar(bool esNuevo, bool forzarHistorica, DateOnly ultimoDia, DateOnly corte) =>
        esNuevo ? ClasePersona.Nueva
        : forzarHistorica || ultimoDia < corte ? ClasePersona.Historica
        : ClasePersona.Vigente;

    /// <summary>Vigente: solo la parte ≥ corte (se descarta si termina antes). Nueva: el tramo completo (M1).</summary>
    private static void AgregarRecortado(Tramo tramo, string clave, ClasePersona clase, DateOnly corte,
        List<Tramo> tramos, List<DiaRegenerado> dias)
    {
        if (clase == ClasePersona.Vigente)
        {
            if (tramo.Fin < corte)
            {
                return;
            }

            tramo = tramo with { Inicio = ReglasCronograma.Maximo(tramo.Inicio, corte) };
        }

        tramos.Add(tramo);
        dias.AddRange(ReglasCronograma.Expandir(tramo).Select(d => new DiaRegenerado(clave, d)));
    }

    /// <summary>
    /// Bloques del principal para revisar su descanso: tramos CONTINUOS de días PRINCIPAL por bloque, uniendo la base y
    /// los regenerados (como el GroupBy de Registrar en la app original). Con base vacía son los bloques de Generar.
    /// </summary>
    private static IEnumerable<Tramo> BloquesParaDescanso(
        PrincipalEdicion principal, List<DiaExistente> baseDias, List<DiaRegenerado> regenerados)
    {
        var persona = new PersonaProyecto(RolCronograma.Principal, principal.Numero);
        var dias = baseDias.Where(d => d.Clave == principal.Clave).Select(d => d.Dia)
            .Concat(regenerados.Where(d => d.Clave == principal.Clave).Select(d => d.Dia))
            .Where(d => d.Rol == RolCronograma.Principal)
            .OrderBy(d => d.Bloque).ThenBy(d => d.Fecha);

        return Continuos(dias, d => d.Bloque, (bloque, inicio, fin) => new Tramo(
            RolCronograma.Principal, TipoAsignacionCronograma.Auto, bloque, persona, principal.EmpleadoId, inicio, fin))
            .OrderBy(t => t.Inicio);
    }

    /// <summary>Tramos de la base para la vista previa: días continuos con la misma persona, rol, tipo y bloque.</summary>
    private static IEnumerable<Tramo> TramosDeLaBase(List<DiaExistente> baseDias) =>
        baseDias
            .Select(d => d.Dia)
            .GroupBy(d => (d.Persona, d.Rol, d.Tipo, d.Bloque, d.EmpleadoId))
            .SelectMany(g => Continuos(g.OrderBy(d => d.Fecha), _ => g.Key.Bloque,
                (bloque, inicio, fin) => new Tramo(g.Key.Rol, g.Key.Tipo, bloque, g.Key.Persona, g.Key.EmpleadoId, inicio, fin)))
            .OrderBy(t => t.Persona.Rol).ThenBy(t => t.Persona.Numero).ThenBy(t => t.Inicio).ThenBy(t => t.Rol);

    /// <summary>Corta una secuencia ordenada en tramos de fechas consecutivas con la misma clave.</summary>
    private static IEnumerable<Tramo> Continuos(IEnumerable<DiaAsignado> ordenados, Func<DiaAsignado, short> clave,
        Func<short, DateOnly, DateOnly, Tramo> crear)
    {
        short? actual = null;
        DateOnly inicio = default, fin = default;
        foreach (var dia in ordenados)
        {
            var k = clave(dia);
            if (actual == k && dia.Fecha == fin.AddDays(1))
            {
                fin = dia.Fecha;
                continue;
            }

            if (actual is short anterior)
            {
                yield return crear(anterior, inicio, fin);
            }

            (actual, inicio, fin) = (k, dia.Fecha, dia.Fecha);
        }

        if (actual is short ultimo)
        {
            yield return crear(ultimo, inicio, fin);
        }
    }

    /// <summary>R5 HISTORICO_PROPIO: día regenerado ≠ DESCANSO con un día de la base ≠ DESCANSO de la misma persona y fecha.</summary>
    private static IReadOnlyList<CruceHistorico> DetectarHistoricos(List<DiaRegenerado> regenerados, List<DiaExistente> baseDias)
    {
        var trabajoBase = baseDias
            .Select(d => d.Dia)
            .Where(d => d.Rol != RolCronograma.Descanso)
            .ToLookup(d => (d.EmpleadoId, d.Fecha));

        return regenerados
            .Select(d => d.Dia)
            .Where(d => d.Rol != RolCronograma.Descanso)
            .SelectMany(r => trabajoBase[(r.EmpleadoId, r.Fecha)].Select(b => new CruceHistorico(r.EmpleadoId, r.Fecha, r.Persona, b.Persona)))
            .Distinct()
            .OrderBy(c => c.Fecha).ThenBy(c => c.EmpleadoId)
            .ToList();
    }

    private static PrincipalEntrada Entrada(PrincipalEdicion p) =>
        new(p.Numero, p.EmpleadoId, p.Inicio, p.Fin, p.DiasTrabajo, p.DiasDescanso);

    private static BackEntrada Entrada(BackEdicion b) =>
        new(b.Numero, b.EmpleadoId, b.Inicio, b.Fin, b.TipoRegistro, b.DiasDescanso, b.PrincipalRelacionado);

    private static void ValidarRegeneracion(SolicitudRegeneracion solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Principales, nameof(solicitud.Principales));
        ArgumentNullException.ThrowIfNull(solicitud.Backs, nameof(solicitud.Backs));
        ArgumentNullException.ThrowIfNull(solicitud.DiasExistentes, nameof(solicitud.DiasExistentes));

        ReglasCronograma.ValidarRango(solicitud.InicioProyecto, solicitud.FinProyecto);
        foreach (var p in solicitud.Principales)
        {
            ReglasCronograma.ValidarPrincipal(Entrada(p));
            ValidarMarcas(p.Clave, p.EsNuevo, p.ForzarHistorica);
        }

        foreach (var b in solicitud.Backs)
        {
            ReglasCronograma.ValidarBack(Entrada(b));
            ValidarMarcas(b.Clave, b.EsNuevo, b.ForzarHistorica);
        }

        var repetida = solicitud.Principales.Select(p => p.Clave).Concat(solicitud.Backs.Select(b => b.Clave))
            .GroupBy(c => c).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null)
        {
            throw new ArgumentException($"La clave de persona '{repetida.Key}' está repetida.", nameof(solicitud));
        }
    }

    private static void ValidarMarcas(string clave, bool esNuevo, bool forzarHistorica)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new ArgumentException("Toda persona debe tener una clave.", "solicitud");
        }

        if (esNuevo && forzarHistorica)
        {
            throw new ArgumentException($"La persona '{clave}' es nueva: no puede marcarse como histórica.", "solicitud");
        }
    }
}
