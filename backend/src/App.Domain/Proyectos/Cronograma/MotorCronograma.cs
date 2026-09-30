namespace App.Domain.Proyectos.Cronograma;

/// <summary>
/// Motor de cronograma (RN04–RN06). Réplica de los botones Generar y Registrar de la app original
/// (docs/fases/FASE_5_Crear_Proyecto.md §5, decisiones P3/P4 de §11). Lógica pura: no lee la fecha del sistema,
/// no accede a datos y no valida reglas de usuario (eso es Application).
/// </summary>
public static class MotorCronograma
{
    /// <exception cref="ArgumentNullException">La solicitud o sus listas son null.</exception>
    /// <exception cref="ArgumentException">Fin anterior al inicio (proyecto, principal o back) o DiasTrabajo = 0.</exception>
    public static ResultadoCronograma Generar(SolicitudCronograma solicitud)
    {
        Validar(solicitud);

        var tramos = new List<Tramo>();
        var diasBase = new List<DiaAsignado>();
        var bloquesPrincipales = new List<(Tramo Tramo, PrincipalEntrada Principal)>();

        // §5.1 Principales: bloques de trabajo por ciclo (trabajo + descanso) dentro de las fechas del principal.
        foreach (var principal in solicitud.Principales)
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
                    break;
                }

                var finBloque = Minimo(inicioBloque.AddDays(principal.DiasTrabajo - 1), principal.Fin);
                var tramo = new Tramo(RolCronograma.Principal, TipoAsignacionCronograma.Auto, checked((short)bloque),
                    persona, principal.EmpleadoId, inicioBloque, finBloque);

                Agregar(tramo, tramos, diasBase);
                bloquesPrincipales.Add((tramo, principal));
            }
        }

        // §5.2 Backs: tramo MANUAL (BACK o DESCANSO) + descanso posterior sin recortar al proyecto (P3).
        foreach (var back in solicitud.Backs)
        {
            var persona = new PersonaProyecto(RolCronograma.Back, back.Numero);
            var rol = back.TipoRegistro == TipoRegistroBack.Descanso ? RolCronograma.Descanso : RolCronograma.Back;

            Agregar(new Tramo(rol, TipoAsignacionCronograma.Manual, back.Numero, persona, back.EmpleadoId, back.Inicio, back.Fin),
                tramos, diasBase);

            if (back.TipoRegistro == TipoRegistroBack.Jornada && back.DiasDescanso > 0)
            {
                Agregar(new Tramo(RolCronograma.Descanso, TipoAsignacionCronograma.Manual, back.Numero, persona, back.EmpleadoId,
                        back.Fin.AddDays(1), back.Fin.AddDays(back.DiasDescanso)),
                    tramos, diasBase);
            }
        }

        // §6 Cruces internos: sobre los días base, antes de los descansos automáticos (D1).
        var cruces = CruceDetector.DetectarInternos(diasBase);

        // §5.3 Descansos automáticos del principal (P4): solo si el PRIMER día siguiente al bloque está libre
        // (ningún día de la persona en los días base, cualquier rol) y hasta la fecha fin de ese principal (D3).
        var ocupados = diasBase.Select(d => (d.EmpleadoId, d.Fecha)).ToHashSet();
        var diasAutomaticos = new List<DiaAsignado>();

        foreach (var (bloque, principal) in bloquesPrincipales)
        {
            var siguiente = bloque.Fin.AddDays(1);
            if (principal.DiasDescanso == 0 || siguiente > principal.Fin || ocupados.Contains((principal.EmpleadoId, siguiente)))
            {
                continue;
            }

            var finDescanso = Minimo(bloque.Fin.AddDays(principal.DiasDescanso), principal.Fin);
            Agregar(new Tramo(RolCronograma.Descanso, TipoAsignacionCronograma.Auto, bloque.Bloque, bloque.Persona,
                    principal.EmpleadoId, siguiente, finDescanso),
                tramos, diasAutomaticos);
        }

        // §5.4 Deduplicación por (EmpleadoId, Fecha, Rol): se conserva el primero (principales → backs → automáticos).
        var claves = new HashSet<(int, DateOnly, RolCronograma)>();
        var diasFinales = diasBase.Concat(diasAutomaticos)
            .Where(d => claves.Add((d.EmpleadoId, d.Fecha, d.Rol)))
            .ToList();

        return new ResultadoCronograma(tramos, diasBase, diasFinales, cruces);
    }

    private static void Validar(SolicitudCronograma solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Principales, nameof(solicitud.Principales));
        ArgumentNullException.ThrowIfNull(solicitud.Backs, nameof(solicitud.Backs));

        if (solicitud.FinProyecto < solicitud.InicioProyecto)
        {
            throw new ArgumentException("La fecha fin del proyecto es anterior a la fecha de inicio.", nameof(solicitud));
        }

        foreach (var principal in solicitud.Principales)
        {
            if (principal.Fin < principal.Inicio)
            {
                throw new ArgumentException($"El principal {principal.Numero} tiene fecha fin anterior a la de inicio.", nameof(solicitud));
            }

            if (principal.DiasTrabajo == 0)
            {
                throw new ArgumentException($"El principal {principal.Numero} tiene DiasTrabajo = 0.", nameof(solicitud));
            }
        }

        foreach (var back in solicitud.Backs)
        {
            if (back.Fin < back.Inicio)
            {
                throw new ArgumentException($"El back {back.Numero} tiene fecha fin anterior a la de inicio.", nameof(solicitud));
            }
        }
    }

    private static void Agregar(Tramo tramo, List<Tramo> tramos, List<DiaAsignado> dias)
    {
        tramos.Add(tramo);
        for (var fecha = tramo.Inicio; fecha <= tramo.Fin; fecha = fecha.AddDays(1))
        {
            dias.Add(new DiaAsignado(tramo.EmpleadoId, fecha, tramo.Rol, tramo.Tipo, tramo.Bloque, tramo.Persona));
        }
    }

    private static int Dias(DateOnly inicio, DateOnly fin) => fin.DayNumber - inicio.DayNumber + 1;

    private static DateOnly Minimo(DateOnly a, DateOnly b) => a <= b ? a : b;
}
