namespace App.Domain.Proyectos.Cronograma;

/// <summary>Principal del proyecto. El cronograma se calcula con SUS fechas (FASE_5 §5.1).</summary>
/// <param name="Numero">Número del principal dentro del proyecto (ProyectoPersonal.Numero).</param>
/// <param name="DiasTrabajo">Días de trabajo de la jornada (debe ser &gt; 0).</param>
/// <param name="DiasDescanso">Días de descanso de la jornada (0 = sin descansos automáticos).</param>
public sealed record PrincipalEntrada(
    short Numero,
    int EmpleadoId,
    DateOnly Inicio,
    DateOnly Fin,
    byte DiasTrabajo,
    byte DiasDescanso);

/// <summary>Back del proyecto (FASE_5 §5.2).</summary>
/// <param name="Numero">Número del back dentro del proyecto; también es el bloque de sus días.</param>
/// <param name="DiasDescanso">Descanso posterior (solo si TipoRegistro = Jornada). No se recorta al rango del proyecto (P3).</param>
/// <param name="PrincipalRelacionado">Número del principal que cubre. Informativo: no interviene en el cálculo.</param>
public sealed record BackEntrada(
    short Numero,
    int EmpleadoId,
    DateOnly Inicio,
    DateOnly Fin,
    TipoRegistroBack TipoRegistro,
    byte DiasDescanso,
    short? PrincipalRelacionado = null);

/// <summary>
/// Datos para generar el cronograma. Del rango del proyecto el motor solo verifica fin &gt;= inicio;
/// que el personal esté dentro del rango (RN08) lo valida la capa Application.
/// </summary>
public sealed record SolicitudCronograma(
    DateOnly InicioProyecto,
    DateOnly FinProyecto,
    IReadOnlyList<PrincipalEntrada> Principales,
    IReadOnlyList<BackEntrada> Backs);
