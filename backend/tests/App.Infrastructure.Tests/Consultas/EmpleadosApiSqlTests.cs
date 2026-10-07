using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Domain.Maestros;
using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.Empleados;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>TAREA-26d: traducción a SQL de las consultas nuevas (alta puntual y datos de referencia) y alta puntual pura.</summary>
public class EmpleadosApiSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void IdsPorCodigos_SinDatosSensibles()
    {
        var sql = Sql(DatosReferenciaProyecto.ConsultaIdsPorCodigos(Db, ["DEV001", "900001"]));

        Assert.Contains("FROM [dbo].[Empleado]", sql);
        Assert.Contains("[CodigoEkon]", sql);
        Assert.DoesNotContain("[Cedula]", sql);
        Assert.DoesNotContain("[CorreoEmpresa]", sql);
    }

    [Fact]
    public void AltaPuntual_EmpleadosPorCodigo_YCargosExistentes()
    {
        Assert.Contains("[CodigoEkon] IN", Sql(AltaPuntualEmpleados.ConsultaPorCodigosSeguimiento(Db, ["DEV001", "900001"])));
        var cargos = Sql(AltaPuntualEmpleados.ConsultaCargosExistentes(Db, ["TECNICO", "SUPERVISOR"]));
        Assert.Contains("FROM [dbo].[CargoInfor]", cargos);
        Assert.Contains("[Cargo] IN", cargos);
    }

    // ------------------------------------------------------------------ alta puntual (lógica pura)

    private static EmpleadoAsignable Alta(int id, string codigo, string? puesto = "Técnico SSA") =>
        new(id, codigo, $"PERSONA {codigo}", puesto,
            new EmpleadoErp(codigo, $"PERSONA {codigo}", "APELLIDO", "NOMBRE", $"{codigo}@ficticio.local", "EF1", "EMPRESA", "P1", puesto,
                "D1", "DEPTO", "U1", "UNIDAD", "A1", "AREA", "S1", "SECCION", "OPERATIVO", "A"));

    [Fact]
    public void Preparar_NuevoExistenteRefrescado_CargosNuevos_SinCedula()
    {
        var existente = new Empleado { Id = 6, CodigoEkon = "DEV006", NombreCompleto = "VIEJO", Puesto = "VIEJO", EstadoErp = "I", Cedula = "9999999906" };
        var existentes = new Dictionary<string, Empleado>(StringComparer.OrdinalIgnoreCase) { ["DEV006"] = existente };
        var fecha = new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);

        var r = AltaPuntualEmpleados.Preparar(
            [Alta(-1, "900001"), Alta(6, "dev006", "Supervisor"), Alta(-1, "900001"), new EmpleadoAsignable(7, "DEV007", "X", null)],
            existentes, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SUPERVISOR" }, fecha);

        var nueva = Assert.Single(r.Nuevas);
        Assert.Equal(("900001", "PERSONA 900001", "A", fecha), (nueva.CodigoEkon, nueva.NombreCompleto, nueva.EstadoErp, nueva.FechaSincronizacion));
        Assert.Null(nueva.Cedula);
        Assert.Same(existente, r.Filas[6]);
        Assert.Equal(("PERSONA dev006", "Supervisor", "A", "9999999906"), (existente.NombreCompleto, existente.Puesto, existente.EstadoErp, existente.Cedula));
        Assert.Equal(["TÉCNICO SSA"], r.CargosNuevos); // "SUPERVISOR" ya estaba
        Assert.Equal([-1, 6], r.Filas.Keys.Order()); // la persona guardada sin datos de la API (7) no se toca
    }

    [Fact]
    public void IdReal_NuncaNegativo()
    {
        var ids = new Dictionary<int, int> { [-1] = 501 };

        Assert.Equal(501, AltaPuntualEmpleados.IdReal(ids, -1));
        Assert.Equal(7, AltaPuntualEmpleados.IdReal(ids, 7));
        Assert.Throws<InvalidOperationException>(() => AltaPuntualEmpleados.IdReal(ids, -2));
    }
}
