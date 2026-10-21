using App.Application.Novedades;
using App.Application.Proyectos.Estados;
using App.Domain.Catalogos;
using App.Domain.Novedades;
using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.Empleados;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Novedades;

/// <summary>
/// Escritura de "Crear novedad" (TAREA-23-1). Se ejecuta DENTRO de ITransaccionAsignaciones: la alta puntual de una
/// persona (con cédula, D15) comparte el applock con todas las escrituras de Empleado, y el duplicado (N7) se busca sin
/// carrera. Auditoría: AuditoriaInterceptor. DetalleOrigenJson y OrigenArea quedan en null (origen PROFESIOGRAMA).
/// </summary>
internal sealed class NovedadRepositorio(ProfesiogramaDbContext db) : INovedadRepositorio, INovedadModificacion
{
    public const int LargoRegistradoPorNombre = 200;
    public const int LargoRegistradoPorCorreo = 256;

    public async Task<DuplicadoNovedad?> BuscarDuplicadoAsync(CriterioDuplicado criterio, int? visibleParaUsuarioId, CancellationToken ct)
    {
        var duplicado = await ConsultaDuplicado(db, criterio).FirstOrDefaultAsync(ct);
        if (duplicado is null)
        {
            return null;
        }

        var visible = visibleParaUsuarioId is null
                      || await ConsultaVisible(db, duplicado.Id, visibleParaUsuarioId.Value).AnyAsync(ct);
        return new DuplicadoNovedad(duplicado.Codigo, visible);
    }

    public Task<bool> ExisteCodigoOUidAsync(string codigo, Guid uid, CancellationToken ct) =>
        ConsultaCodigoOUid(db, codigo, uid).AnyAsync(ct);

    public async Task<NovedadCreadaDto> AgregarAsync(NuevaNovedad nueva, CancellationToken ct)
    {
        var d = nueva.Datos;

        // PERSONA: alta puntual (fila nueva o refrescada con los datos frescos de la API); Id temporal → real.
        int? empleadoId = null;
        if (d.Empleado is { } empleado)
        {
            var ids = await AltaPuntualEmpleados.AplicarAsync(db, [empleado], d.Cedulas, ct);
            empleadoId = AltaPuntualEmpleados.IdReal(ids, empleado.Id);
        }

        var novedad = new Novedad
        {
            Uid = nueva.Uid,
            Codigo = nueva.Codigo,
            TipoAplicacionNovedadId = d.AplicacionId,
            TipoNovedadId = d.Tipo.Id,
            OrigenNovedadId = CatalogoIds.OrigenNovedad.Profesiograma,
            EmpleadoId = empleadoId,
            ProyectoId = d.ProyectoId,
            FechaInicio = d.FechaInicio,
            FechaFin = d.FechaFin,
            Observacion = d.Observacion,
            RegistradoPorNombre = Truncar(nueva.RegistradoPorNombre, LargoRegistradoPorNombre),
            RegistradoPorCorreo = Truncar(nueva.RegistradoPorCorreo, LargoRegistradoPorCorreo),
        };
        foreach (var fecha in nueva.Dias)
        {
            novedad.Dias.Add(new NovedadDia { Fecha = fecha });
        }

        db.Novedades.Add(novedad);
        await db.SaveChangesAsync(ct);

        // RowVer lo genera SQL Server al insertar y EF lo lee de vuelta: es la versión inicial (token de la 23-2).
        return new NovedadCreadaDto(novedad.Id, novedad.Codigo, Convert.ToBase64String(novedad.RowVer));
    }

    // ------------------------------------------------------------------ edición y anulación (TAREA-23-2)

    public Task<NovedadParaModificar?> ObtenerParaModificarAsync(int id, int? visibleParaUsuarioId, CancellationToken ct) =>
        ConsultaDatosModificacion(db, id, visibleParaUsuarioId).FirstOrDefaultAsync(ct);

    public async Task<string> EditarAsync(EdicionNovedad edicion, CancellationToken ct)
    {
        var novedad = await ConsultaParaModificar(db, edicion.Id).FirstAsync(ct);
        PrepararEdicion(db, novedad, edicion);
        await GuardarAsync(ct);
        return Convert.ToBase64String(novedad.RowVer); // A2: el RowVer que EF leyó tras el UPDATE (antes del commit)
    }

    public async Task<string> AnularAsync(AnulacionNovedad anulacion, CancellationToken ct)
    {
        var novedad = await ConsultaParaModificar(db, anulacion.Id).FirstAsync(ct);
        novedad.Anulada = true;
        novedad.FechaAnulacion = anulacion.FechaAnulacionUtc;
        novedad.AnuladaPorId = anulacion.AnuladaPorId;
        novedad.MotivoAnulacion = anulacion.Motivo;
        MarcarModificacion(db, novedad, anulacion.Token);
        await GuardarAsync(ct);
        return Convert.ToBase64String(novedad.RowVer);
    }

    /// <summary>
    /// Diff de días: se quitan los que salen y se agregan los que entran; los que quedan no se tocan (conservan su
    /// auditoría). Nunca se borra y se vuelve a insertar la misma fecha, así que UQ_NovedadDia no choca. FechaInicio y
    /// FechaFin = mínimo y máximo de los días. Todo va en el MISMO SaveChanges que el UPDATE de la novedad (R1).
    /// </summary>
    internal static void PrepararEdicion(ProfesiogramaDbContext db, Novedad novedad, EdicionNovedad edicion)
    {
        var (inicio, fin) = DiasNovedad.Limites(edicion.Dias);
        novedad.TipoNovedadId = edicion.TipoNovedadId;
        novedad.FechaInicio = inicio;
        novedad.FechaFin = fin;
        novedad.Observacion = edicion.Observacion;

        var nuevos = edicion.Dias.ToHashSet();
        foreach (var dia in novedad.Dias.Where(d => !nuevos.Contains(d.Fecha)).ToList())
        {
            db.NovedadDias.Remove(dia);
        }

        var actuales = novedad.Dias.Where(d => db.Entry(d).State != EntityState.Deleted).Select(d => d.Fecha).ToHashSet();
        foreach (var fecha in edicion.Dias.Where(f => !actuales.Contains(f)))
        {
            novedad.Dias.Add(new NovedadDia { Fecha = fecha });
        }

        MarcarModificacion(db, novedad, edicion.Token);
    }

    /// <summary>
    /// R1: el token del cliente es el OriginalValue de RowVer (WHERE RowVer = token: si otro cambió la novedad, 0 filas →
    /// 409) y FechaModificacion queda marcada aunque no cambie de valor (dos ediciones en el mismo segundo, o una que
    /// solo cambia días): así siempre hay UPDATE de Novedad y su rowversion cambia.
    /// </summary>
    internal static void MarcarModificacion(ProfesiogramaDbContext db, Novedad novedad, byte[] token)
    {
        var entrada = db.Entry(novedad);
        entrada.Property(n => n.RowVer).OriginalValue = token;
        entrada.Property(n => n.FechaModificacion).IsModified = true;
    }

    private async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoConcurrenciaException("La novedad fue modificada por otro proceso.", ex);
        }
    }

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    /// <summary>Novedad CON seguimiento y sus días, para editar o anular (la visibilidad ya se comprobó).</summary>
    internal static IQueryable<Novedad> ConsultaParaModificar(ProfesiogramaDbContext db, int novedadId) =>
        db.Novedades.Include(n => n.Dias).Where(n => n.Id == novedadId);

    /// <summary>Datos para decidir la edición o la anulación (sin seguimiento; visible según PermisosNovedad.VisiblePara).</summary>
    internal static IQueryable<NovedadParaModificar> ConsultaDatosModificacion(ProfesiogramaDbContext db, int novedadId, int? visibleParaUsuarioId) =>
        NovedadConsultas.Visibles(db, visibleParaUsuarioId).Where(n => n.Id == novedadId)
            .Select(n => new NovedadParaModificar(
                n.Id,
                n.TipoAplicacionNovedadId,
                n.TipoNovedadId,
                n.EmpleadoId,
                n.ProyectoId,
                n.OrigenNovedad.Codigo,
                n.OrigenNovedad.EsEditable,
                n.Proyecto != null ? n.Proyecto.PropietarioUsuarioId : null,
                n.Anulada,
                n.CreadoPorId,
                n.Observacion,
                n.Dias.OrderBy(d => d.Fecha).Select(d => d.Fecha).ToList(),
                n.RowVer));

    /// <summary>
    /// N7: novedad NO anulada con el mismo tipo de aplicación, el mismo tipo de novedad, el mismo sujeto (empleado o
    /// proyecto; GENERAL: ninguno) y al menos un día en [Desde, Hasta]. La más antigua primero.
    /// </summary>
    internal static IQueryable<DuplicadoFila> ConsultaDuplicado(ProfesiogramaDbContext db, CriterioDuplicado c)
    {
        var novedades = db.Novedades.AsNoTracking().Where(n => !n.Anulada
            && n.TipoAplicacionNovedadId == c.AplicacionId
            && n.TipoNovedadId == c.TipoNovedadId);

        // Creación: un día en el rango. Edición (TAREA-23-2): un día de los resultantes (el rango acota el índice).
        if (c.Dias is { } dias)
        {
            novedades = novedades.Where(n => n.Dias.Any(d => d.Fecha >= c.Desde && d.Fecha <= c.Hasta && dias.Contains(d.Fecha)));
        }
        else
        {
            novedades = novedades.Where(n => n.Dias.Any(d => d.Fecha >= c.Desde && d.Fecha <= c.Hasta));
        }

        if (c.ExcluirNovedadId is int propia)
        {
            novedades = novedades.Where(n => n.Id != propia);
        }

        if (c.EmpleadoId is int empleadoId)
        {
            novedades = novedades.Where(n => n.EmpleadoId == empleadoId);
        }

        if (c.ProyectoId is int proyectoId)
        {
            novedades = novedades.Where(n => n.ProyectoId == proyectoId);
        }

        return novedades.OrderBy(n => n.Id).Select(n => new DuplicadoFila(n.Id, n.Codigo));
    }

    /// <summary>¿La novedad es visible para el usuario? (misma regla que el listado: PermisosNovedad.VisiblePara).</summary>
    internal static IQueryable<Novedad> ConsultaVisible(ProfesiogramaDbContext db, int novedadId, int usuarioId) =>
        NovedadConsultas.Visibles(db, usuarioId).Where(n => n.Id == novedadId);

    internal static IQueryable<Novedad> ConsultaCodigoOUid(ProfesiogramaDbContext db, string codigo, Guid uid) =>
        db.Novedades.AsNoTracking().Where(n => n.Codigo == codigo || n.Uid == uid);

    internal sealed record DuplicadoFila(int Id, string Codigo);

    private static string? Truncar(string? valor, int maximo) =>
        valor is null ? null : valor.Length <= maximo ? valor : valor[..maximo];
}
