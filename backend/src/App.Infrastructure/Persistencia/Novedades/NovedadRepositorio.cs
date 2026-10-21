using App.Application.Novedades;
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
internal sealed class NovedadRepositorio(ProfesiogramaDbContext db) : INovedadRepositorio
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

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    /// <summary>
    /// N7: novedad NO anulada con el mismo tipo de aplicación, el mismo tipo de novedad, el mismo sujeto (empleado o
    /// proyecto; GENERAL: ninguno) y al menos un día en [Desde, Hasta]. La más antigua primero.
    /// </summary>
    internal static IQueryable<DuplicadoFila> ConsultaDuplicado(ProfesiogramaDbContext db, CriterioDuplicado c)
    {
        var novedades = db.Novedades.AsNoTracking().Where(n => !n.Anulada
            && n.TipoAplicacionNovedadId == c.AplicacionId
            && n.TipoNovedadId == c.TipoNovedadId
            && n.Dias.Any(d => d.Fecha >= c.Desde && d.Fecha <= c.Hasta));

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
