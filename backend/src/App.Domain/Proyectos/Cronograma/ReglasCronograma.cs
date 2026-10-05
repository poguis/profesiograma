namespace App.Domain.Proyectos.Cronograma;

/// <summary>
/// Reglas comunes de MotorCronograma.Generar y MotorCronograma.Regenerar (FASE_5 §5). Extraídas de Generar sin
/// cambiar sus resultados: las pruebas E1–E10 y B1–B7 de la TAREA-10 lo verifican.
/// </summary>
internal static class ReglasCronograma
{
    /// <summary>§5.1 Bloques de trabajo de un principal, completos y anclados a SU fecha de inicio.</summary>
    internal static IEnumerable<Tramo> BloquesPrincipal(PrincipalEntrada principal)
    {
        var persona = new PersonaProyecto(RolCronograma.Principal, principal.Numero);
        var ciclo = principal.DiasTrabajo + principal.DiasDescanso;
        var totalDias = Dias(principal.Inicio, principal.Fin);
        var bloques = (totalDias + ciclo - 1) / ciclo; // techo(total / ciclo)

        for (var bloque = 1; bloque <= bloques; bloque++)
        {
            var inicioBloque = principal.Inicio.AddDays((bloque - 1) * ciclo);
            if (inicioBloque > principal.Fin)
            {
                yield break;
            }

            var finBloque = Minimo(inicioBloque.AddDays(principal.DiasTrabajo - 1), principal.Fin);
            yield return new Tramo(RolCronograma.Principal, TipoAsignacionCronograma.Auto, checked((short)bloque),
                persona, principal.EmpleadoId, inicioBloque, finBloque);
        }
    }

    /// <summary>§5.2 Tramo MANUAL del back (BACK o DESCANSO) + descanso posterior sin recortar al proyecto (P3).</summary>
    internal static IEnumerable<Tramo> TramosBack(BackEntrada back)
    {
        var persona = new PersonaProyecto(RolCronograma.Back, back.Numero);
        var rol = back.TipoRegistro == TipoRegistroBack.Descanso ? RolCronograma.Descanso : RolCronograma.Back;

        yield return new Tramo(rol, TipoAsignacionCronograma.Manual, back.Numero, persona, back.EmpleadoId, back.Inicio, back.Fin);

        if (back.TipoRegistro == TipoRegistroBack.Jornada && back.DiasDescanso > 0)
        {
            yield return new Tramo(RolCronograma.Descanso, TipoAsignacionCronograma.Manual, back.Numero, persona, back.EmpleadoId,
                back.Fin.AddDays(1), back.Fin.AddDays(back.DiasDescanso));
        }
    }

    /// <summary>
    /// §5.3 Descanso automático después de un bloque (P4): solo si el PRIMER día siguiente está libre (ningún día de la
    /// persona en <paramref name="ocupados"/>, cualquier rol) y hasta la fecha fin de ese principal (D3). null si no hay.
    /// M4 (TAREA-17): el descanso se corta en el primer día en que el mismo empleado ya tiene un día que no es DESCANSO
    /// (<paramref name="diasTrabajo"/>): ese día y los siguientes no se emiten. Evita PRINCIPAL + DESCANSO el mismo día
    /// cuando la jornada cambió (M3) y el descanso del último bloque de la base llega al primer bloque regenerado.
    /// </summary>
    internal static Tramo? DescansoAutomatico(Tramo bloque, DateOnly finPrincipal, byte diasDescanso,
        ISet<(int, DateOnly)> ocupados, ISet<(int, DateOnly)> diasTrabajo)
    {
        var siguiente = bloque.Fin.AddDays(1);
        if (diasDescanso == 0 || siguiente > finPrincipal || ocupados.Contains((bloque.EmpleadoId, siguiente)))
        {
            return null;
        }

        var finDescanso = Minimo(bloque.Fin.AddDays(diasDescanso), finPrincipal);
        for (var fecha = siguiente.AddDays(1); fecha <= finDescanso; fecha = fecha.AddDays(1))
        {
            if (diasTrabajo.Contains((bloque.EmpleadoId, fecha)))
            {
                finDescanso = fecha.AddDays(-1); // M4
                break;
            }
        }

        return new Tramo(RolCronograma.Descanso, TipoAsignacionCronograma.Auto, bloque.Bloque, bloque.Persona,
            bloque.EmpleadoId, siguiente, finDescanso);
    }

    /// <summary>Un día por fecha del tramo.</summary>
    internal static IEnumerable<DiaAsignado> Expandir(Tramo tramo)
    {
        for (var fecha = tramo.Inicio; fecha <= tramo.Fin; fecha = fecha.AddDays(1))
        {
            yield return new DiaAsignado(tramo.EmpleadoId, fecha, tramo.Rol, tramo.Tipo, tramo.Bloque, tramo.Persona);
        }
    }

    /// <summary>§5.4 Deduplicación por (EmpleadoId, Fecha, Rol): se conserva el primero.</summary>
    internal static List<T> Deduplicar<T>(IEnumerable<T> dias, Func<T, DiaAsignado> dia, HashSet<(int, DateOnly, RolCronograma)>? claves = null)
    {
        claves ??= [];
        return dias.Where(d => claves.Add((dia(d).EmpleadoId, dia(d).Fecha, dia(d).Rol))).ToList();
    }

    internal static void ValidarRango(DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio)
        {
            throw new ArgumentException("La fecha fin del proyecto es anterior a la fecha de inicio.", "solicitud");
        }
    }

    internal static void ValidarPrincipal(PrincipalEntrada principal)
    {
        if (principal.Fin < principal.Inicio)
        {
            throw new ArgumentException($"El principal {principal.Numero} tiene fecha fin anterior a la de inicio.", "solicitud");
        }

        if (principal.DiasTrabajo == 0)
        {
            throw new ArgumentException($"El principal {principal.Numero} tiene DiasTrabajo = 0.", "solicitud");
        }
    }

    internal static void ValidarBack(BackEntrada back)
    {
        if (back.Fin < back.Inicio)
        {
            throw new ArgumentException($"El back {back.Numero} tiene fecha fin anterior a la de inicio.", "solicitud");
        }
    }

    internal static int Dias(DateOnly inicio, DateOnly fin) => fin.DayNumber - inicio.DayNumber + 1;

    internal static DateOnly Minimo(DateOnly a, DateOnly b) => a <= b ? a : b;

    internal static DateOnly Maximo(DateOnly a, DateOnly b) => a >= b ? a : b;
}
