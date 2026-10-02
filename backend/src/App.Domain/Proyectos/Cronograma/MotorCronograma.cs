namespace App.Domain.Proyectos.Cronograma;

/// <summary>
/// Motor de cronograma (RN04–RN06). Réplica de los botones Generar y Registrar de la app original
/// (docs/fases/FASE_5_Crear_Proyecto.md §5, decisiones P3/P4 de §11). Lógica pura: no lee la fecha del sistema,
/// no accede a datos y no valida reglas de usuario (eso es Application).
/// </summary>
public static partial class MotorCronograma
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
            foreach (var tramo in ReglasCronograma.BloquesPrincipal(principal))
            {
                Agregar(tramo, tramos, diasBase);
                bloquesPrincipales.Add((tramo, principal));
            }
        }

        // §5.2 Backs: tramo MANUAL (BACK o DESCANSO) + descanso posterior sin recortar al proyecto (P3).
        foreach (var back in solicitud.Backs)
        {
            foreach (var tramo in ReglasCronograma.TramosBack(back))
            {
                Agregar(tramo, tramos, diasBase);
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
            if (ReglasCronograma.DescansoAutomatico(bloque, principal.Fin, principal.DiasDescanso, ocupados) is { } descanso)
            {
                Agregar(descanso, tramos, diasAutomaticos);
            }
        }

        // §5.4 Deduplicación por (EmpleadoId, Fecha, Rol): se conserva el primero (principales → backs → automáticos).
        var diasFinales = ReglasCronograma.Deduplicar(diasBase.Concat(diasAutomaticos), d => d);

        return new ResultadoCronograma(tramos, diasBase, diasFinales, cruces);
    }

    private static void Validar(SolicitudCronograma solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Principales, nameof(solicitud.Principales));
        ArgumentNullException.ThrowIfNull(solicitud.Backs, nameof(solicitud.Backs));

        ReglasCronograma.ValidarRango(solicitud.InicioProyecto, solicitud.FinProyecto);
        foreach (var principal in solicitud.Principales)
        {
            ReglasCronograma.ValidarPrincipal(principal);
        }

        foreach (var back in solicitud.Backs)
        {
            ReglasCronograma.ValidarBack(back);
        }
    }

    private static void Agregar(Tramo tramo, List<Tramo> tramos, List<DiaAsignado> dias)
    {
        tramos.Add(tramo);
        dias.AddRange(ReglasCronograma.Expandir(tramo));
    }
}
