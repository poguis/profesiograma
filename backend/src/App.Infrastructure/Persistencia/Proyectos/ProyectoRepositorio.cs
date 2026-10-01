using App.Application.Proyectos.Crear;
using App.Domain.Catalogos;
using App.Domain.Maestros;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Escritura de "Crear proyecto". Debe ejecutarse dentro de ITransaccionAsignaciones (una transacción con applock).
/// Dos SaveChanges en la misma transacción:
///  1) Compania (upsert), Proyecto, ProyectoPersonal, ProyectoEtapa v1, ProyectoActividad v1;
///  2) ProyectoAsignacionDia (FK compuesta con los Id ya generados) y PrincipalRelacionadoId (FK sin navegación).
/// La auditoría (CreadoPorId/FechaCreacion) la llena AuditoriaInterceptor.
/// </summary>
internal sealed class ProyectoRepositorio(ProfesiogramaDbContext db) : IProyectoRepositorio
{
    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct) =>
        ConsultaCodigo(db, codigo).AnyAsync(ct);

    /// <summary>Proyectos con ese código (internal: prueba de traducción con ToQueryString).</summary>
    internal static IQueryable<Proyecto> ConsultaCodigo(ProfesiogramaDbContext db, string codigo) =>
        db.Proyectos.Where(p => p.Codigo == codigo);

    /// <summary>Compañía en caché por Id, con seguimiento para el upsert (internal: prueba de traducción).</summary>
    internal static IQueryable<Compania> ConsultaCompania(ProfesiogramaDbContext db, int companiaId) =>
        db.Companias.Where(c => c.Id == companiaId);

    public async Task<int> AgregarAsync(NuevoProyecto nuevo, string codigo, Guid uid, CancellationToken ct)
    {
        var d = nuevo.Datos;

        await UpsertCompaniaAsync(d, ct);

        var proyecto = new Proyecto
        {
            Uid = uid,
            Codigo = codigo,
            NombreVisual = Truncar(nuevo.NombreVisual, 300)!,
            GrupoProyectoId = d.Grupo.Id,
            CompaniaId = d.Compania.Id,
            ProyectoErpId = d.ProyectoErp?.Id,
            ProyectoErpNombre = Truncar(d.ProyectoErp?.Nombre, 300),
            ProyectoErpEstado = Truncar(d.ProyectoErp?.Estado, 30),
            DimensionUegpId = d.Dimension?.UegpId,
            DimensionDescripcion = Truncar(d.Dimension?.Descripcion, 300),
            FechaInicio = d.FechaInicio,
            FechaFin = d.FechaFin,
            EstadoProyectoId = CatalogoIds.EstadoProyecto.Activo,
            HorarioCodigo = d.Horario.Codigo,
            HorarioDescripcion = Truncar(d.Horario.Descripcion, 200),
            HoraEntrada = d.Horario.HoraEntrada,
            HoraSalida = d.Horario.HoraSalida,
            HorasJornadaMin = d.Horario.MinutosJornada,
            HorasTrabajadasMin = d.Horario.MinutosTrabajados,
            TipoHorario = Truncar(d.Horario.Tipo, 5),
            SalidaAlmuerzo = d.SalidaAlmuerzo,
            RegresoAlmuerzo = d.RegresoAlmuerzo,
            DepartamentoId = d.DepartamentoId,
            PropietarioUsuarioId = d.PropietarioUsuarioId,
        };

        var personal = new Dictionary<PersonaProyecto, ProyectoPersonal>();
        foreach (var p in d.Principales)
        {
            personal[p.Persona] = new ProyectoPersonal
            {
                RolAsignacionId = CatalogoIds.RolAsignacion.Principal,
                Numero = p.Persona.Numero,
                EmpleadoId = p.Empleado.Id,
                CargoAsignado = Truncar(p.Cargo, 200),
                FechaInicio = p.Inicio,
                FechaFin = p.Fin,
                JornadaId = p.Jornada!.Id,
                DiasTrabajo = p.Jornada.DiasTrabajo,
                DiasDescanso = p.Jornada.DiasDescanso,
                EsPrincipalInicial = p.Persona.Numero == 1, // como la app original: el primer principal agregado
                TipoRegistro = ProyectoPersonal.TipoRegistroJornada,
            };
        }

        foreach (var b in d.Backs)
        {
            personal[b.Persona] = new ProyectoPersonal
            {
                RolAsignacionId = CatalogoIds.RolAsignacion.Back,
                Numero = b.Persona.Numero,
                EmpleadoId = b.Empleado.Id,
                CargoAsignado = Truncar(b.Cargo, 200),
                FechaInicio = b.Inicio,
                FechaFin = b.Fin,
                DiasDescanso = b.DiasDescanso,
                TipoRegistro = b.TipoRegistro == TipoRegistroBack.Descanso
                    ? ProyectoPersonal.TipoRegistroDescanso
                    : ProyectoPersonal.TipoRegistroJornada,
                Observacion = Truncar(b.Observacion, 500),
            };
        }

        foreach (var pp in personal.Values)
        {
            proyecto.Personal.Add(pp);
        }

        // RN12: etapa v1 CREACION / ACTIVO, FechaCorte = inicio, snapshot sin cédula ni correo.
        proyecto.Etapas.Add(new ProyectoEtapa
        {
            Version = 1,
            TipoMovimientoId = CatalogoIds.TipoMovimiento.Creacion,
            EstadoProyectoId = CatalogoIds.EstadoProyecto.Activo,
            FechaInicio = d.FechaInicio,
            FechaFin = d.FechaFin,
            FechaCorte = d.FechaInicio,
            ActividadCodigo = d.Actividad?.Id,
            SnapshotPersonal = nuevo.SnapshotPersonal,
        });

        // RN13: actividad v1 CREACION solo si hay actividad; vigencia = rango del proyecto.
        if (d.Actividad is { } actividad)
        {
            proyecto.Actividades.Add(new ProyectoActividad
            {
                Version = 1,
                TipoMovimientoId = CatalogoIds.TipoMovimiento.Creacion,
                FechaInicio = d.FechaInicio,
                FechaFin = d.FechaFin,
                ActividadCodigo = actividad.Id,
                ActividadDescripcion = Truncar(actividad.Descripcion, 300),
                ActividadTipo = Truncar(actividad.Tipo, 100),
            });
        }

        db.Proyectos.Add(proyecto);
        await db.SaveChangesAsync(ct);

        // 2) Relación back → principal (FK auto-referenciada sin navegación) y días del cronograma.
        foreach (var b in d.Backs.Where(b => b.PrincipalRelacionado is not null))
        {
            personal[b.Persona].PrincipalRelacionadoId =
                personal[new PersonaProyecto(RolCronograma.Principal, b.PrincipalRelacionado!.Value)].Id;
        }

        db.ProyectoAsignacionesDia.AddRange(nuevo.Dias.Select(dia =>
        {
            var pp = personal[dia.Persona];
            return new ProyectoAsignacionDia
            {
                ProyectoId = proyecto.Id,
                ProyectoPersonalId = pp.Id,
                EmpleadoId = pp.EmpleadoId,
                Fecha = dia.Fecha,
                RolAsignacionId = (byte)dia.Rol,
                TipoAsignacion = dia.Tipo == TipoAsignacionCronograma.Auto ? ProyectoAsignacionDia.TipoAuto : ProyectoAsignacionDia.TipoManual,
                Bloque = dia.Bloque,
            };
        }));

        await db.SaveChangesAsync(ct);
        return proyecto.Id;
    }

    /// <summary>Caché de la compañía del ERP: Nombre = tradeName ?? name, NombreComercial = name, Ruc.</summary>
    private async Task UpsertCompaniaAsync(ProyectoValidado d, CancellationToken ct)
    {
        var compania = await ConsultaCompania(db, d.Compania.Id).FirstOrDefaultAsync(ct);
        if (compania is null)
        {
            db.Companias.Add(new Compania
            {
                Id = d.Compania.Id,
                Nombre = Truncar(d.Compania.Nombre, 300)!,
                NombreComercial = Truncar(d.Compania.NombreCorto, 300),
                Ruc = Truncar(d.Compania.Ruc, 13),
            });
            return;
        }

        // EF solo marca como modificadas las propiedades que cambian.
        compania.Nombre = Truncar(d.Compania.Nombre, 300)!;
        compania.NombreComercial = Truncar(d.Compania.NombreCorto, 300);
        compania.Ruc = Truncar(d.Compania.Ruc, 13);
    }

    private static string? Truncar(string? valor, int maximo) =>
        valor is null ? null : valor.Length <= maximo ? valor : valor[..maximo];
}
