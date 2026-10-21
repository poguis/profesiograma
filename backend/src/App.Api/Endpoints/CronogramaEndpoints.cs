using System.Globalization;
using App.Application.Comun;
using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

/// <summary>
/// Cronograma (TAREA-20, docs/fases/FASE_5_Cronograma.md). Solo lectura. Los parámetros llegan como texto y los valida el
/// servicio: un valor ilegible responde 400 en su campo (nunca un error de enlace).
/// </summary>
public static class CronogramaEndpoints
{
    public static IEndpointRouteBuilder MapCronogramaEndpoints(this IEndpointRouteBuilder app)
    {
        // desde, hasta (yyyy-MM-dd), proyectoId, departamentoId y codigoEkon, todos opcionales.
        app.MapGet("/api/cronograma", async Task<Results<Ok<CronogramaDto>, ValidationProblem>> (
                [AsParameters] CronogramaSolicitud solicitud, CronogramaConsultaServicio servicio, CancellationToken ct) =>
                Responder(await servicio.ConsultarAsync(solicitud, ct)))
            .WithTags("Cronograma")
            .RequireAuthorization(Politicas.Gestor);

        // Solo los días de ese proyecto. Inexistente, eliminado o no visible → 404 (nunca 403).
        app.MapGet("/api/proyectos/{id:int}/cronograma", async Task<Results<Ok<CronogramaDto>, ValidationProblem, NotFound>> (
                int id, [AsParameters] ProyectoCronogramaSolicitud solicitud, CronogramaConsultaServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.ConsultarProyectoAsync(id, solicitud, ct);
                if (resultado is null)
                {
                    return TypedResults.NotFound();
                }

                return resultado.EsValido
                    ? TypedResults.Ok(resultado.Valor!)
                    : TypedResults.ValidationProblem(resultado.Errores!, title: TituloInvalido);
            })
            .WithTags("Cronograma")
            .RequireAuthorization(Politicas.Gestor);

        // TAREA-22-2: reporte Excel del proyecto (desde y hasta obligatorios, sin tope). Inexistente o no visible → 404.
        // El libro se arma en memoria. Log: Id del proyecto, Id del usuario, rango y cantidad de filas (nunca nombres ni cédulas).
        app.MapGet("/api/proyectos/{id:int}/reporte", async Task<Results<FileContentHttpResult, ValidationProblem, NotFound>> (
                int id, [AsParameters] ReporteProyectoSolicitud solicitud, ReporteProyectoServicio servicio, IEscritorReporteExcel escritor,
                IUsuarioActual usuario, ILoggerFactory registro, HttpContext contexto, CancellationToken ct) =>
            {
                var resultado = await servicio.ArmarAsync(id, solicitud, ct);
                if (resultado is null)
                {
                    return TypedResults.NotFound();
                }

                if (!resultado.EsValido)
                {
                    return TypedResults.ValidationProblem(resultado.Errores!, title: TituloInvalido);
                }

                var reporte = resultado.Valor!;
                var archivo = escritor.Escribir(reporte.Filas);
                registro.CreateLogger(nameof(CronogramaEndpoints)).LogInformation(
                    "Reporte Excel: proyecto {ProyectoId}, usuario {UsuarioId}, {Desde} a {Hasta}, {Filas} filas.",
                    id, usuario.UsuarioId, reporte.Desde, reporte.Hasta, reporte.Filas.Count);
                contexto.Response.Headers.CacheControl = "no-store";
                contexto.Response.Headers[EncabezadoFilas] = reporte.Filas.Count.ToString(CultureInfo.InvariantCulture);
                return TypedResults.File(archivo, TipoXlsx, reporte.NombreArchivo); // Content-Disposition con filename*
            })
            .WithTags("Cronograma")
            .RequireAuthorization(Politicas.Gestor);

        // Solo conteos (sin nombres ni cédulas), con los mismos parámetros.
        app.MapGet("/api/proyectos/{id:int}/reporte/resumen", async Task<Results<Ok<ResumenReporteDto>, ValidationProblem, NotFound>> (
                int id, [AsParameters] ReporteProyectoSolicitud solicitud, ReporteProyectoServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.ResumirAsync(id, solicitud, ct);
                if (resultado is null)
                {
                    return TypedResults.NotFound();
                }

                return resultado.EsValido
                    ? TypedResults.Ok(resultado.Valor!)
                    : TypedResults.ValidationProblem(resultado.Errores!, title: TituloInvalido);
            })
            .WithTags("Cronograma")
            .RequireAuthorization(Politicas.Gestor);

        return app;
    }

    /// <summary>Cantidad de filas del reporte (0 = tabla vacía, Z3).</summary>
    public const string EncabezadoFilas = "X-Reporte-Filas";

    public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static Results<Ok<CronogramaDto>, ValidationProblem> Responder(ResultadoConsulta<CronogramaDto> resultado) =>
        resultado.EsValido
            ? TypedResults.Ok(resultado.Valor!)
            : TypedResults.ValidationProblem(resultado.Errores!, title: TituloInvalido);

    private const string TituloInvalido = "Parámetros de consulta no válidos.";
}
