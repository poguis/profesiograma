using App.Application.Proyectos.Estados;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Lectura y escritura del cambio de estado (SUSPENSION / CIERRE). AplicarAsync debe ejecutarse dentro de
/// ITransaccionAsignaciones. Orden (FK compuesta y autorreferenciada con Restrict):
///  1) ExecuteDelete de los días (Fecha &gt; F, o de personal que se elimina);
///  2) SaveChanges 1: backs sin principal (referencia a null) y recorte de FechaFin del personal;
///  3) SaveChanges 2: eliminar personal y actividades, recortar actividades, actualizar el proyecto, insertar la etapa.
/// Personal, actividades y proyecto se actualizan con seguimiento: AuditoriaInterceptor llena ModificadoPorId /
/// FechaModificacion (y CreadoPorId en la etapa). El proyecto se actualiza con su RowVer (concurrencia optimista).
/// </summary>
internal sealed class CambioEstadoRepositorio(ProfesiogramaDbContext db) : ICambioEstadoRepositorio
{
    // SQL Server: 2601 = índice único duplicado, 2627 = restricción única (p. ej. UQ_ProyectoEtapa_Version).
    private static readonly int[] ErroresUnicos = [2601, 2627];

    public async Task<DatosCambioEstado?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly fecha, CancellationToken ct)
    {
        var cabecera = await ConsultaCabecera(db, proyectoId, propietarioUsuarioId).FirstOrDefaultAsync(ct);
        if (cabecera is null)
        {
            return null;
        }

        var personal = await ConsultaPersonal(db, proyectoId).ToListAsync(ct);
        var dias = await ConsultaDiasPosteriores(db, proyectoId, fecha).ToListAsync(ct);
        var actividades = await ConsultaActividades(db, proyectoId).ToListAsync(ct);

        return new DatosCambioEstado(
            cabecera.Id,
            cabecera.Codigo,
            cabecera.EstadoCodigo,
            cabecera.FechaInicio,
            cabecera.FechaFin,
            personal.Select(p => new PersonalCambio(
                new PersonaCorte(p.Id, (RolCronograma)p.RolAsignacionId, p.Numero, p.EmpleadoId, p.FechaInicio, p.FechaFin, p.PrincipalRelacionadoId),
                p.CodigoEkon, p.NombreCompleto, p.JornadaCodigo, p.DiasTrabajo, p.DiasDescanso, p.TipoRegistro)).ToList(),
            dias.Select(d => new DiaCorte(d.PersonalId, d.Fecha, (RolCronograma)d.RolAsignacionId)).ToList(),
            actividades.Select(a => new ActividadCorte(a.Id, a.Version, a.ActividadCodigo, a.FechaInicio, a.FechaFin)).ToList());
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct) =>
        ConsultaUltimaVersion(db, proyectoId).FirstOrDefaultAsync(ct);

    public async Task AplicarAsync(CambioEstadoAplicar cambio, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cambio);
        var plan = cambio.Plan;
        var idProyecto = cambio.ProyectoId;

        // 1) y 2) Días, referencias a principales eliminados y recorte de fechas (EscrituraRecorte, común con la TAREA-18).
        var personal = await EscrituraRecorte.AplicarAntesDeGuardarAsync(db, idProyecto, plan, ct);

        var estadoId = await ConsultaIdEstado(db, cambio.EstadoDestino).FirstAsync(ct);
        var tipoMovimientoId = await ConsultaIdTipoMovimiento(db, cambio.TipoMovimiento).FirstAsync(ct);
        var proyecto = await ConsultaProyectoSeguimiento(db, idProyecto).FirstAsync(ct);
        var actividades = await ConsultaActividadesSeguimiento(db, idProyecto).ToListAsync(ct);

        await GuardarAsync(ct);

        // 3) Personal y actividades eliminadas, actividades recortadas (H3), proyecto y etapa nueva (E4).
        EscrituraRecorte.AplicarPersonalYActividades(db, plan, personal, actividades);

        proyecto.FechaFin = plan.Fecha;
        proyecto.EstadoProyectoId = estadoId;

        // Versión = máx + 1 (la calcula el servicio dentro del applock; UQ_ProyectoEtapa_Version es la red de seguridad).
        db.ProyectoEtapas.Add(new ProyectoEtapa
        {
            ProyectoId = idProyecto,
            Version = cambio.Version,
            TipoMovimientoId = tipoMovimientoId,
            EstadoProyectoId = estadoId,
            FechaInicio = cambio.FechaInicioProyecto,
            FechaFin = plan.Fecha,
            FechaCorte = plan.Fecha,
            ActividadCodigo = plan.ActividadVigenteEnF,
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
            throw new ConflictoConcurrenciaException("Otra etapa del proyecto se registró al mismo tiempo.", ex);
        }
    }

    // ------------------------------------------------------------------ consultas (internal: prueba de traducción con ToQueryString)

    internal sealed class FilaCabecera
    {
        public int Id { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string EstadoCodigo { get; init; } = string.Empty;
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
    }

    internal sealed class FilaPersonal
    {
        public int Id { get; init; }
        public byte RolAsignacionId { get; init; }
        public short Numero { get; init; }
        public int EmpleadoId { get; init; }
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
        public int? PrincipalRelacionadoId { get; init; }
        public string CodigoEkon { get; init; } = string.Empty;
        public string NombreCompleto { get; init; } = string.Empty;
        public string? JornadaCodigo { get; init; }
        public byte? DiasTrabajo { get; init; }
        public byte DiasDescanso { get; init; }
        public string TipoRegistro { get; init; } = string.Empty;
    }

    internal sealed class FilaDia
    {
        public int PersonalId { get; init; }
        public DateOnly Fecha { get; init; }
        public byte RolAsignacionId { get; init; }
    }

    internal sealed class FilaActividad
    {
        public int Id { get; init; }
        public int Version { get; init; }
        public string ActividadCodigo { get; init; } = string.Empty;
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
    }

    /// <summary>Proyecto no eliminado y visible (R1). Estado por código del catálogo.</summary>
    internal static IQueryable<FilaCabecera> ConsultaCabecera(ProfesiogramaDbContext db, int proyectoId, int? propietarioUsuarioId)
    {
        var proyectos = db.Proyectos.AsNoTracking().Where(p => p.Id == proyectoId && !p.Eliminado);
        if (propietarioUsuarioId is int propietario)
        {
            proyectos = proyectos.Where(p => p.PropietarioUsuarioId == propietario);
        }

        return proyectos.Select(p => new FilaCabecera
        {
            Id = p.Id,
            Codigo = p.Codigo,
            EstadoCodigo = p.EstadoProyecto.Codigo,
            FechaInicio = p.FechaInicio,
            FechaFin = p.FechaFin,
        });
    }

    /// <summary>Personal con código EKON y nombre (sin cédula ni correo) y código de jornada.</summary>
    internal static IQueryable<FilaPersonal> ConsultaPersonal(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoPersonal.AsNoTracking()
            .Where(pp => pp.ProyectoId == proyectoId)
            .OrderBy(pp => pp.RolAsignacionId).ThenBy(pp => pp.Numero)
            .Select(pp => new FilaPersonal
            {
                Id = pp.Id,
                RolAsignacionId = pp.RolAsignacionId,
                Numero = pp.Numero,
                EmpleadoId = pp.EmpleadoId,
                FechaInicio = pp.FechaInicio,
                FechaFin = pp.FechaFin,
                PrincipalRelacionadoId = pp.PrincipalRelacionadoId,
                CodigoEkon = pp.Empleado.CodigoEkon,
                NombreCompleto = pp.Empleado.NombreCompleto,
                JornadaCodigo = pp.Jornada != null ? pp.Jornada.Codigo : null,
                DiasTrabajo = pp.DiasTrabajo,
                DiasDescanso = pp.DiasDescanso,
                TipoRegistro = pp.TipoRegistro,
            });

    /// <summary>Días con Fecha &gt; F (los únicos que el recorte elimina).</summary>
    internal static IQueryable<FilaDia> ConsultaDiasPosteriores(ProfesiogramaDbContext db, int proyectoId, DateOnly fecha) =>
        db.ProyectoAsignacionesDia.AsNoTracking()
            .Where(d => d.ProyectoId == proyectoId && d.Fecha > fecha)
            .Select(d => new FilaDia { PersonalId = d.ProyectoPersonalId, Fecha = d.Fecha, RolAsignacionId = d.RolAsignacionId });

    internal static IQueryable<FilaActividad> ConsultaActividades(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoActividades.AsNoTracking()
            .Where(a => a.ProyectoId == proyectoId)
            .Select(a => new FilaActividad
            {
                Id = a.Id,
                Version = a.Version,
                ActividadCodigo = a.ActividadCodigo,
                FechaInicio = a.FechaInicio,
                FechaFin = a.FechaFin,
            });

    /// <summary>
    /// Filtro del DELETE masivo: días posteriores a F y, por defensa, todos los del personal que se elimina
    /// (sus días ya son posteriores a F si los datos son coherentes).
    /// </summary>
    internal static IQueryable<ProyectoAsignacionDia> ConsultaDiasAEliminar(
        ProfesiogramaDbContext db, int proyectoId, DateOnly fecha, IReadOnlyCollection<int> personalEliminado) =>
        db.ProyectoAsignacionesDia
            .Where(d => d.ProyectoId == proyectoId && (d.Fecha > fecha || personalEliminado.Contains(d.ProyectoPersonalId)));

    internal static IQueryable<byte> ConsultaIdEstado(ProfesiogramaDbContext db, string codigo) =>
        db.EstadosProyecto.AsNoTracking().Where(e => e.Codigo == codigo).Select(e => e.Id);

    internal static IQueryable<byte> ConsultaIdTipoMovimiento(ProfesiogramaDbContext db, string codigo) =>
        db.TiposMovimiento.AsNoTracking().Where(t => t.Codigo == codigo).Select(t => t.Id);

    internal static IQueryable<Proyecto> ConsultaProyectoSeguimiento(ProfesiogramaDbContext db, int proyectoId) =>
        db.Proyectos.Where(p => p.Id == proyectoId);

    internal static IQueryable<ProyectoPersonal> ConsultaPersonalSeguimiento(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoPersonal.Where(pp => pp.ProyectoId == proyectoId);

    internal static IQueryable<ProyectoActividad> ConsultaActividadesSeguimiento(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoActividades.Where(a => a.ProyectoId == proyectoId);

    /// <summary>Última versión de etapa del proyecto (0 si no hay).</summary>
    internal static IQueryable<int> ConsultaUltimaVersion(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoEtapas.AsNoTracking()
            .Where(e => e.ProyectoId == proyectoId)
            .OrderByDescending(e => e.Version)
            .Select(e => e.Version);
}
