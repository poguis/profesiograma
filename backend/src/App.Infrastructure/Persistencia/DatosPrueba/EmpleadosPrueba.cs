using App.Application.Empleados;
using App.Domain.Maestros;

namespace App.Infrastructure.Persistencia.DatosPrueba;

/// <summary>
/// Empleados de prueba (SOLO Development; datos ficticios). Una sola definición para:
///  - DatosPruebaSembrador: filas DEV001–DEV008 de la tabla Empleado;
///  - EmpleadosErpSimulado (TAREA-26d, P12): la "API" del modo Simulado devuelve los mismos DEV (datos IDÉNTICOS, así la
///    alta puntual no cambia sus filas) más SIM001–SIM200, sin datos sensibles.
/// </summary>
internal static class EmpleadosPrueba
{
    public const string DepartamentoSig = "UNIDAD SISTEMA INTEGRADO DE GESTION";
    public const string Empresa = "COMPAÑÍA DE PRUEBA S.A.";
    public const string FamiliaPuesto = "ADMINISTRATIVO";
    public const int CantidadSimulados = 200;

    private static readonly string[] PuestosDev =
        ["SUPERVISOR SSA", "PARAMEDICO", "TECNICO SSA", "INSPECTOR SSA", "ASISTENTE SSA", "SUPERVISOR SSA", "PARAMEDICO", "TECNICO SSA"];

    /// <summary>Los 7 departamentos/unidades de la tabla Departamento (semillas de la migración Inicial).</summary>
    private static readonly string[] Departamentos =
    [
        "DEPARTAMENTO DE INFRAESTRUCTURA", "DEPARTAMENTO SEDEMI TELECOM", "DEPARTAMENTO SEDEMI PETROLEO Y GAS",
        "DEPARTAMENTO SEDEMI ENERGIA", "DEPARTAMENTO DE INFRAESTRUCTURA METALICA", "DEPARTAMENTO SEDEMI MINERIA",
    ];

    private static readonly string[] PuestosSimulados = ["TECNICO SSA", "SUPERVISOR SSA", "PARAMEDICO", "INSPECTOR SSA", "ASISTENTE SSA"];

    /// <summary>Fila DEV de la tabla Empleado (i = 1..8), como la crea el sembrador.</summary>
    public static Empleado CrearDev(int i, DateTime fechaSincronizacionUtc) => new()
    {
        CodigoEkon = $"DEV{i:000}",
        Cedula = $"99999999{i:00}",
        NombreCompleto = $"EMPLEADO PRUEBA {i:00}",
        Apellidos = "PRUEBA",
        Nombres = $"EMPLEADO {i:00}",
        CorreoEmpresa = $"empleado{i:00}.dev@profesiograma.local",
        Empresa = Empresa,
        Puesto = PuestosDev[i - 1],
        Departamento = DepartamentoSig,
        Unidad = DepartamentoSig,
        FamiliaPuesto = FamiliaPuesto,
        EstadoErp = MapeoEmpleado.EstadoActivo,
        FechaSincronizacion = fechaSincronizacionUtc,
    };

    public static int CantidadDev => PuestosDev.Length;

    /// <summary>Lista del modo Simulado: DEV001–DEV008 (mismos datos laborales que las filas) + SIM001–SIM200.</summary>
    public static IReadOnlyList<EmpleadoErp> ListaSimulada()
    {
        var lista = new List<EmpleadoErp>();
        for (var i = 1; i <= CantidadDev; i++)
        {
            var e = CrearDev(i, DateTime.MinValue);
            lista.Add(new EmpleadoErp(e.CodigoEkon, e.NombreCompleto, e.Apellidos, e.Nombres, e.CorreoEmpresa, e.CodEmpresa, e.Empresa,
                e.CodPuesto, e.Puesto, e.CodDepartamento, e.Departamento, e.CodUnidad, e.Unidad, e.CodArea, e.Area, e.CodSeccion,
                e.Seccion, e.FamiliaPuesto, MapeoEmpleado.EstadoActivo));
        }

        // SIM: 60 % en la unidad SIG y el resto repartido entre los otros 6 departamentos de la tabla.
        for (var i = 1; i <= CantidadSimulados; i++)
        {
            var departamento = i % 10 < 6 ? DepartamentoSig : Departamentos[i % Departamentos.Length];
            lista.Add(new EmpleadoErp($"SIM{i:000}", $"EMPLEADO SIMULADO {i:000}", "SIMULADO", $"EMPLEADO {i:000}",
                $"sim{i:000}.dev@profesiograma.local", null, Empresa, null, PuestosSimulados[i % PuestosSimulados.Length],
                null, departamento, null, departamento, null, null, null, null, FamiliaPuesto, MapeoEmpleado.EstadoActivo));
        }

        return lista;
    }
}
