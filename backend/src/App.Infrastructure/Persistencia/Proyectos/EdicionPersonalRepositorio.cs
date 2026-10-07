using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Domain.Catalogos;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using App.Infrastructure.Persistencia.Empleados;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Lectura y escritura de "Actualizar personal" (TAREA-17) y "Reactivar" (TAREA-17b). AplicarAsync debe ejecutarse
/// dentro de ITransaccionAsignaciones. Orden (D7, por la FK compuesta y la autorreferenciada, ambas Restrict):
///  1) ExecuteDelete de los días con Fecha ≥ corte;
///  2) SaveChanges 1: vigentes actualizadas (relaciones con principales guardados) + nuevas
///     (reactivación: el principal inicial nuevo y el proyecto ACTIVO con la nueva FechaFin, con su RowVer);
///  3) SaveChanges 2: relaciones con principales nuevos (ya tienen Id), referencias a omitidas en null, omitidas eliminadas;
///  4) SaveChanges 3: días regenerados (clave → ProyectoPersonalId), actividad REACTIVACION (si aplica) y etapa.
/// Personal, proyecto, actividad y etapa con seguimiento: AuditoriaInterceptor llena la auditoría.
/// </summary>
internal sealed class EdicionPersonalRepositorio(ProfesiogramaDbContext db) : IEdicionPersonalRepositorio, IReactivacionRepositorio
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
        var conDescansoPosterior = (await ConsultaBacksConDescansoPosterior(db, proyectoId).ToListAsync(ct)).ToHashSet();
        var dias = await ConsultaDiasBase(db, proyectoId, corte).ToListAsync(ct);
        var actividades = await CambioEstadoRepositorio.ConsultaActividades(db, proyectoId).ToListAsync(ct);

        var personas = personal.ToDictionary(p => p.Id, p => new PersonaProyecto((RolCronograma)p.RolAsignacionId, p.Numero));

        return new DatosEdicion(
            cabecera.Id, cabecera.Codigo, cabecera.EstadoCodigo, cabecera.FechaInicio, cabecera.FechaFin,
            personal.Select(p => new PersonaGuardada(
                p.Id, (RolCronograma)p.RolAsignacionId, p.Numero, p.EmpleadoId, p.CodigoEkon, p.NombreCompleto,
                p.FechaInicio, p.FechaFin, p.JornadaCodigo, p.DiasTrabajo, p.DiasDescanso, p.TipoRegistro,
                p.PrincipalRelacionadoId, p.CargoAsignado, p.Observacion, p.EsPrincipalInicial)
            {
                // H12: back sin días DESCANSO guardados después de su FechaFin (la suspensión los borró).
                SinDescansoPosterior = p.RolAsignacionId == (byte)RolCronograma.Back && !conDescansoPosterior.Contains(p.Id),
            }).ToList(),
            dias.Select(d => new DiaGuardado(d.PersonalId, new DiaAsignado(
                d.EmpleadoId, d.Fecha, (RolCronograma)d.RolAsignacionId,
                d.TipoAsignacion == ProyectoAsignacionDia.TipoAuto ? TipoAsignacionCronograma.Auto : TipoAsignacionCronograma.Manual,
                d.Bloque, personas[d.PersonalId]))).ToList(),
            actividades.Select(a => new ActividadCorte(a.Id, a.Version, a.ActividadCodigo, a.FechaInicio, a.FechaFin)).ToList());
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct) =>
        CambioEstadoRepositorio.ConsultaUltimaVersion(db, proyectoId).FirstOrDefaultAsync(ct);

    public async Task<ActividadParaReactivar?> ObtenerActividadVigenteAsync(int proyectoId, DateOnly fecha, CancellationToken ct)
    {
        var fila = await ConsultaActividadParaReactivar(db, proyectoId, fecha).FirstOrDefaultAsync(ct);
        return fila is null ? null : new ActividadParaReactivar(fila.ActividadCodigo, fila.ActividadDescripcion, fila.ActividadTipo);
    }

    public async Task AplicarAsync(CambioPersonal cambio, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cambio);
        var idProyecto = cambio.ProyectoId;

        // 1) Días desde el corte (también los de las omitidas, que empiezan en el corte o después).
        await ConsultaDiasDesdeCorte(db, idProyecto, cambio.Corte).ExecuteDeleteAsync(ct);

        var tipoMovimientoId = await CambioEstadoRepositorio.ConsultaIdTipoMovimiento(db, cambio.TipoMovimiento).FirstAsync(ct);
        var personal = (await CambioEstadoRepositorio.ConsultaPersonalSeguimiento(db, idProyecto).ToListAsync(ct)).ToDictionary(p => p.Id);

        // TAREA-26d: alta puntual de los empleados de las personas nuevas (fila nueva o refrescada); Id temporal → real.
        var empleados = await AltaPuntualEmpleados.AplicarAsync(db, cambio.AltasEmpleados ?? [], ct);

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
            EmpleadoId = AltaPuntualEmpleados.IdReal(empleados, n.EmpleadoId),
            CargoAsignado = n.Cargo,
            FechaInicio = n.FechaInicio,
            FechaFin = n.FechaFin,
            JornadaId = n.JornadaId,
            DiasTrabajo = n.DiasTrabajo,
            DiasDescanso = n.DiasDescanso,
            // D6 + P3 (TAREA-19y): inicial solo la clave indicada; reactivación: R6 (H4), null si solo hay backs.
            EsPrincipalInicial = n.Clave == (cambio.Reactivacion is { } r ? r.ClavePrincipalInicial : cambio.ClaveInicialNueva),
            TipoRegistro = n.TipoRegistro,
            Observacion = n.Observacion,
            PrincipalRelacionadoId = n.Relacion.Id,
        });
        db.ProyectoPersonal.AddRange(nuevas.Values);

        // Reactivación (R4, H5): proyecto ACTIVO con la nueva fecha fin. Se actualiza con su RowVer (concurrencia).
        if (cambio.Reactivacion is { } reactivacion)
        {
            var proyecto = await CambioEstadoRepositorio.ConsultaProyectoSeguimiento(db, idProyecto).FirstAsync(ct);
            proyecto.EstadoProyectoId = CatalogoIds.EstadoProyecto.Activo;
            proyecto.FechaFin = reactivacion.FechaFinProyecto;
        }

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

        // R8: actividad nueva de la reactivación (misma actividad, de R a la nueva fecha fin). Las existentes no cambian.
        if (cambio.Reactivacion?.Actividad is { } actividad)
        {
            db.ProyectoActividades.Add(new ProyectoActividad
            {
                ProyectoId = idProyecto,
                Version = actividad.Version,
                TipoMovimientoId = tipoMovimientoId,
                FechaInicio = actividad.FechaInicio,
                FechaFin = actividad.FechaFin,
                ActividadCodigo = actividad.Codigo,
                ActividadDescripcion = actividad.Descripcion,
                ActividadTipo = actividad.Tipo,
            });
        }

        db.ProyectoEtapas.Add(new ProyectoEtapa
        {
            ProyectoId = idProyecto,
            Version = cambio.Version,
            TipoMovimientoId = tipoMovimientoId,
            EstadoProyectoId = CatalogoIds.EstadoProyecto.Activo, // edición: el estado no cambia; reactivación: queda ACTIVO
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

    internal sealed class FilaActividadReactivar
    {
        public string ActividadCodigo { get; init; } = string.Empty;
        public string? ActividadDescripcion { get; init; }
        public string? ActividadTipo { get; init; }
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

    /// <summary>
    /// H12: backs con al menos un día DESCANSO guardado después de su FechaFin. Un back recortado por una suspensión
    /// no aparece (sus días posteriores a la fecha de suspensión se borraron).
    /// </summary>
    internal static IQueryable<int> ConsultaBacksConDescansoPosterior(ProfesiogramaDbContext db, int proyectoId) =>
        db.ProyectoAsignacionesDia.AsNoTracking()
            .Where(d => d.ProyectoId == proyectoId
                        && d.RolAsignacionId == (byte)RolCronograma.Descanso
                        && d.ProyectoPersonal.RolAsignacionId == (byte)RolCronograma.Back
                        && d.Fecha > d.ProyectoPersonal.FechaFin)
            .Select(d => d.ProyectoPersonalId)
            .Distinct();

    /// <summary>R8: actividad vigente en la fecha (la de mayor versión si se solapan), con descripción y tipo.</summary>
    internal static IQueryable<FilaActividadReactivar> ConsultaActividadParaReactivar(ProfesiogramaDbContext db, int proyectoId, DateOnly fecha) =>
        db.ProyectoActividades.AsNoTracking()
            .Where(a => a.ProyectoId == proyectoId && a.FechaInicio <= fecha && a.FechaFin >= fecha)
            .OrderByDescending(a => a.Version)
            .Select(a => new FilaActividadReactivar
            {
                ActividadCodigo = a.ActividadCodigo,
                ActividadDescripcion = a.ActividadDescripcion,
                ActividadTipo = a.ActividadTipo,
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
