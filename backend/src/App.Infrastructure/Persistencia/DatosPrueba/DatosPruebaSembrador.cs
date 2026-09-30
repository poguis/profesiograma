using App.Domain.Catalogos;
using App.Domain.Maestros;
using App.Domain.Novedades;
using App.Domain.Proyectos;
using App.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistencia.DatosPrueba;

/// <summary>Usuario simulado de DevAuth que se registra en dbo.Usuario.</summary>
public sealed record UsuarioPrueba(string Clave, Guid ObjectId, string Email, string Nombre);

/// <summary>
/// Datos de prueba SOLO para Development. Idempotente: si ya existen proyectos PRY-DEV-*, no hace nada.
/// Todos los datos son ficticios. No reemplaza la carga masiva de la Fase 3.
/// </summary>
public sealed class DatosPruebaSembrador(
    ProfesiogramaDbContext db,
    TimeProvider reloj,
    ILogger<DatosPruebaSembrador> logger)
{
    private const string PrefijoProyecto = "PRY-DEV-";
    private const int IdCompaniaPrueba = 9001;
    private const string DepartamentoPrueba = "UNIDAD SISTEMA INTEGRADO DE GESTION";
    private const string ZonaEcuador = "SA Pacific Standard Time"; // igual a Parametro ZONA_HORARIA

    public async Task SembrarAsync(IReadOnlyCollection<UsuarioPrueba> usuariosPrueba, CancellationToken ct = default)
    {
        if (!db.Database.GetMigrations().Any())
        {
            logger.LogWarning("Datos de prueba omitidos: no hay migraciones en el ensamblado. Ejecute la TAREA-03.");
            return;
        }

        var pendientes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pendientes.Count > 0)
        {
            logger.LogWarning("Datos de prueba omitidos: hay {Cantidad} migraciones pendientes ({Lista}). Ejecute 'dotnet ef database update'.",
                pendientes.Count, string.Join(", ", pendientes));
            return;
        }

        if (await db.Proyectos.AnyAsync(p => p.Codigo.StartsWith(PrefijoProyecto), ct))
        {
            logger.LogInformation("Datos de prueba ya existentes (proyectos {Prefijo}*). No se vuelven a crear.", PrefijoProyecto);
            return;
        }

        var hoy = HoyEcuador();
        var jornadas = await db.Jornadas.AsNoTracking().ToDictionaryAsync(j => j.Id, ct);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        // 1. Usuarios simulados (mismo oid/correo que DevAuth)
        var usuarios = await AsegurarUsuariosAsync(usuariosPrueba, ct);
        var adminId = usuarios.TryGetValue("admin", out var admin) ? admin.Id : Usuario.IdSistema;
        var gestorId = usuarios.TryGetValue("gestor", out var gestor) ? gestor.Id : Usuario.IdSistema;

        // 2. Compañía y empleados ficticios
        var compania = await db.Companias.FirstOrDefaultAsync(c => c.Id == IdCompaniaPrueba, ct);
        if (compania is null)
        {
            compania = new Compania { Id = IdCompaniaPrueba, Nombre = "COMPAÑÍA DE PRUEBA S.A.", NombreComercial = "PRUEBA", Ruc = "0999999999001" };
            db.Companias.Add(compania);
        }

        var empleados = await AsegurarEmpleadosAsync(ct);

        // 3. Permisos por departamento
        var departamento = await db.Departamentos.FirstAsync(d => d.Nombre == DepartamentoPrueba, ct);
        foreach (var usuario in usuarios.Values)
        {
            var existe = await db.UsuarioDepartamentos.AnyAsync(x => x.UsuarioId == usuario.Id && x.DepartamentoId == departamento.Id, ct);
            if (!existe)
            {
                db.UsuarioDepartamentos.Add(new UsuarioDepartamento { UsuarioId = usuario.Id, DepartamentoId = departamento.Id, Notificado = true });
            }
        }

        await db.SaveChangesAsync(ct);

        // 4. Proyectos
        var p1 = NuevoProyecto("PRY-DEV-0001", "PROYECTO PRUEBA CAMPO", CatalogoIds.GrupoProyecto.Campo,
            CatalogoIds.EstadoProyecto.Activo, hoy.AddDays(-10), hoy.AddDays(50), gestorId, departamento.Id, compania.Id);
        p1.ProyectoErpId = "DEV-ERP-001";
        p1.ProyectoErpNombre = "PROYECTO ERP DE PRUEBA";
        p1.ProyectoErpEstado = "ABIERTO";

        var p2 = NuevoProyecto("PRY-DEV-0002", "PROYECTO PRUEBA PLANTA", CatalogoIds.GrupoProyecto.Planta,
            CatalogoIds.EstadoProyecto.Suspendido, hoy.AddDays(-30), hoy.AddDays(30), gestorId, departamento.Id, compania.Id);
        p2.DimensionUegpId = "DEV-DIM-01";
        p2.DimensionDescripcion = "PLANTA DE PRUEBA";

        var p3 = NuevoProyecto("PRY-DEV-0003", "PROYECTO PRUEBA OFICINAS", CatalogoIds.GrupoProyecto.OficinasAdministrativas,
            CatalogoIds.EstadoProyecto.Terminado, hoy.AddDays(-60), hoy.AddDays(-5), adminId, departamento.Id, compania.Id);
        p3.DimensionUegpId = "DEV-DIM-02";
        p3.DimensionDescripcion = "OFICINAS DE PRUEBA";

        // Personal
        p1.Personal.Add(Principal(p1, 1, empleados[0], jornadas[CatalogoIds.Jornada.Tipo2], inicial: true));
        p1.Personal.Add(Principal(p1, 2, empleados[1], jornadas[CatalogoIds.Jornada.Tipo3], inicial: false));
        p1.Personal.Add(Back(p1, 1, empleados[2], hoy.AddDays(1), hoy.AddDays(5)));
        p2.Personal.Add(Principal(p2, 1, empleados[3], jornadas[CatalogoIds.Jornada.Tipo1], inicial: true));
        p3.Personal.Add(Principal(p3, 1, empleados[4], jornadas[CatalogoIds.Jornada.Tipo3], inicial: true));

        // Historial de etapas
        p1.Etapas.Add(Etapa(p1, 1, CatalogoIds.TipoMovimiento.Creacion, CatalogoIds.EstadoProyecto.Activo));
        p2.Etapas.Add(Etapa(p2, 1, CatalogoIds.TipoMovimiento.Creacion, CatalogoIds.EstadoProyecto.Activo));
        p2.Etapas.Add(Etapa(p2, 2, CatalogoIds.TipoMovimiento.Suspension, CatalogoIds.EstadoProyecto.Suspendido));
        p3.Etapas.Add(Etapa(p3, 1, CatalogoIds.TipoMovimiento.Creacion, CatalogoIds.EstadoProyecto.Activo));
        p3.Etapas.Add(Etapa(p3, 2, CatalogoIds.TipoMovimiento.Cierre, CatalogoIds.EstadoProyecto.Terminado));

        // Actividad vigente
        foreach (var p in new[] { p1, p2, p3 })
        {
            p.Actividades.Add(new ProyectoActividad
            {
                Version = 1,
                TipoMovimientoId = CatalogoIds.TipoMovimiento.Creacion,
                FechaInicio = p.FechaInicio,
                FechaFin = p.FechaFin,
                ActividadCodigo = "DEV.01",
                ActividadDescripcion = "ACTIVIDAD DE PRUEBA",
                ActividadTipo = "PRUEBA",
                CreadoPorId = p.CreadoPorId
            });
        }

        db.Proyectos.AddRange(p1, p2, p3);
        await db.SaveChangesAsync(ct);

        // 5. Asignaciones diarias (ciclo trabajo/descanso simplificado; la regla real va en Fase 5)
        foreach (var pp in p1.Personal.Concat(p2.Personal).Concat(p3.Personal))
        {
            var dias = pp.RolAsignacionId == CatalogoIds.RolAsignacion.Principal
                ? GenerarCicloPrincipal(pp)
                : GenerarBack(pp);
            db.ProyectoAsignacionesDia.AddRange(dias);
        }

        // 6. Novedades
        var n1 = NuevaNovedad("NOV-DEV-0001", CatalogoIds.TipoAplicacionNovedad.Persona, CatalogoIds.TipoNovedad.Vacaciones,
            CatalogoIds.OrigenNovedad.Profesiograma, hoy.AddDays(3), hoy.AddDays(7), gestorId);
        n1.EmpleadoId = empleados[0].Id;
        n1.Observacion = "Vacaciones de prueba";

        var n2 = NuevaNovedad("NOV-DEV-0002", CatalogoIds.TipoAplicacionNovedad.Proyecto, CatalogoIds.TipoNovedad.Feriado,
            CatalogoIds.OrigenNovedad.Profesiograma, hoy.AddDays(14), hoy.AddDays(14), gestorId);
        n2.ProyectoId = p1.Id;

        var n3 = NuevaNovedad("NOV-DEV-0003", CatalogoIds.TipoAplicacionNovedad.General, CatalogoIds.TipoNovedad.Feriado,
            CatalogoIds.OrigenNovedad.Profesiograma, hoy.AddDays(20), hoy.AddDays(20), adminId);
        n3.Observacion = "Feriado general de prueba";

        var n4 = NuevaNovedad("NOV-DEV-0004", CatalogoIds.TipoAplicacionNovedad.Persona, CatalogoIds.TipoNovedad.PermisoMedico,
            CatalogoIds.OrigenNovedad.PermisosMedicos, hoy, hoy.AddDays(2), Usuario.IdSistema);
        n4.EmpleadoId = empleados[1].Id;
        n4.OrigenArea = "PLANTA";
        n4.RegistradoPorNombre = "APP PERMISOS MEDICOS (PRUEBA)";
        n4.DetalleOrigenJson = """{"origen":"DATOS_PRUEBA","nota":"Simula una novedad sincronizada desde la app Permisos Médicos"}""";

        db.Novedades.AddRange(n1, n2, n3, n4);
        await db.SaveChangesAsync(ct);

        await transaccion.CommitAsync(ct);
        logger.LogInformation("Datos de prueba creados: 3 proyectos, {Empleados} empleados, 4 novedades.", empleados.Count);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<Dictionary<string, Usuario>> AsegurarUsuariosAsync(IEnumerable<UsuarioPrueba> usuariosPrueba, CancellationToken ct)
    {
        var resultado = new Dictionary<string, Usuario>(StringComparer.OrdinalIgnoreCase);
        foreach (var up in usuariosPrueba)
        {
            var email = up.Email.Trim().ToLowerInvariant();
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (usuario is null)
            {
                usuario = new Usuario { EntraObjectId = up.ObjectId, Email = email, NombreMostrar = up.Nombre };
                db.Usuarios.Add(usuario);
            }
            resultado[up.Clave] = usuario;
        }
        await db.SaveChangesAsync(ct);
        return resultado;
    }

    private async Task<List<Empleado>> AsegurarEmpleadosAsync(CancellationToken ct)
    {
        string[] puestos = ["SUPERVISOR SSA", "PARAMEDICO", "TECNICO SSA", "INSPECTOR SSA", "ASISTENTE SSA", "SUPERVISOR SSA", "PARAMEDICO", "TECNICO SSA"];
        var lista = new List<Empleado>();
        for (var i = 1; i <= puestos.Length; i++)
        {
            var codigo = $"DEV{i:000}";
            var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.CodigoEkon == codigo, ct);
            if (empleado is null)
            {
                empleado = new Empleado
                {
                    CodigoEkon = codigo,
                    Cedula = $"99999999{i:00}",
                    NombreCompleto = $"EMPLEADO PRUEBA {i:00}",
                    Apellidos = "PRUEBA",
                    Nombres = $"EMPLEADO {i:00}",
                    CorreoEmpresa = $"empleado{i:00}.dev@profesiograma.local",
                    Empresa = "COMPAÑÍA DE PRUEBA S.A.",
                    Puesto = puestos[i - 1],
                    Departamento = DepartamentoPrueba,
                    Unidad = DepartamentoPrueba,
                    FamiliaPuesto = "ADMINISTRATIVO",
                    EstadoErp = "A",
                    FechaSincronizacion = reloj.GetUtcNow().UtcDateTime
                };
                db.Empleados.Add(empleado);
            }
            lista.Add(empleado);
        }
        return lista;
    }

    private static Proyecto NuevoProyecto(string codigo, string nombre, byte grupo, byte estado,
        DateOnly inicio, DateOnly fin, int propietarioId, int departamentoId, int companiaId) => new()
    {
        Codigo = codigo,
        NombreVisual = nombre,
        GrupoProyectoId = grupo,
        EstadoProyectoId = estado,
        CompaniaId = companiaId,
        FechaInicio = inicio,
        FechaFin = fin,
        HorarioCodigo = 1,
        HorarioDescripcion = "07:00 - 18:00 (PRUEBA)",
        HoraEntrada = new TimeOnly(7, 0),
        HoraSalida = new TimeOnly(18, 0),
        HorasJornadaMin = 660,
        HorasTrabajadasMin = 600,
        SalidaAlmuerzo = new TimeOnly(13, 0),
        RegresoAlmuerzo = new TimeOnly(14, 0),
        DepartamentoId = departamentoId,
        PropietarioUsuarioId = propietarioId,
        CreadoPorId = propietarioId
    };

    private static ProyectoPersonal Principal(Proyecto p, short numero, Empleado e, Jornada j, bool inicial) => new()
    {
        RolAsignacionId = CatalogoIds.RolAsignacion.Principal,
        Numero = numero,
        EmpleadoId = e.Id,
        CargoAsignado = e.Puesto,
        FechaInicio = p.FechaInicio,
        FechaFin = p.FechaFin,
        JornadaId = j.Id,
        DiasTrabajo = j.DiasTrabajo,
        DiasDescanso = j.DiasDescanso,
        EsPrincipalInicial = inicial,
        TipoRegistro = ProyectoPersonal.TipoRegistroJornada,
        CreadoPorId = p.CreadoPorId
    };

    private static ProyectoPersonal Back(Proyecto p, short numero, Empleado e, DateOnly inicio, DateOnly fin) => new()
    {
        RolAsignacionId = CatalogoIds.RolAsignacion.Back,
        Numero = numero,
        EmpleadoId = e.Id,
        CargoAsignado = e.Puesto,
        FechaInicio = inicio,
        FechaFin = fin,
        DiasDescanso = 0,
        TipoRegistro = ProyectoPersonal.TipoRegistroJornada,
        Observacion = "Back de prueba",
        CreadoPorId = p.CreadoPorId
    };

    private static ProyectoEtapa Etapa(Proyecto p, int version, byte movimiento, byte estado) => new()
    {
        Version = version,
        TipoMovimientoId = movimiento,
        EstadoProyectoId = estado,
        FechaInicio = p.FechaInicio,
        FechaFin = p.FechaFin,
        ActividadCodigo = "DEV.01",
        CreadoPorId = p.CreadoPorId
    };

    private static IEnumerable<ProyectoAsignacionDia> GenerarCicloPrincipal(ProyectoPersonal pp)
    {
        var trabajo = pp.DiasTrabajo ?? 0;
        var ciclo = Math.Max(1, trabajo + pp.DiasDescanso);
        var indice = 0;
        for (var fecha = pp.FechaInicio; fecha <= pp.FechaFin; fecha = fecha.AddDays(1))
        {
            var posicion = indice % ciclo;
            yield return new ProyectoAsignacionDia
            {
                ProyectoId = pp.ProyectoId,
                ProyectoPersonalId = pp.Id,
                EmpleadoId = pp.EmpleadoId,
                Fecha = fecha,
                RolAsignacionId = posicion < trabajo ? CatalogoIds.RolAsignacion.Principal : CatalogoIds.RolAsignacion.Descanso,
                TipoAsignacion = ProyectoAsignacionDia.TipoAuto,
                Bloque = (short)(indice / ciclo + 1),
                CreadoPorId = pp.CreadoPorId
            };
            indice++;
        }
    }

    private static IEnumerable<ProyectoAsignacionDia> GenerarBack(ProyectoPersonal pp)
    {
        for (var fecha = pp.FechaInicio; fecha <= pp.FechaFin; fecha = fecha.AddDays(1))
        {
            yield return new ProyectoAsignacionDia
            {
                ProyectoId = pp.ProyectoId,
                ProyectoPersonalId = pp.Id,
                EmpleadoId = pp.EmpleadoId,
                Fecha = fecha,
                RolAsignacionId = CatalogoIds.RolAsignacion.Back,
                TipoAsignacion = ProyectoAsignacionDia.TipoManual,
                Bloque = 1,
                CreadoPorId = pp.CreadoPorId
            };
        }
    }

    private static Novedad NuevaNovedad(string codigo, byte aplicacion, byte tipo, byte origen,
        DateOnly inicio, DateOnly fin, int creadoPorId)
    {
        var novedad = new Novedad
        {
            Codigo = codigo,
            TipoAplicacionNovedadId = aplicacion,
            TipoNovedadId = tipo,
            OrigenNovedadId = origen,
            FechaInicio = inicio,
            FechaFin = fin,
            CreadoPorId = creadoPorId
        };
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
        {
            novedad.Dias.Add(new NovedadDia { Fecha = fecha, CreadoPorId = creadoPorId });
        }
        return novedad;
    }

    private DateOnly HoyEcuador()
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(ZonaEcuador);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), zona).DateTime);
    }
}
