using App.Application.Comun;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Endpoints;

public static class UsuarioEndpoints
{
    public static IEndpointRouteBuilder MapUsuarioEndpoints(this IEndpointRouteBuilder app)
    {
        // Cualquier usuario autenticado: útil para probar DevAuth y, luego, Entra ID.
        app.MapGet("/api/usuarios/me", (IUsuarioActual usuario) =>
                TypedResults.Ok(new UsuarioActualDto(usuario.UsuarioId!.Value, usuario.Email!, usuario.NombreMostrar, usuario.Roles)))
            .WithTags("Usuarios")
            .RequireAuthorization();

        // TAREA-25-1: usuarios y departamentos (Admin; el gestor recibe 403). Los roles siguen en Entra ID (App Roles).
        // Logs: SOLO Id (usuario, departamentos y Admin); nunca correos ni nombres.
        var admin = app.MapGroup("/api/admin/usuarios").WithTags("Usuarios").RequireAuthorization(Politicas.Admin);

        // Filtros: texto (correo o nombre), incluirInactivos (false), pagina, tamano (≤ 100). Sin SISTEMA; orden por correo.
        admin.MapGet("/", async Task<Results<Ok<PaginaResultado<UsuarioAdminDto>>, ValidationProblem>> (
            [AsParameters] UsuarioAdminListadoSolicitud solicitud, [FromServices] UsuariosAdminServicio servicio, CancellationToken ct) =>
        {
            var r = await servicio.ListarAsync(solicitud, ct);
            return r.EsValido ? TypedResults.Ok(r.Valor!) : TypedResults.ValidationProblem(r.Errores!, title: "Parámetros de consulta no válidos.");
        });

        admin.MapGet("/{id:int}", async Task<Results<Ok<UsuarioAdminDetalleDto>, NotFound>> (
            int id, [FromServices] UsuariosAdminServicio servicio, CancellationToken ct) =>
            await servicio.ObtenerDetalleAsync(id, ct) is { } detalle ? TypedResults.Ok(detalle) : TypedResults.NotFound());

        // 201 { id }; 400 en email/nombreMostrar; 409 si el correo ya existe (con usuarioId para abrirlo).
        admin.MapPost("/", async Task<Results<Created<UsuarioCreadoDto>, ValidationProblem, ProblemHttpResult>> (
            CrearUsuarioSolicitud solicitud, [FromServices] UsuariosAdminServicio servicio, IUsuarioActual usuario, ILoggerFactory registro,
            CancellationToken ct) =>
        {
            var r = await servicio.CrearAsync(solicitud, ct);
            switch (r.Estado)
            {
                case EstadoAdminUsuario.Ok:
                    Log(registro).LogInformation("Usuario {Id} dado de alta (admin {AdminId}).", r.Valor!.Id, usuario.UsuarioId);
                    return TypedResults.Created($"/api/admin/usuarios/{r.Valor.Id}", r.Valor);
                case EstadoAdminUsuario.Invalido:
                    return TypedResults.ValidationProblem(r.Errores!, title: "Los datos del usuario no son válidos.");
                default:
                    return TypedResults.Problem(title: UsuariosAdminServicio.MensajeEmailDuplicado, statusCode: StatusCodes.Status409Conflict,
                        extensions: r.UsuarioExistenteId is { } existente ? new Dictionary<string, object?> { ["usuarioId"] = existente } : null);
            }
        });

        // 200 con el usuario; 400; 404 (inexistente o SISTEMA); 409 si nombre o activo ya no son los que vio el Admin.
        admin.MapPut("/{id:int}", async Task<Results<Ok<UsuarioAdminDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
            int id, EditarUsuarioSolicitud solicitud, [FromServices] UsuariosAdminServicio servicio, IUsuarioActual usuario,
            ILoggerFactory registro, CancellationToken ct) =>
        {
            var r = await servicio.EditarAsync(id, solicitud, ct);
            if (r.Estado == EstadoAdminUsuario.Ok)
            {
                Log(registro).LogInformation("Usuario {Id} actualizado: activo {ActivoAnterior} → {Activo} (admin {AdminId}).",
                    id, solicitud.ActivoAnterior, solicitud.Activo, usuario.UsuarioId);
            }

            return Respuesta(r, UsuariosAdminServicio.MensajeConflictoUsuario, "Los datos del usuario no son válidos.");
        });

        // 200 { departamentos }; 400 en departamentoIds/anteriores; 404; 409 si el conjunto cambió; 503 si el bloqueo no llega.
        admin.MapPut("/{id:int}/departamentos", async Task<Results<Ok<DepartamentosAsignadosDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
            int id, AsignarDepartamentosSolicitud solicitud, [FromServices] UsuariosAdminServicio servicio, IUsuarioActual usuario,
            ILoggerFactory registro, CancellationToken ct) =>
        {
            var r = await servicio.AsignarDepartamentosAsync(id, solicitud, ct);
            if (r.Estado == EstadoAdminUsuario.Ok)
            {
                Log(registro).LogInformation("Departamentos del usuario {Id}: agregados [{Agregados}], retirados [{Retirados}] (admin {AdminId}).",
                    id, string.Join(", ", r.Agregados!), string.Join(", ", r.Retirados!), usuario.UsuarioId);
            }

            return Respuesta(r, UsuariosAdminServicio.MensajeConflictoDepartamentos, "Los departamentos no son válidos.");
        });

        return app;
    }

    private static ILogger Log(ILoggerFactory registro) => registro.CreateLogger(nameof(UsuarioEndpoints));

    private static Results<Ok<T>, ValidationProblem, NotFound, ProblemHttpResult> Respuesta<T>(
        ResultadoAdminUsuario<T> r, string conflicto, string tituloInvalido) => r.Estado switch
    {
        EstadoAdminUsuario.Ok => TypedResults.Ok(r.Valor!),
        EstadoAdminUsuario.Invalido => TypedResults.ValidationProblem(r.Errores!, title: tituloInvalido),
        EstadoAdminUsuario.NoEncontrado => TypedResults.NotFound(),
        _ => TypedResults.Problem(title: conflicto, statusCode: StatusCodes.Status409Conflict),
    };
}
