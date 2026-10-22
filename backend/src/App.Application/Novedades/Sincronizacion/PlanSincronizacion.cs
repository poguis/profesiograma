using App.Domain.Catalogos;

namespace App.Application.Novedades.Sincronizacion;

public enum AccionSincronizacion
{
    Nueva,
    NoActivaSinRegistro,
    SinCambios,
    Actualizar,
    Anular,
    Reactivar,
}

/// <summary>Plan: lo omitido, lo que no cambia (no entra a la transacción) y lo que hay que escribir.</summary>
public sealed record PlanSincronizacionResultado(
    IReadOnlyList<OmisionSincronizacion> Omisiones,
    int SinCambios,
    IReadOnlyList<(ItemSincronizacion Item, AccionSincronizacion Accion)> Escrituras);

/// <summary>
/// Compara lo que trae la fuente con lo guardado (TAREA-23-3, lógica pura). La misma <see cref="Clasificar"/> se usa
/// dentro de cada lote con la novedad releída (A1), así el plan y la escritura no pueden discrepar.
/// </summary>
public static class PlanSincronizacion
{
    public const string MotivoAnulacion = "Anulada en el origen (PERMISOS_MEDICOS)";

    /// <summary>
    /// Qué hacer con una fila válida. Nueva no activa → NoActivaSinRegistro (P4). Guardada: no activa y vigente → Anular;
    /// activa y anulada → Reactivar; si no, Actualizar si cambió algún campo comparado y SinCambios si no.
    /// </summary>
    public static AccionSincronizacion Clasificar(PermisoMedicoMapeado m, int empleadoId, NovedadExistente? e)
    {
        if (e is null)
        {
            return m.Activa ? AccionSincronizacion.Nueva : AccionSincronizacion.NoActivaSinRegistro;
        }

        if (!m.Activa && !e.Anulada)
        {
            return AccionSincronizacion.Anular;
        }

        if (m.Activa && e.Anulada)
        {
            return AccionSincronizacion.Reactivar;
        }

        return CamposCambiados(m, empleadoId, e) ? AccionSincronizacion.Actualizar : AccionSincronizacion.SinCambios;
    }

    /// <summary>Codigo, LegacyId, EmpleadoId, fechas, conjunto de días, OrigenArea, RegistradoPor* y DetalleOrigenJson.</summary>
    public static bool CamposCambiados(PermisoMedicoMapeado m, int empleadoId, NovedadExistente e) =>
        m.Codigo != e.Codigo
        || m.LegacyId != e.LegacyId
        || empleadoId != e.EmpleadoId
        || m.FechaInicio != e.FechaInicio
        || m.FechaFin != e.FechaFin
        || !m.Dias.SequenceEqual(e.Dias.Order())
        || m.OrigenArea != e.OrigenArea
        || m.RegistradoPorNombre != e.RegistradoPorNombre
        || m.RegistradoPorCorreo != e.RegistradoPorCorreo
        || m.DetalleOrigenJson != e.DetalleOrigenJson;

    /// <param name="empleado">EmpleadoId real o temporal (negativo) de la fila; null → sinEmpleado.</param>
    public static PlanSincronizacionResultado Armar(
        IReadOnlyList<PermisoMedicoMapeado> mapeados,
        IReadOnlyDictionary<Guid, NovedadExistente> existentes,
        IReadOnlyDictionary<string, Guid> duenosCodigo,
        IReadOnlyDictionary<int, Guid> duenosLegacy,
        Func<PermisoMedicoMapeado, int?> empleado)
    {
        var omisiones = new List<OmisionSincronizacion>();
        var escrituras = new List<(ItemSincronizacion, AccionSincronizacion)>();
        var sinCambios = 0;
        var uidsVistos = new HashSet<Guid>();
        var codigosVistos = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var legacyVistos = new Dictionary<int, Guid>();

        foreach (var m in mapeados)
        {
            void Omitir(string categoria) => omisiones.Add(new OmisionSincronizacion(categoria, m.Codigo));

            if (!uidsVistos.Add(m.Uid))
            {
                Omitir(CategoriaOmision.UidInvalido); // Uid repetido en la fuente: cuenta la primera fila
                continue;
            }

            existentes.TryGetValue(m.Uid, out var guardada);
            if (guardada is { OrigenId: not CatalogoIds.OrigenNovedad.PermisosMedicos })
            {
                Omitir(CategoriaOmision.ConflictoCodigo); // el Uid es de una novedad de PROFESIOGRAMA
                continue;
            }

            if (guardada is null && !m.Activa)
            {
                Omitir(CategoriaOmision.NoActivaSinRegistro);
                continue;
            }

            if ((duenosCodigo.TryGetValue(m.Codigo, out var duenoCodigo) && duenoCodigo != m.Uid)
                || (codigosVistos.TryGetValue(m.Codigo, out var otroCodigo) && otroCodigo != m.Uid))
            {
                Omitir(CategoriaOmision.ConflictoCodigo);
                continue;
            }

            if (m.LegacyId is int legacy
                && ((duenosLegacy.TryGetValue(legacy, out var duenoLegacy) && duenoLegacy != m.Uid)
                    || (legacyVistos.TryGetValue(legacy, out var otroLegacy) && otroLegacy != m.Uid)))
            {
                Omitir(CategoriaOmision.ConflictoLegacyId);
                continue;
            }

            if (empleado(m) is not int empleadoId)
            {
                Omitir(CategoriaOmision.SinEmpleado);
                continue;
            }

            codigosVistos[m.Codigo] = m.Uid;
            if (m.LegacyId is int l)
            {
                legacyVistos[l] = m.Uid;
            }

            var accion = Clasificar(m, empleadoId, guardada);
            if (accion == AccionSincronizacion.SinCambios)
            {
                sinCambios++;
            }
            else
            {
                escrituras.Add((new ItemSincronizacion(m, empleadoId), accion));
            }
        }

        return new PlanSincronizacionResultado(omisiones, sinCambios, escrituras);
    }
}
