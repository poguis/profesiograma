using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Endpoints;

/// <summary>
/// TAREA-25-3-1: CargoInfor (Politica.Admin; el gestor recibe 403). Sin alta manual ni borrado. Logs: SOLO conteos e Id
/// (cargo y Admin); nunca nombres de puestos ni códigos.
/// </summary>
public static class CargoInforEndpoints
{
    public static IEndpointRouteBuilder MapCargoInforEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/cargos-infor").WithTags("Cargos Infor").RequireAuthorization(Politicas.Admin);

        // Filtros: texto (cargo o código, contiene), filtro (todos | sinCodigo | conCodigo), incluirInactivos, pagina, tamano.
        admin.MapGet("/", async Task<Results<Ok<PaginaResultado<CargoInforDto>>, ValidationProblem>> (
            [AsParameters] CargoInforListadoSolicitud solicitud, [FromServices] CargosInforServicio servicio, CancellationToken ct) =>
        {
            var r = await servicio.ListarAsync(solicitud, ct);
            return r.EsValido ? TypedResults.Ok(r.Valor!) : TypedResults.ValidationProblem(r.Errores!, title: "Parámetros de consulta no válidos.");
        });

        // Vista previa con la lista en caché (sin lista utilizable → 503 por el manejador común del ERP). No escribe.
        admin.MapGet("/importacion", async ([FromServices] CargosInforServicio servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.VistaPreviaAsync(ct)));

        // Importación (sin cuerpo): lectura fresca (503 sin abrir la transacción), applock de asignaciones (503), 409 si salta
        // UQ_CargoInfor_Cargo. Idempotente.
        admin.MapPost("/importacion", async Task<Results<Ok<ImportacionCargosDto>, ProblemHttpResult>> (
            [FromServices] CargosInforServicio servicio, IUsuarioActual usuario, ILoggerFactory registro, CancellationToken ct) =>
        {
            var r = await servicio.ImportarAsync(ct);
            if (r.Estado != EstadoCargoInfor.Ok)
            {
                return TypedResults.Problem(title: CargosInforServicio.MensajeConflictoImportacion, statusCode: StatusCodes.Status409Conflict);
            }

            Log(registro).LogInformation(
                "Importación de cargos Infor (admin {AdminId}): puestos {PuestosApi}, insertados {Insertados}, ya existentes {YaExistentes}, inactivos {Inactivos}.",
                usuario.UsuarioId, r.Valor!.PuestosApi, r.Valor.Insertados, r.Valor.YaExistentes, r.Valor.ExistentesInactivos);
            return TypedResults.Ok(r.Valor);
        });

        // 200 { id, codigoInfor, activo }; 400 en codigoInfor/activo/anterior; 404; 409 si otro lo cambió (compare-and-set).
        admin.MapPut("/{id:int}", async Task<Results<Ok<CargoInforEditadoDto>, ValidationProblem, NotFound<ProblemDetails>, ProblemHttpResult>> (
            int id, EditarCargoInforSolicitud solicitud, [FromServices] CargosInforServicio servicio, IUsuarioActual usuario,
            ILoggerFactory registro, CancellationToken ct) =>
        {
            var r = await servicio.EditarAsync(id, solicitud, ct);
            switch (r.Estado)
            {
                case EstadoCargoInfor.Ok:
                    Log(registro).LogInformation("Cargo Infor {Id} actualizado: con código {ConCodigo}, activo {ActivoAnterior} → {Activo} (admin {AdminId}).",
                        id, r.Valor!.CodigoInfor is not null, solicitud.ActivoAnterior, r.Valor.Activo, usuario.UsuarioId);
                    return TypedResults.Ok(r.Valor);
                case EstadoCargoInfor.Invalido:
                    return TypedResults.ValidationProblem(r.Errores!, title: "Los datos del cargo no son válidos.");
                case EstadoCargoInfor.NoEncontrado:
                    return TypedResults.NotFound(new ProblemDetails { Title = CargosInforServicio.MensajeNoExiste, Status = StatusCodes.Status404NotFound });
                default:
                    return TypedResults.Problem(title: CargosInforServicio.MensajeConflicto, statusCode: StatusCodes.Status409Conflict);
            }
        });

        return app;
    }

    private static ILogger Log(ILoggerFactory registro) => registro.CreateLogger(nameof(CargoInforEndpoints));
}
