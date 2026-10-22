using App.Application.Novedades;
using App.Application.Novedades.Sincronizacion;
using App.Domain.Catalogos;
using App.Domain.Novedades;
using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Empleados;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Novedades;

/// <summary>
/// Lecturas masivas y escritura por lotes de la sincronización de PERMISO MEDICO (TAREA-23-3). Lecturas sin seguimiento,
/// en bloques de 500 valores (EF 10 traduce Contains a IN con un parámetro por valor; límite de SQL Server: 2100).
/// AplicarLoteAsync se ejecuta DENTRO de ITransaccionAsignaciones: relee CON seguimiento las del lote y las reclasifica
/// con la misma regla del plan (A1: un Uid «nuevo» que ya existe se trata como actualización o sin cambios; nunca se
/// duplica). Lo que no cambia no se toca (sin UPDATE: RowVer y FechaModificacion quedan iguales).
/// </summary>
internal sealed class SincronizacionNovedadesRepositorio(ProfesiogramaDbContext db) : ISincronizacionNovedades
{
    public const int Bloque = 500;

    public async Task<IReadOnlyList<NovedadExistente>> ObtenerExistentesPorUidAsync(IReadOnlyCollection<Guid> uids, CancellationToken ct)
    {
        var resultado = new List<NovedadExistente>();
        foreach (var bloque in uids.Chunk(Bloque))
        {
            resultado.AddRange(await ConsultaExistentesPorUid(db, bloque).ToListAsync(ct));
        }

        return resultado;
    }

    public async Task<IReadOnlyDictionary<string, Guid>> ObtenerDuenosCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct)
    {
        var resultado = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var bloque in codigos.Chunk(Bloque))
        {
            foreach (var (codigo, uid) in await ConsultaCodigos(db, bloque).ToListAsync(ct))
            {
                resultado[codigo] = uid;
            }
        }

        return resultado;
    }

    public async Task<IReadOnlyDictionary<int, Guid>> ObtenerDuenosLegacyAsync(IReadOnlyCollection<int> legacyIds, CancellationToken ct)
    {
        var resultado = new Dictionary<int, Guid>();
        foreach (var bloque in legacyIds.Chunk(Bloque))
        {
            foreach (var (legacy, uid) in await ConsultaLegacyIds(db, bloque).ToListAsync(ct))
            {
                resultado[legacy] = uid;
            }
        }

        return resultado;
    }

    public async Task<IReadOnlyList<EmpleadoPorCorreo>> ObtenerEmpleadosPorCorreoAsync(IReadOnlyCollection<string> correos, CancellationToken ct)
    {
        var resultado = new List<EmpleadoPorCorreo>();
        foreach (var bloque in correos.Chunk(Bloque))
        {
            resultado.AddRange(await ConsultaEmpleadosPorCorreo(db, bloque).ToListAsync(ct));
        }

        return resultado;
    }

    public async Task<IReadOnlyList<Guid>> ObtenerUidsPermisosMedicosAsync(CancellationToken ct) =>
        await ConsultaUidsPermisosMedicos(db).ToListAsync(ct);

    public async Task<ResultadoLote> AplicarLoteAsync(LoteSincronizacion lote, CancellationToken ct)
    {
        // Alta puntual (con cédula, D15) de las personas sin fila, activas en la API; Id temporal → real.
        var ids = lote.Altas.Count == 0
            ? new Dictionary<int, int>()
            : await AltaPuntualEmpleados.AplicarAsync(db, lote.Altas, lote.Cedulas, ct);

        var guardadas = (await ConsultaParaSincronizar(db, lote.Items.Select(i => i.Mapeado.Uid).ToList()).ToListAsync(ct))
            .ToDictionary(n => n.Uid);

        int nuevas = 0, actualizadas = 0, anuladas = 0, reactivadas = 0, sinCambios = 0;
        foreach (var item in lote.Items)
        {
            var m = item.Mapeado;
            var empleadoId = item.EmpleadoId > 0 ? item.EmpleadoId : AltaPuntualEmpleados.IdReal(ids, item.EmpleadoId);
            guardadas.TryGetValue(m.Uid, out var novedad);
            if (novedad is { OrigenNovedadId: not CatalogoIds.OrigenNovedad.PermisosMedicos })
            {
                continue; // el Uid es de una novedad de PROFESIOGRAMA (el plan ya la omitió)
            }

            switch (PlanSincronizacion.Clasificar(m, empleadoId, novedad is null ? null : Instantanea(novedad)))
            {
                case AccionSincronizacion.Nueva:
                    db.Novedades.Add(Nueva(m, empleadoId));
                    nuevas++;
                    break;
                case AccionSincronizacion.Actualizar:
                    Asignar(novedad!, m, empleadoId);
                    actualizadas++;
                    break;
                case AccionSincronizacion.Anular:
                    Asignar(novedad!, m, empleadoId);
                    novedad!.Anulada = true;
                    novedad.FechaAnulacion = lote.AhoraUtc;
                    novedad.AnuladaPorId = Usuario.IdSistema; // P5
                    novedad.MotivoAnulacion = PlanSincronizacion.MotivoAnulacion;
                    anuladas++;
                    break;
                case AccionSincronizacion.Reactivar:
                    Asignar(novedad!, m, empleadoId);
                    novedad!.Anulada = false;
                    novedad.FechaAnulacion = null;
                    novedad.AnuladaPorId = null;
                    novedad.MotivoAnulacion = null;
                    reactivadas++;
                    break;
                case AccionSincronizacion.SinCambios:
                    sinCambios++;
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
        return new ResultadoLote(nuevas, actualizadas, anuladas, reactivadas, sinCambios);
    }

    /// <summary>R5: CreadoPorId = SISTEMA; FechaCreacion = Registro.FechaRegistro en UTC (si falta, la pone el interceptor).</summary>
    internal static Novedad Nueva(PermisoMedicoMapeado m, int empleadoId)
    {
        var novedad = new Novedad
        {
            Uid = m.Uid,
            TipoAplicacionNovedadId = CatalogoIds.TipoAplicacionNovedad.Persona,
            TipoNovedadId = CatalogoIds.TipoNovedad.PermisoMedico,
            OrigenNovedadId = CatalogoIds.OrigenNovedad.PermisosMedicos,
            CreadoPorId = Usuario.IdSistema,
            FechaCreacion = m.FechaRegistroUtc ?? default,
        };
        Asignar(novedad, m, empleadoId);
        return novedad;
    }

    /// <summary>
    /// Campos comparados por el plan; EF solo marca los que cambian de valor. Días: diff (sin borrar y reinsertar la misma
    /// fecha). Observacion siempre null (N13); DetalleOrigenJson = lista blanca.
    /// </summary>
    internal static void Asignar(Novedad n, PermisoMedicoMapeado m, int empleadoId)
    {
        n.Codigo = m.Codigo;
        n.LegacyId = m.LegacyId;
        n.EmpleadoId = empleadoId;
        n.FechaInicio = m.FechaInicio;
        n.FechaFin = m.FechaFin;
        n.OrigenArea = m.OrigenArea;
        n.RegistradoPorNombre = m.RegistradoPorNombre;
        n.RegistradoPorCorreo = m.RegistradoPorCorreo;
        n.DetalleOrigenJson = m.DetalleOrigenJson;
        n.Observacion = null;

        var nuevos = m.Dias.ToHashSet();
        foreach (var dia in n.Dias.Where(d => !nuevos.Contains(d.Fecha)).ToList())
        {
            n.Dias.Remove(dia); // FK requerida: EF borra el día huérfano
        }

        var actuales = n.Dias.Select(d => d.Fecha).ToHashSet();
        foreach (var fecha in m.Dias.Where(f => !actuales.Contains(f)))
        {
            n.Dias.Add(new NovedadDia { Fecha = fecha, CreadoPorId = n.Id == 0 ? Usuario.IdSistema : 0 });
        }
    }

    private static NovedadExistente Instantanea(Novedad n) => new(
        n.Id, n.Uid, n.OrigenNovedadId, n.Codigo, n.LegacyId, n.EmpleadoId, n.FechaInicio, n.FechaFin,
        n.Dias.Select(d => d.Fecha).Order().ToList(), n.OrigenArea, n.RegistradoPorNombre, n.RegistradoPorCorreo,
        n.DetalleOrigenJson, n.Anulada);

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    internal static IQueryable<NovedadExistente> ConsultaExistentesPorUid(ProfesiogramaDbContext db, IReadOnlyCollection<Guid> uids) =>
        db.Novedades.AsNoTracking().Where(n => uids.Contains(n.Uid))
            .Select(n => new NovedadExistente(
                n.Id, n.Uid, n.OrigenNovedadId, n.Codigo, n.LegacyId, n.EmpleadoId, n.FechaInicio, n.FechaFin,
                n.Dias.OrderBy(d => d.Fecha).Select(d => d.Fecha).ToList(),
                n.OrigenArea, n.RegistradoPorNombre, n.RegistradoPorCorreo, n.DetalleOrigenJson, n.Anulada));

    internal static IQueryable<(string Codigo, Guid Uid)> ConsultaCodigos(ProfesiogramaDbContext db, IReadOnlyCollection<string> codigos) =>
        db.Novedades.AsNoTracking().Where(n => codigos.Contains(n.Codigo)).Select(n => ValueTuple.Create(n.Codigo, n.Uid));

    internal static IQueryable<(int LegacyId, Guid Uid)> ConsultaLegacyIds(ProfesiogramaDbContext db, IReadOnlyCollection<int> legacyIds) =>
        db.Novedades.AsNoTracking()
            .Where(n => n.OrigenNovedadId == CatalogoIds.OrigenNovedad.PermisosMedicos && n.LegacyId != null && legacyIds.Contains(n.LegacyId.Value))
            .Select(n => ValueTuple.Create(n.LegacyId!.Value, n.Uid));

    /// <summary>P2: filas de Empleado por CorreoEmpresa (la colación de la base no distingue mayúsculas). Sin cédula.</summary>
    internal static IQueryable<EmpleadoPorCorreo> ConsultaEmpleadosPorCorreo(ProfesiogramaDbContext db, IReadOnlyCollection<string> correos) =>
        db.Empleados.AsNoTracking().Where(e => e.CorreoEmpresa != null && correos.Contains(e.CorreoEmpresa))
            .Select(e => new EmpleadoPorCorreo(e.Id, e.CodigoEkon, e.CorreoEmpresa!));

    internal static IQueryable<Guid> ConsultaUidsPermisosMedicos(ProfesiogramaDbContext db) =>
        db.Novedades.AsNoTracking().Where(n => n.OrigenNovedadId == CatalogoIds.OrigenNovedad.PermisosMedicos).Select(n => n.Uid);

    /// <summary>Las novedades del lote CON seguimiento y sus días (dentro de la transacción).</summary>
    internal static IQueryable<Novedad> ConsultaParaSincronizar(ProfesiogramaDbContext db, IReadOnlyCollection<Guid> uids) =>
        db.Novedades.Include(n => n.Dias).Where(n => uids.Contains(n.Uid));
}
