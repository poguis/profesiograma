using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Domain.Catalogos;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Lectura y escritura de "Actualizar personal" (TAREA-17). AplicarAsync debe ejecutarse dentro de ITransaccionAsignaciones.
/// Orden (D7, por la FK compuesta y la autorreferenciada, ambas Restrict):
///  1) ExecuteDelete de los días con Fecha ≥ corte;
///  2) SaveChanges 1: vigentes actualizadas (relaciones con principales guardados) + nuevas;
///  3) SaveChanges 2: relaciones con principales nuevos (ya tienen Id), referencias a omitidas en null, omitidas eliminadas;
///  4) SaveChanges 3: días regenerados (clave → ProyectoPersonalId) y etapa ACTUALIZACION_PERSONAL.
/// Personal y etapa con seguimiento: AuditoriaInterceptor llena la auditoría.
/// </summary>
internal sealed class EdicionPersonalRepositorio(ProfesiogramaDbContext db) : IEdicionPersonalRepositorio
{
    private static readonly int[] ErroresUnicos = [2601, 2627];

    public async Task<DatosEdicion?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly corte, CancellationToken ct)
    {
        var cabecera = await CambioEstadoRepositorio.ConsultaCabecera(db, proyectoId, propietarioUsuarioId).FirstOrDefaultAsync(ct);
        if (cabecera is null)
        {
            return null;
        }

        var personal = await ConsultaPersonalEdicion(db, proyectoId).ToListAsync(ct);
        var dias = await ConsultaDiasBase(db, proyectoId, corte).ToListAsync(ct);
        var actividades = await CambioEstadoRepositorio.ConsultaActividades(db, proyectoId).ToListAsync(ct);

        var personas = personal.ToDictionary(p => p.Id, p => new PersonaProyecto((RolCronograma)p.RolAsignacionId, p.Numero));

        return new DatosEdicion(
            cabecera.Id, cabecera.Codigo, cabecera.EstadoCodigo, cabecera.FechaInicio, cabecera.FechaFin,
            personal.Select(p => new PersonaGuardada(
                p.Id, (RolCronograma)p.RolAsignacionId, p.Numero, p.EmpleadoId, p.CodigoEkon, p.NombreCompleto,
                p.FechaInicio, p.FechaFin, p.JornadaCodigo, p.DiasTrabajo, p.DiasDescanso, p.TipoRegistro,
                p.PrincipalRelacionadoId, p.CargoAsignado, p.Observacion, p.EsPrincipalInicial)).ToList(),
            dias.Select(d => new DiaGuardado(d.PersonalId, new DiaAsignado(
                d.EmpleadoId, d.Fecha, (RolCronograma)d.RolAsignacionId,
                d.TipoAsignacion == ProyectoAsignacionDia.TipoAuto ? TipoAsignacionCronograma.Auto : TipoAsignacionCronograma.Manual,
                d.Bloque, personas[d.PersonalId]))).ToList(),
            actividades.Select(a => new ActividadCorte(a.Id, a.Version, a.ActividadCodigo, a.FechaInicio, a.FechaFin)).ToList());
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct) =>
        CambioEstadoRepositorio.ConsultaUltimaVersion(db, proyectoId).FirstOrDefaultAsync(ct);

    public async Task AplicarAsync(CambioPersonal cambio, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cambio);
        var idProyecto = cambio.ProyectoId;

        // 1) Días desde el corte (también los de las omitidas, que empiezan en el corte o después).
        await ConsultaDiasDesdeCorte(db, idProyecto, cambio.Corte).ExecuteDeleteAsync(ct);

        var tipoMovimientoId = await CambioEstadoRepositorio.ConsultaIdTipoMovimiento(db, cambio.TipoMovimiento).FirstAsync(ct);
        var personal = (await CambioEstadoRepositorio.ConsultaPersonalSeguimiento(db, idProyecto).ToListAsync(ct)).ToDictionary(p => p.Id);

        // 2) Vigentes (el empleado no cambia) y nuevas. Las relaciones con principales nuevos se asignan en el paso 3.
        foreach (var v in cambio.Vigentes)
        {
            var p = personal[v.Id];
            p.JornadaId = v.JornadaId;
            p.DiasTrabajo = v.DiasTrabajo;
            p.DiasDescanso = v.DiasDescanso;
            p.FechaInicio = v.FechaInicio;
            p.FechaFin = v.FechaFin;
            p.TipoRegistro = v.TipoRegistro;
            p.CargoAsignado = v.Cargo;
            p.Observacion = v.Observacion;
            p.PrincipalRelacionadoId = v.Relacion.Id;
        }

        var nuevas = cambio.Nuevas.ToDictionary(n => n.Clave, n => new ProyectoPersonal
        {
            ProyectoId = idProyecto,
            RolAsignacionId = (byte)n.Rol,
            Numero = n.Numero,
            EmpleadoId = n.EmpleadoId,
            CargoAsignado = n.Cargo,
            FechaInicio = n.FechaInicio,
            FechaFin = n.FechaFin,
            JornadaId = n.JornadaId,
            DiasTrabajo = n.DiasTrabajo,
            DiasDescanso = n.DiasDescanso,
            EsPrincipalInicial = false, // D6 (H4 en la TAREA-17b)
            TipoRegistro = n.TipoRegistro,
            Observacion = n.Observacion,
            PrincipalRelacionadoId = n.Relacion.Id,
        });
        db.ProyectoPersonal.AddRange(nuevas.Values);
        await GuardarAsync(ct);

        // 3) Relaciones con principales nuevos; referencias a omitidas en null (por defensa); eliminar omitidas.
        foreach (var v in cambio.Vigentes.Where(v => v.Relacion.ClaveNueva is not null))
        {
            personal[v.Id].PrincipalRelacionadoId = nuevas[v.Relacion.ClaveNueva!].Id;
        }

        foreach (var n in cambio.Nuevas.Where(n => n.Relacion.ClaveNueva is not null))
        {
            nuevas[n.Clave].PrincipalRelacionadoId = nuevas[n.Relacion.ClaveNueva!].Id;
        }

        var eliminadas = cambio.Eliminadas.ToHashSet();
        foreach (var p in personal.Values.Where(p => p.PrincipalRelacionadoId is int r && eliminadas.Contains(r)))
        {
            p.PrincipalRelacionadoId = null;
        }

        db.ProyectoPersonal.RemoveRange(personal.Values.Where(p => eliminadas.Contains(p.Id)));
        await GuardarAsync(ct);

        // 4) Días regenerados y etapa (versión = máx + 1, calculada por el servicio dentro del applock).
        db.ProyectoAsignacionesDia.AddRange(cambio.Dias.Select(d =>
        {
            var persona = d.PersonalId is int id ? personal[id] : nuevas[d.ClaveNueva!];
            return new ProyectoAsignacionDia
            {
                ProyectoId = idProyecto,
                ProyectoPersonalId = persona.Id,
                EmpleadoId = persona.EmpleadoId,
                Fecha = d.Dia.Fecha,
                RolAsignacionId = (byte)d.Dia.Rol,
                TipoAsignacion = d.Dia.Tipo == TipoAsignacionCronograma.Auto ? ProyectoAsignacionDia.TipoAuto : ProyectoAsignacionDia.TipoManual,
                Bloque = d.Dia.Bloque,
            };
        }));

        db.ProyectoEtapas.Add(new ProyectoEtapa
        {
            ProyectoId = idProyecto,
            Version = cambio.Version,
            TipoMovimientoId = tipoMovimientoId,
            EstadoProyectoId = CatalogoIds.EstadoProyecto.Activo, // el estado no cambia (solo proyectos ACTIVO)
            FechaInicio = cambio.FechaInicioProyecto,
            FechaFin = cambio.FechaFinProyecto,
            FechaCorte = cambio.Corte,
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
            throw new ConflictoConcurrenciaException("El personal fue modificado por otro proceso.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && ErroresUnicos.Contains(sql.Number))
        {
            throw new ConflictoConcurrenciaException("Otro registro del proyecto se guardó al mismo tiempo.", ex);
        }
    }

    // ------------------------------------------------------------------ consultas (internal: prueba de traducción con ToQueryString)

    internal sealed class FilaPersonalEdicion
    {
        public int Id { get; init; }
        public byte RolAsignacionId { get; init; }
        public short Numero { get; init; }
        public int EmpleadoId { get; init; }
        public string CodigoEkon { get; init; } = string.Empty;
        public string NombreCompleto { get; init; } = string.Empty;
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
        public string? JornadaCodigo { get; init; }
        public byte? DiasTrabajo { get; init; }
        public byte DiasDescanso { get; init; }
        public string TipoRegistro { get; init; } = string.Empty;
        public int? PrincipalRelacionadoId { get; init; }
        public string? CargoAsignado { get; init; }
        public string? Observacion { get; init; }
        public bool EsPrincipalInicial { get; init; }
    }

    internal sealed class FilaDiaBase
    {
        public int PersonalId { get; init; }
        public int EmpleadoId { get; init; }
        public DateOnly Fecha { get; init; }
        public byte RolAsignacionId { get; init; }
        public string TipoAsignacion { get; init; } = string.Empty;
        public short Bloque { get; init; }
    }

    /// <summary>Todo el personal del proyecto (sin cédula ni correo), con el código de jornada.</summary>
    internal static IQueryable<FilaPersonalEdicion> ConsultaPersonalEdicion(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoPersonal.AsNoTracking()
            .Where(pp => pp.ProyectoId == proyectoId)
            .OrderBy(pp => pp.RolAsignacionId).ThenBy(pp => pp.Numero)
            .Select(pp => new FilaPersonalEdicion
            {
                Id = pp.Id,
                RolAsignacionId = pp.RolAsignacionId,
                Numero = pp.Numero,
                EmpleadoId = pp.EmpleadoId,
                CodigoEkon = pp.Empleado.CodigoEkon,
                NombreCompleto = pp.Empleado.NombreCompleto,
                FechaInicio = pp.FechaInicio,
                FechaFin = pp.FechaFin,
                JornadaCodigo = pp.Jornada != null ? pp.Jornada.Codigo : null,
                DiasTrabajo = pp.DiasTrabajo,
                DiasDescanso = pp.DiasDescanso,
                TipoRegistro = pp.TipoRegistro,
                PrincipalRelacionadoId = pp.PrincipalRelacionadoId,
                CargoAsignado = pp.CargoAsignado,
                Observacion = pp.Observacion,
                EsPrincipalInicial = pp.EsPrincipalInicial,
            });

    /// <summary>Base del motor: días con Fecha &lt; corte.</summary>
    internal static IQueryable<FilaDiaBase> ConsultaDiasBase(ProfesiogramaDbContext db, int proyectoId, DateOnly corte) =>
        db.ProyectoAsignacionesDia.AsNoTracking()
            .Where(d => d.ProyectoId == proyectoId && d.Fecha < corte)
            .Select(d => new FilaDiaBase
            {
                PersonalId = d.ProyectoPersonalId,
                EmpleadoId = d.EmpleadoId,
                Fecha = d.Fecha,
                RolAsignacionId = d.RolAsignacionId,
                TipoAsignacion = d.TipoAsignacion,
                Bloque = d.Bloque,
            });

    /// <summary>Filtro del DELETE masivo: días del proyecto con Fecha ≥ corte (se regeneran).</summary>
    internal static IQueryable<ProyectoAsignacionDia> ConsultaDiasDesdeCorte(ProfesiogramaDbContext db, int proyectoId, DateOnly corte) =>
        db.ProyectoAsignacionesDia.Where(d => d.ProyectoId == proyectoId && d.Fecha >= corte);
}
