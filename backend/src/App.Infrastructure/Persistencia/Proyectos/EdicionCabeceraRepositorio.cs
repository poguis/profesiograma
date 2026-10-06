using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Estados;
using App.Domain.Catalogos;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Lectura y escritura de "Editar cabecera" (TAREA-18). AplicarAsync debe ejecutarse dentro de ITransaccionAsignaciones.
/// Orden (el de la TAREA-14, EscrituraRecorte):
///  1) si se acorta la fecha fin: ExecuteDelete de los días &gt; F, referencias a principales eliminados en null y recorte
///     de FechaFin del personal → SaveChanges 1;
///  2) personal eliminado, descanso posterior de los backs que quedan (H15), actividades (eliminadas, con fechas nuevas y
///     la nueva CAMBIO_ACTIVIDAD), proyecto (fechas,
///     horario, almuerzo; RowVer) y etapa → SaveChanges 2.
/// Proyecto, personal, actividades y etapa con seguimiento: AuditoriaInterceptor llena la auditoría.
/// </summary>
internal sealed class EdicionCabeceraRepositorio(ProfesiogramaDbContext db) : ICabeceraRepositorio
{
    private static readonly int[] ErroresUnicos = [2601, 2627];

    public async Task<DatosCabecera?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly fechaDias, CancellationToken ct)
    {
        var c = await ConsultaCabeceraEdicion(db, proyectoId, propietarioUsuarioId).FirstOrDefaultAsync(ct);
        if (c is null)
        {
            return null;
        }

        var personal = await EdicionPersonalRepositorio.ConsultaPersonalEdicion(db, proyectoId).ToListAsync(ct);
        var dias = fechaDias == DateOnly.MaxValue
            ? []
            : await CambioEstadoRepositorio.ConsultaDiasPosteriores(db, proyectoId, fechaDias).ToListAsync(ct);
        var actividades = await ConsultaActividadesEdicion(db, proyectoId).ToListAsync(ct);

        return new DatosCabecera(
            new CabeceraGuardada(c.Id, c.Codigo, c.EstadoCodigo, c.FechaInicio, c.FechaFin, c.GrupoCodigo, c.RequiereProyectoErp,
                c.CompaniaId, c.ProyectoErpId, c.HorarioCodigo, c.HorarioDescripcion, c.HoraEntrada, c.HoraSalida,
                c.SalidaAlmuerzo, c.RegresoAlmuerzo, c.RowVer),
            personal.Select(p => new PersonalCambio(
                new PersonaCorte(p.Id, (RolCronograma)p.RolAsignacionId, p.Numero, p.EmpleadoId, p.FechaInicio, p.FechaFin, p.PrincipalRelacionadoId),
                p.CodigoEkon, p.NombreCompleto, p.JornadaCodigo, p.DiasTrabajo, p.DiasDescanso, p.TipoRegistro)).ToList(),
            personal.Where(p => p.EsPrincipalInicial).Select(p => p.Id).ToHashSet(),
            dias.Select(d => new DiaCorte(d.PersonalId, d.Fecha, (RolCronograma)d.RolAsignacionId)).ToList(),
            actividades.Select(a => new ActividadGuardada(a.Id, a.Version, a.ActividadCodigo, a.ActividadDescripcion, a.ActividadTipo,
                a.TipoMovimiento, a.FechaInicio, a.FechaFin)).ToList());
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct) =>
        CambioEstadoRepositorio.ConsultaUltimaVersion(db, proyectoId).FirstOrDefaultAsync(ct);

    public async Task AplicarAsync(CambioCabeceraAplicar cambio, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cambio);
        var idProyecto = cambio.ProyectoId;

        // 1) Recorte del personal y de los días (C4). Las actividades se aplican abajo con el resultado final.
        List<ProyectoPersonal>? personal = null;
        if (cambio.Recorte is { } recorte)
        {
            personal = await EscrituraRecorte.AplicarAntesDeGuardarAsync(db, idProyecto, recorte, ct);
        }

        var tipoEtapaId = await CambioEstadoRepositorio.ConsultaIdTipoMovimiento(db, cambio.TipoMovimiento).FirstAsync(ct);
        var proyecto = await CambioEstadoRepositorio.ConsultaProyectoSeguimiento(db, idProyecto).FirstAsync(ct);
        var actividades = await CambioEstadoRepositorio.ConsultaActividadesSeguimiento(db, idProyecto).ToListAsync(ct);

        if (personal is not null)
        {
            await GuardarAsync(ct);
            EscrituraRecorte.AplicarPersonalYActividades(
                db, cambio.Recorte! with { ActividadesEliminadas = [], ActividadesRecortadas = [] }, personal, []);
        }

        // 2) Actividades: eliminadas, con fechas nuevas (P1, C3, C4, C6) y la nueva (C6).
        var eliminadas = cambio.ActividadesEliminadas.ToHashSet();
        db.ProyectoActividades.RemoveRange(actividades.Where(a => eliminadas.Contains(a.Id)));
        var modificadas = cambio.ActividadesModificadas.ToDictionary(m => m.Id);
        foreach (var a in actividades.Where(a => modificadas.ContainsKey(a.Id)))
        {
            a.FechaInicio = modificadas[a.Id].FechaInicio;
            a.FechaFin = modificadas[a.Id].FechaFin;
        }

        if (cambio.ActividadNueva is { } nueva)
        {
            db.ProyectoActividades.Add(new ProyectoActividad
            {
                ProyectoId = idProyecto,
                Version = nueva.Version,
                TipoMovimientoId = CatalogoIds.TipoMovimiento.CambioActividad,
                FechaInicio = nueva.FechaInicio,
                FechaFin = nueva.FechaFin,
                ActividadCodigo = nueva.Codigo,
                ActividadDescripcion = nueva.Descripcion,
                ActividadTipo = nueva.Tipo,
            });
        }

        // H15: descanso posterior de los backs que quedan (los días > F ya se borraron en el paso 1: sin choque con la UQ).
        db.ProyectoAsignacionesDia.AddRange(cambio.DescansosAgregados.Select(d => new ProyectoAsignacionDia
        {
            ProyectoId = idProyecto,
            ProyectoPersonalId = d.PersonalId,
            EmpleadoId = d.Dia.EmpleadoId,
            Fecha = d.Dia.Fecha,
            RolAsignacionId = (byte)d.Dia.Rol,
            TipoAsignacion = ProyectoAsignacionDia.TipoManual,
            Bloque = d.Dia.Bloque,
        }));

        // Proyecto (H13: aquí sí se guarda FechaInicio). Almuerzo: valores resultantes. Horario: solo si cambia.
        proyecto.FechaInicio = cambio.FechaInicio;
        proyecto.FechaFin = cambio.FechaFin;
        proyecto.SalidaAlmuerzo = cambio.SalidaAlmuerzo;
        proyecto.RegresoAlmuerzo = cambio.RegresoAlmuerzo;
        if (cambio.Horario is { } h)
        {
            proyecto.HorarioCodigo = h.Codigo;
            proyecto.HorarioDescripcion = h.Descripcion;
            proyecto.HoraEntrada = h.HoraEntrada;
            proyecto.HoraSalida = h.HoraSalida;
            proyecto.HorasJornadaMin = h.MinutosJornada;
            proyecto.HorasTrabajadasMin = h.MinutosTrabajados;
            proyecto.TipoHorario = h.Tipo;
        }

        // C7: etapa (versión = máx + 1 dentro del applock; UQ_ProyectoEtapa_Version como red de seguridad).
        db.ProyectoEtapas.Add(new ProyectoEtapa
        {
            ProyectoId = idProyecto,
            Version = cambio.Version,
            TipoMovimientoId = tipoEtapaId,
            EstadoProyectoId = CatalogoIds.EstadoProyecto.Activo, // solo proyectos ACTIVO; el estado no cambia
            FechaInicio = cambio.FechaInicio,
            FechaFin = cambio.FechaFin,
            FechaCorte = cambio.FechaCorte,
            ActividadCodigo = cambio.ActividadCodigo,
            SnapshotPersonal = cambio.SnapshotPersonal,
        });

        await GuardarAsync(ct);
    }

    private async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoConcurrenciaException("El proyecto fue modificado por otro proceso.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && ErroresUnicos.Contains(sql.Number))
        {
            throw new ConflictoConcurrenciaException("Otra etapa o actividad del proyecto se registró al mismo tiempo.", ex);
        }
    }

    // ------------------------------------------------------------------ consultas (internal: prueba de traducción con ToQueryString)

    internal sealed class FilaCabeceraEdicion
    {
        public int Id { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string EstadoCodigo { get; init; } = string.Empty;
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
        public string GrupoCodigo { get; init; } = string.Empty;
        public bool RequiereProyectoErp { get; init; }
        public int CompaniaId { get; init; }
        public string? ProyectoErpId { get; init; }
        public int? HorarioCodigo { get; init; }
        public string? HorarioDescripcion { get; init; }
        public TimeOnly? HoraEntrada { get; init; }
        public TimeOnly? HoraSalida { get; init; }
        public TimeOnly? SalidaAlmuerzo { get; init; }
        public TimeOnly? RegresoAlmuerzo { get; init; }
        public byte[] RowVer { get; init; } = [];
    }

    internal sealed class FilaActividadEdicion
    {
        public int Id { get; init; }
        public int Version { get; init; }
        public string ActividadCodigo { get; init; } = string.Empty;
        public string? ActividadDescripcion { get; init; }
        public string? ActividadTipo { get; init; }
        public string TipoMovimiento { get; init; } = string.Empty;
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
    }

    /// <summary>Cabecera del proyecto no eliminado y visible (R1), con grupo, horario, almuerzo y RowVer.</summary>
    internal static IQueryable<FilaCabeceraEdicion> ConsultaCabeceraEdicion(ProfesiogramaDbContext db, int proyectoId, int? propietarioUsuarioId)
    {
        var proyectos = db.Proyectos.AsNoTracking().Where(p => p.Id == proyectoId && !p.Eliminado);
        if (propietarioUsuarioId is int propietario)
        {
            proyectos = proyectos.Where(p => p.PropietarioUsuarioId == propietario);
        }

        return proyectos.Select(p => new FilaCabeceraEdicion
        {
            Id = p.Id,
            Codigo = p.Codigo,
            EstadoCodigo = p.EstadoProyecto.Codigo,
            FechaInicio = p.FechaInicio,
            FechaFin = p.FechaFin,
            GrupoCodigo = p.GrupoProyecto.Codigo,
            RequiereProyectoErp = p.GrupoProyecto.RequiereProyectoErp,
            CompaniaId = p.CompaniaId,
            ProyectoErpId = p.ProyectoErpId,
            HorarioCodigo = p.HorarioCodigo,
            HorarioDescripcion = p.HorarioDescripcion,
            HoraEntrada = p.HoraEntrada,
            HoraSalida = p.HoraSalida,
            SalidaAlmuerzo = p.SalidaAlmuerzo,
            RegresoAlmuerzo = p.RegresoAlmuerzo,
            RowVer = p.RowVer,
        });
    }

    /// <summary>Todas las actividades del proyecto con descripción, tipo y código del tipo de movimiento.</summary>
    internal static IQueryable<FilaActividadEdicion> ConsultaActividadesEdicion(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoActividades.AsNoTracking()
            .Where(a => a.ProyectoId == proyectoId)
            .OrderBy(a => a.Version)
            .Select(a => new FilaActividadEdicion
            {
                Id = a.Id,
                Version = a.Version,
                ActividadCodigo = a.ActividadCodigo,
                ActividadDescripcion = a.ActividadDescripcion,
                ActividadTipo = a.ActividadTipo,
                TipoMovimiento = a.TipoMovimiento.Codigo,
                FechaInicio = a.FechaInicio,
                FechaFin = a.FechaFin,
            });
}
