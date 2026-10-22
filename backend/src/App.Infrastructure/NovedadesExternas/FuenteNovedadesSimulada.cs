using App.Application.Novedades.Sincronizacion;

namespace App.Infrastructure.NovedadesExternas;

/// <summary>
/// Fuente SIMULADA de PERMISO MEDICO (TAREA-23-3, solo Development). 10 filas FICTICIAS con Uid fijos, códigos
/// NOV-SIMPM-0001…0010, año 2075 y LegacyId desde 2.000.000.001. Usa DEV001–DEV006, que tienen fila en Empleado (el
/// sembrador), así que funciona también con el ERP en modo Http. Cubre: (1) nueva; (2) estable; (3) cambio de fechas;
/// (4) tipo VACACIONES → tipoNoSoportado; (5) JSON inválido; (6) EKON ZZ999 sin fila → sinEmpleado; (7) solo correo
/// (DEV004); (8) Persona.Ekon (DEV005); (9) sin Dias (DEV006, 3 días por rango); (10) aplicacionProyectos → otroOrigen.
/// Escenarios (P1): base; fechas (la fila 3 pasa a 3 días); anuladas (las 6 válidas con ESTADO «ANULADA»).
/// </summary>
internal sealed class FuenteNovedadesSimulada : IFuenteNovedadesExternas
{
    public const string Nombre = "Simulado";
    public const int LegacyBase = 2_000_000_000;

    /// <summary>Filas que el escenario «anuladas» pasa a ESTADO ANULADA (las 6 que se sincronizan).</summary>
    public static readonly int[] Validas = [1, 2, 3, 7, 8, 9];

    public string Fuente => Nombre;
    public bool AdmiteEscenarios => true;

    public Task<LecturaNovedadesExternas> LeerPermisosMedicosAsync(string? escenario, CancellationToken ct) =>
        Task.FromResult(new LecturaNovedadesExternas(Nombre, Filas(escenario ?? FuenteNovedades.EscenarioBase), 0));

    public static Guid Uid(int n) => Guid.Parse($"2075a0de-0000-4000-8000-{n:000000000000}");

    public static string Codigo(int n) => $"NOV-SIMPM-{n:0000}";

    internal static IReadOnlyList<FilaNovedadExterna> Filas(string escenario)
    {
        var fechas = escenario == FuenteNovedades.EscenarioFechas;
        var anuladas = escenario == FuenteNovedades.EscenarioAnuladas;

        FilaNovedadExterna Fila(int n, string? json) => new(
            LegacyBase + n, Codigo(n), Uid(n).ToString(), anuladas && Validas.Contains(n) ? "ANULADA" : "ACTIVA", json, null);

        return
        [
            Fila(1, Json(1, fechasEkon: "DEV001", dias: ["2075-03-01", "2075-03-02"])),
            Fila(2, Json(2, fechasEkon: "DEV002", dias: ["2075-03-06"])),
            Fila(3, Json(3, fechasEkon: "DEV003", dias: fechas ? ["2075-03-11", "2075-03-12", "2075-03-13"] : ["2075-03-11"])),
            Fila(4, Json(4, fechasEkon: "DEV001", dias: ["2075-03-16"], tipo: "VACACIONES")),
            Fila(5, "{ esto no es JSON"),
            Fila(6, Json(6, fechasEkon: "ZZ999", dias: ["2075-03-26"])),
            Fila(7, Json(7, correo: "empleado04.dev@profesiograma.local", dias: ["2075-03-31"])),
            Fila(8, Json(8, personaEkon: "DEV005", dias: ["2075-04-05"])),
            Fila(9, Json(9, fechasEkon: "DEV006", dias: null, inicio: "2075-04-10", fin: "2075-04-12")),
            Fila(10, Json(10, fechasEkon: "DEV007", dias: ["2075-04-15"], origenPermiso: "aplicacionProyectos")),
        ];
    }

    /// <summary>DETALLE_JSON con la forma del original (datos FICTICIOS).</summary>
    private static string Json(int n, string[]? dias, string? fechasEkon = null, string? personaEkon = null, string? correo = null,
        string tipo = "PERMISO MEDICO", string origenPermiso = "aplicacionPermisosMedicos", string? inicio = null, string? fin = null)
    {
        static string Texto(string? v) => v is null ? "null" : $"\"{v}\"";
        inicio ??= dias?[0];
        fin ??= dias?[^1];
        var diasJson = dias is null ? "" : $",\"Dias\":[{string.Join(",", dias.Select(d => $"{{\"Fecha\":\"{d}\",\"FechaClave\":\"{d}\",\"EstadoExcel\":\"LIBRE\",\"ObservacionExcel\":\"{tipo}\"}}"))}]";
        var personaEkonJson = personaEkon is null ? "" : $",\"Ekon\":\"{personaEkon}\"";
        var fechasEkonJson = fechasEkon is null ? "" : $",\"Ekon\":\"{fechasEkon}\"";
        return $"{{\"NovedadUID\":\"{Uid(n)}\",\"TipoAplicacion\":\"PERSONA\",\"TipoNovedad\":\"{tipo}\",\"CategoriaId\":5," +
               $"\"CategoriaDescripcion\":\"Asistencia Libre\",\"EstadoExcel\":\"LIBRE\",\"ObservacionExcel\":\"{tipo}\",\"Sigla\":\"PM\"," +
               $"\"Persona\":{{\"Correo\":{Texto(correo)},\"Nombre\":\"PERSONA SIMULADA {n:00}\"{personaEkonJson}}}," +
               $"\"OrigenPermiso\":\"{origenPermiso}\",\"Origen\":\"PLANTA\"," +
               "\"Proyecto\":{\"ProjectUID\":null,\"CompanyId\":null,\"NombreProyecto\":null}," +
               $"\"Fechas\":{{\"FechaInicio\":{Texto(inicio)},\"FechaFin\":{Texto(fin)}{fechasEkonJson}}}{diasJson}," +
               "\"Registro\":{\"FechaRegistro\":\"2075-02-01 10:00:00\",\"UsuarioRegistro\":\"APP PERMISOS MEDICOS (SIMULADA)\"," +
               "\"CorreoRegistro\":\"permisos.simulados@profesiograma.local\"}}";
    }
}
