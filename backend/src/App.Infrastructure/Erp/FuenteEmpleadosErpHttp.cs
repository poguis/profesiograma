using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.Application.Empleados;
using App.Application.Erp;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Erp;

/// <summary>Descarga de los empleados activos del ERP (Http o Simulado). La caché la pone <see cref="CacheEmpleadosErp"/>.</summary>
internal interface IDescargaEmpleadosErp
{
    /// <exception cref="ErpNoDisponibleException">La API no respondió o la respuesta no sirve (incluido result vacío).</exception>
    Task<IReadOnlyList<EmpleadoErp>> DescargarAsync(CancellationToken ct);

    /// <summary>
    /// Lo mismo, con las cédulas aparte (TAREA-22-1, D15). Solo para la lectura fresca de <see cref="CacheEmpleadosErp"/>.
    /// Por defecto, sin cédulas.
    /// </summary>
    /// <exception cref="ErpNoDisponibleException">La API no respondió o la respuesta no sirve (incluido result vacío).</exception>
    async Task<(IReadOnlyList<EmpleadoErp> Registros, CedulasEmpleados Cedulas)> DescargarConCedulasAsync(CancellationToken ct) =>
        (await DescargarAsync(ct), CedulasEmpleados.Vacio);
}

/// <summary>
/// API EvolutionEmployee del ERP (TAREA-26d; contrato confirmado por el sondeo de la TAREA-26a):
/// POST {ErpBase7048}/api/EvolutionEmployee/EmployeesEvolution, cuerpo fijo (todos los activos de todas las empresas),
/// sin autenticación, envoltura { statusCode, isSuccess, errorMessages, result[] }, ~1633 registros, ~2,5 MB, sin paginación.
/// DATOS SENSIBLES: se deserializa SOLO a <see cref="EmpleadoJson"/>, que tiene únicamente los campos permitidos; el
/// resto de la respuesta (teléfono, dirección, sueldo, BPR, fechas personales…) nunca se materializa. La cédula (D15,
/// TAREA-22-1) se lee, pero sale aparte en <see cref="CedulasEmpleados"/>, nunca en EmpleadoErp.
/// Nunca se registra el cuerpo ni la URL. Sin caché propia (la pone CacheEmpleadosErp).
/// </summary>
internal sealed class FuenteEmpleadosErpHttp(
    HttpClient http,
    ServiciosExternosOpciones opciones,
    ILogger<FuenteEmpleadosErpHttp> logger) : IDescargaEmpleadosErp
{
    public const string Ruta = "api/EvolutionEmployee/EmployeesEvolution";
    private const string Operacion = "empleados";

    /// <summary>Cuerpo fijo (TAREA-26a, P5/P6): parameter vacío = todos; estado "A"; todas las empresas y departamentos.</summary>
    internal static readonly SolicitudEmpleados Cuerpo = new(string.Empty, "A", string.Empty, string.Empty);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<EmpleadoErp>> DescargarAsync(CancellationToken ct) => (await DescargarConCedulasAsync(ct)).Registros;

    public async Task<(IReadOnlyList<EmpleadoErp> Registros, CedulasEmpleados Cedulas)> DescargarConCedulasAsync(CancellationToken ct)
    {
        var uri = new Uri(new Uri(opciones.ErpBase7048.TrimEnd('/') + "/"), Ruta);
        HttpResponseMessage respuesta;
        var cronometro = Stopwatch.StartNew();
        try
        {
            respuesta = await http.PostAsJsonAsync(uri, Cuerpo, Json, ct);
        }
        catch (HttpRequestException ex)
        {
            throw Registrar(new ErpNoDisponibleException(Operacion, "error de red o de conexión segura", ex));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw Registrar(new ErpNoDisponibleException(Operacion, "tiempo de espera agotado", ex));
        }

        logger.LogDebug("ERP {Operacion}: HTTP {Codigo} en {Milisegundos} ms", Operacion, (int)respuesta.StatusCode,
            cronometro.ElapsedMilliseconds);

        RespuestaEmpleados? contenido;
        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                throw Registrar(new ErpNoDisponibleException(Operacion, $"respuesta HTTP {(int)respuesta.StatusCode}"));
            }

            try
            {
                contenido = await respuesta.Content.ReadFromJsonAsync<RespuestaEmpleados>(Json, ct);
            }
            catch (JsonException ex)
            {
                // Sin la excepción interna: su mensaje podría incluir un fragmento de la respuesta.
                throw Registrar(new ErpNoDisponibleException(Operacion, $"respuesta con formato inválido ({ex.GetType().Name})"));
            }
        }

        // TAREA-26d: result vacío tampoco es una respuesta válida (todas las empresas siempre tienen activos).
        if (contenido is not { IsSuccess: true, Result: { Count: > 0 } registros })
        {
            throw Registrar(new ErpNoDisponibleException(Operacion, "respuesta sin resultados (isSuccess = false o result nulo o vacío)"));
        }

        var json = registros.OfType<EmpleadoJson>().ToList();
        var empleados = json.Select(r => new EmpleadoErp(
            r.CodPersona, r.NombreCompleto, r.Apellidos, r.Nombres, r.MailEmpresa, r.CodEmpresa, r.Empresa, r.CodPuesto, r.Puesto,
            r.CodDepartamento, r.Departamento, r.CodUnidad, r.Unidad, r.CodArea, r.Area, r.CodSeccion, r.Seccion,
            r.FamiliaPuesto, r.Estado)).ToList();
        return (empleados, new CedulasEmpleados(json.Select(r => (r.CodPersona, r.Cedula))));
    }

    private ErpNoDisponibleException Registrar(ErpNoDisponibleException ex)
    {
        logger.LogWarning(ex.InnerException, "{Mensaje}", ex.Message);
        return ex;
    }

    // ------------------------------------------------------------------ contrato JSON

    internal sealed record SolicitudEmpleados(string Parameter, string Estado, string CodEmpresa, string CodDepartamento);

    internal sealed record RespuestaEmpleados(int StatusCode, bool IsSuccess, List<EmpleadoJson?>? Result);

    /// <summary>
    /// SOLO los campos permitidos (docs/fases/FASE_4_Empleados_API.md §3). "nombrecompleto" llega en minúsculas: se lee
    /// sin distinguir mayúsculas (JsonSerializerDefaults.Web). Los códigos pueden llegar como texto o como número.
    /// Cedula (D15, TAREA-22-1): solo texto (<see cref="SoloTexto"/>); ToString no la imprime.
    /// </summary>
    internal sealed record EmpleadoJson(
        [property: JsonConverter(typeof(TextoONumero))] string? CodPersona,
        string? NombreCompleto,
        string? Apellidos,
        string? Nombres,
        string? MailEmpresa,
        [property: JsonConverter(typeof(TextoONumero))] string? CodEmpresa,
        string? Empresa,
        [property: JsonConverter(typeof(TextoONumero))] string? CodPuesto,
        string? Puesto,
        [property: JsonConverter(typeof(TextoONumero))] string? CodDepartamento,
        string? Departamento,
        [property: JsonConverter(typeof(TextoONumero))] string? CodUnidad,
        string? Unidad,
        [property: JsonConverter(typeof(TextoONumero))] string? CodArea,
        string? Area,
        [property: JsonConverter(typeof(TextoONumero))] string? CodSeccion,
        string? Seccion,
        string? FamiliaPuesto,
        string? Estado,
        [property: JsonConverter(typeof(SoloTexto))] string? Cedula = null)
    {
        /// <summary>Sin la cédula (dato sensible): un ToString accidental en un log no la expone.</summary>
        public override string ToString() => $"{nameof(EmpleadoJson)}({CodPersona})";
    }

    /// <summary>Lee un código que puede venir como texto o como número (texto invariante).</summary>
    internal sealed class TextoONumero : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var entero)
                    ? entero.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
                JsonTokenType.Null => null,
                _ => throw new JsonException("Código con un tipo no admitido."),
            };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    /// <summary>
    /// Cédula (D15): TOLERANTE, nunca lanza. Texto → sin espacios extremos (vacío → null). Número, booleano, objeto o
    /// arreglo → null: un número habría perdido los ceros iniciales y no se reconstruye; una cédula rara no debe invalidar
    /// al empleado ni a la lista (daría 503 a todo).
    /// </summary>
    internal sealed class SoloTexto : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return MapeoEmpleado.Normalizar(reader.GetString());
            }

            reader.Skip(); // objeto o arreglo: se salta completo; un valor simple no avanza
            return null;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
