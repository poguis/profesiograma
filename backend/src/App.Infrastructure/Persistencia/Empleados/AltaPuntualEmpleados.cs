using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Domain.Configuracion;
using App.Domain.Maestros;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Empleados;

/// <summary>
/// Alta puntual de empleados (TAREA-26d, opción C): la tabla Empleado solo tiene a las personas asignadas. Al asignar a
/// una persona NUEVA (crear proyecto, actualizar personal, reactivar) se crea su fila, o se refresca la existente (P6),
/// con los datos de la API validados FUERA de la transacción (lectura fresca, P5). Se ejecuta DENTRO de
/// ITransaccionAsignaciones (applock de asignaciones: todas las escrituras de Empleado pasan por ahí), antes de crear
/// ProyectoPersonal. Mapeo de campos permitidos (MapeoEmpleado); la cédula y los datos sensibles nunca se escriben.
/// También da de alta el puesto en CargoInfor (P8). Auditoría: AuditoriaInterceptor (usuario que asigna).
/// </summary>
internal static class AltaPuntualEmpleados
{
    private static readonly int[] ErroresUnicos = [2601, 2627];

    /// <summary>
    /// Crea o refresca las filas y devuelve el Id real de cada empleado (clave: el Id que trae el plan, real o temporal
    /// negativo). Los que no traen datos de la API (personas guardadas) conservan su Id.
    /// </summary>
    /// <exception cref="ConflictoConcurrenciaException">Duplicado en UQ_Empleado_CodigoEkon o UQ_CargoInfor_Cargo (409).</exception>
    public static async Task<IReadOnlyDictionary<int, int>> AplicarAsync(
        ProfesiogramaDbContext db, IEnumerable<EmpleadoAsignable> empleados, CancellationToken ct)
    {
        var altas = empleados.Where(e => e.Erp is not null).DistinctBy(e => e.Id).ToList();
        var ids = new Dictionary<int, int>();
        if (altas.Count == 0)
        {
            return ids;
        }

        var fecha = DateTime.UtcNow;
        fecha = fecha.AddTicks(-(fecha.Ticks % TimeSpan.TicksPerSecond)); // DATETIME2(0)

        var codigos = altas.Select(a => a.CodigoEkon).ToList();
        var existentes = (await ConsultaPorCodigosSeguimiento(db, codigos).ToListAsync(ct))
            .ToDictionary(e => e.CodigoEkon, StringComparer.OrdinalIgnoreCase);
        var cargos = altas.Select(a => MapeoEmpleado.NormalizarCargo(a.Erp!.Puesto)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var cargosGuardados = cargos.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await ConsultaCargosExistentes(db, cargos).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var preparado = Preparar(altas, existentes, cargosGuardados, fecha);
        db.Empleados.AddRange(preparado.Nuevas);
        db.CargosInfor.AddRange(preparado.CargosNuevos.Select(c => new CargoInfor { Cargo = c, CodigoInfor = null, Activo = true }));
        var filas = preparado.Filas;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && ErroresUnicos.Contains(sql.Number))
        {
            throw new ConflictoConcurrenciaException("Otro registro dio de alta al mismo empleado al mismo tiempo.", ex);
        }

        foreach (var (idPlan, fila) in filas)
        {
            ids[idPlan] = fila.Id;
        }

        return ids;
    }

    /// <summary>Resultado puro de la alta: filas nuevas, fila (nueva o existente) por Id del plan y cargos nuevos.</summary>
    internal sealed record AltaPreparada(IReadOnlyList<Empleado> Nuevas, IReadOnlyDictionary<int, Empleado> Filas, IReadOnlyList<string> CargosNuevos);

    /// <summary>
    /// Lógica pura (sin base): refresca las filas existentes (por código, sin distinguir mayúsculas) con los datos de la
    /// API, crea las que faltan y calcula los cargos que no están en CargoInfor. Las altas sin datos de la API se ignoran.
    /// </summary>
    internal static AltaPreparada Preparar(IEnumerable<EmpleadoAsignable> altas, IDictionary<string, Empleado> existentes,
        IReadOnlySet<string> cargosGuardados, DateTime fechaUtc)
    {
        var nuevas = new List<Empleado>();
        var filas = new Dictionary<int, Empleado>();
        var cargos = new List<string>();
        foreach (var alta in altas.Where(a => a.Erp is not null).DistinctBy(a => a.Id))
        {
            if (!existentes.TryGetValue(alta.CodigoEkon, out var fila))
            {
                fila = new Empleado { CodigoEkon = alta.CodigoEkon };
                nuevas.Add(fila);
                existentes[alta.CodigoEkon] = fila;
            }

            Asignar(fila, MapeoEmpleado.Datos(alta.Erp!), fechaUtc);
            filas[alta.Id] = fila;

            if (MapeoEmpleado.NormalizarCargo(alta.Erp!.Puesto) is { } cargo && !cargosGuardados.Contains(cargo) && !cargos.Contains(cargo))
            {
                cargos.Add(cargo);
            }
        }

        return new AltaPreparada(nuevas, filas, cargos);
    }

    /// <summary>Id real para persistir: el de la alta (si lo hubo) o el mismo. Nunca un Id temporal (negativo).</summary>
    public static int IdReal(IReadOnlyDictionary<int, int> ids, int idPlan)
    {
        var id = ids.TryGetValue(idPlan, out var real) ? real : idPlan;
        return id > 0
            ? id
            : throw new InvalidOperationException($"El empleado con Id temporal {idPlan} no se dio de alta antes de asignarlo.");
    }

    private static void Asignar(Empleado e, DatosEmpleado d, DateTime fechaUtc)
    {
        e.NombreCompleto = d.NombreCompleto;
        e.Apellidos = d.Apellidos;
        e.Nombres = d.Nombres;
        e.CorreoEmpresa = d.CorreoEmpresa;
        e.CodEmpresa = d.CodEmpresa;
        e.Empresa = d.Empresa;
        e.CodPuesto = d.CodPuesto;
        e.Puesto = d.Puesto;
        e.CodDepartamento = d.CodDepartamento;
        e.Departamento = d.Departamento;
        e.CodUnidad = d.CodUnidad;
        e.Unidad = d.Unidad;
        e.CodArea = d.CodArea;
        e.Area = d.Area;
        e.CodSeccion = d.CodSeccion;
        e.Seccion = d.Seccion;
        e.FamiliaPuesto = d.FamiliaPuesto;
        e.EstadoErp = MapeoEmpleado.EstadoActivo; // estado en la API al asignarlo
        e.FechaSincronizacion = fechaUtc;         // fecha de la última copia desde la API
        // Cedula no se toca: la alta puntual nunca la escribe (P10 de la 26b).
    }

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    /// <summary>Filas de Empleado por código, CON seguimiento (refresco).</summary>
    internal static IQueryable<Empleado> ConsultaPorCodigosSeguimiento(ProfesiogramaDbContext db, IReadOnlyCollection<string> codigos) =>
        db.Empleados.Where(e => codigos.Contains(e.CodigoEkon));

    internal static IQueryable<string> ConsultaCargosExistentes(ProfesiogramaDbContext db, IReadOnlyCollection<string> cargos) =>
        db.CargosInfor.AsNoTracking().Where(c => cargos.Contains(c.Cargo)).Select(c => c.Cargo);
}
