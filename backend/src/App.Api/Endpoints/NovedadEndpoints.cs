using App.Application.Comun;
using App.Application.Novedades;
using App.Application.Novedades.Sincronizacion;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

/// <summary>Novedades (TAREA-23-1): listado, opciones, detalle y creación. Editar y anular: 23-2.</summary>
public static class NovedadEndpoints
{
    public static IEndpointRouteBuilder MapNovedadEndpoints(this IEndpointRouteBuilder app)
    {
        var novedades = app.MapGroup("/api/novedades")
            .WithTags("Novedades")
            .RequireAuthorization(Politicas.Gestor);

        // Filtros: aplicacion, tipo, origen, texto, desde, hasta (solapamiento), incluirAnuladas, pagina, tamano (≤ 100).
        novedades.MapGet("/", async Task<Results<Ok<PaginaResultado<NovedadResumenDto>>, ValidationProblem>> (
            [AsParameters] NovedadListadoSolicitud solicitud, NovedadConsultaServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.ListarAsync(solicitud, ct);
            return resultado.EsValido
                ? TypedResults.Ok(resultado.Valor!)
                : TypedResults.ValidationProblem(resultado.Errores!, title: "Parámetros de consulta no válidos.");
        });

        novedades.MapGet("/opciones-formulario", async (OpcionesFormularioNovedad servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.ObtenerAsync(ct)));

        // Inexistente o no visible → 404 (nunca 403).
        novedades.MapGet("/{id:int}", async Task<Results<Ok<NovedadDetalleDto>, NotFound>> (
            int id, NovedadConsultaServicio servicio, CancellationToken ct) =>
            await servicio.ObtenerDetalleAsync(id, ct) is { } detalle ? TypedResults.Ok(detalle) : TypedResults.NotFound());

        // 201 + Location; 400 por campo; 403 GENERAL sin permiso; 409 duplicado (N7) o alta concurrente, con tipoConflicto (TAREA-27); 503 empleados,
        // registro ocupado o base caída (manejadores comunes).
        novedades.MapPost("/", async Task<Results<Created<NovedadCreadaDto>, ValidationProblem, ProblemHttpResult>> (
            CrearNovedadSolicitud solicitud, NovedadEscrituraServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.CrearAsync(solicitud, ct);
            return resultado.Estado switch
            {
                EstadoCrearNovedad.Creado => TypedResults.Created($"/api/novedades/{resultado.Creado!.Id}", resultado.Creado),
                EstadoCrearNovedad.Invalido => TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos de la novedad no son válidos."),
                EstadoCrearNovedad.Prohibido => TypedResults.Problem(
                    title: ResultadoCrearNovedad.MensajeProhibido, statusCode: StatusCodes.Status403Forbidden),
                // R6: el código de la existente solo si quien registra la ve.
                EstadoCrearNovedad.Duplicado => Duplicado(resultado.CodigoDuplicado),
                _ => ProblemasConflicto.Conflicto(ResultadoCrearNovedad.MensajeCambiado, ProblemasConflicto.Tipos.RegistroConcurrente),
            };
        });

        // TAREA-23-3: sincronización manual de PERMISO MEDICO (Admin, sin cuerpo). escenario solo con la fuente Simulada.
        // Respuesta y log: SOLO conteos y códigos NOV (nunca nombres, correos, cédulas, el JSON ni la ruta del archivo).
        app.MapPost("/api/admin/novedades/sincronizar-permisos-medicos", async Task<Results<Ok<ResultadoSincronizacionDto>, ValidationProblem>> (
                string? escenario, SincronizacionPermisosMedicosServicio servicio, IUsuarioActual usuario, ILoggerFactory registro,
                CancellationToken ct) =>
            {
                var log = registro.CreateLogger(nameof(NovedadEndpoints));
                var cronometro = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var r = await servicio.SincronizarAsync(escenario, ct);
                    if (!r.EsValido)
                    {
                        return TypedResults.ValidationProblem(r.Errores!, title: "Parámetros de la sincronización no válidos.");
                    }

                    var v = r.Valor!;
                    log.LogInformation(
                        "Sincronización PERMISO MEDICO (usuario {UsuarioId}, fuente {Fuente}, {Ms} ms): leídas {Leidas}, nuevas {Nuevas}, actualizadas {Actualizadas}, anuladas {Anuladas}, reactivadas {Reactivadas}, sin cambios {SinCambios}, ausentes en el origen {Ausentes}, omitidas {Omitidas}; códigos omitidos: {Codigos}.",
                        usuario.UsuarioId, v.Fuente, cronometro.ElapsedMilliseconds, v.Leidas, v.Nuevas, v.Actualizadas, v.Anuladas,
                        v.Reactivadas, v.SinCambios, v.AusentesEnOrigen, Conteos(v.Omitidas), string.Join(", ", v.CodigosOmitidos));
                    return TypedResults.Ok(v);
                }
                catch (Exception ex) when (servicio.Parcial is { } p)
                {
                    // Un lote falló: los anteriores quedaron confirmados; repetir la sincronización completa el resto.
                    log.LogWarning(
                        "Sincronización PERMISO MEDICO incompleta (usuario {UsuarioId}, {Ms} ms, {Error}): hasta el último lote confirmado, nuevas {Nuevas}, actualizadas {Actualizadas}, anuladas {Anuladas}, reactivadas {Reactivadas}.",
                        usuario.UsuarioId, cronometro.ElapsedMilliseconds, ex.GetType().Name, p.Nuevas, p.Actualizadas, p.Anuladas, p.Reactivadas);
                    throw;
                }
            })
            .WithTags("Novedades")
            .RequireAuthorization(Politicas.Admin);

        // TAREA-23-2: editar (N8) y anular (N10). 200 { id, version }; 400 (version, origen, campos, general); 403; 404;
        // 409 con tipoConflicto anulada, cambio o duplicado (TAREA-27); 503 (registro ocupado o base caída).
        novedades.MapPut("/{id:int}", async Task<Results<Ok<NovedadEditadaDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
            int id, EditarNovedadSolicitud solicitud, NovedadModificacionServicio servicio, CancellationToken ct) =>
            Respuesta(await servicio.EditarAsync(id, solicitud, ct), "Los datos de la novedad no son válidos."));

        novedades.MapPost("/{id:int}/anular", async Task<Results<Ok<NovedadEditadaDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
            int id, AnularNovedadSolicitud solicitud, NovedadModificacionServicio servicio, CancellationToken ct) =>
            Respuesta(await servicio.AnularAsync(id, solicitud, ct), "Los datos de la anulación no son válidos."));

        return app;
    }

    private static Results<Ok<NovedadEditadaDto>, ValidationProblem, NotFound, ProblemHttpResult> Respuesta(
        ResultadoModificacionNovedad r, string tituloInvalido) => r.Estado switch
    {
        EstadoModificacionNovedad.Hecho => TypedResults.Ok(r.Hecho!),
        EstadoModificacionNovedad.Invalido => TypedResults.ValidationProblem(r.Errores!, title: tituloInvalido),
        EstadoModificacionNovedad.NoEncontrada => TypedResults.NotFound(),
        EstadoModificacionNovedad.Prohibido => TypedResults.Problem(title: PermisosNovedad.MensajeSinPermiso, statusCode: StatusCodes.Status403Forbidden),
        EstadoModificacionNovedad.Duplicado => Duplicado(r.CodigoDuplicado),
        _ => ProblemasConflicto.Conflicto(r.MensajeConflicto!, r.TipoConflicto == TipoConflictoNovedad.Anulada
            ? ProblemasConflicto.Tipos.Anulada
            : ProblemasConflicto.Tipos.Cambio),
    };

    /// <summary>N7: 409 «duplicado»; <c>codigo</c> solo si la existente es visible (R6).</summary>
    private static ProblemHttpResult Duplicado(string? codigo) => ProblemasConflicto.Conflicto(
        ResultadoCrearNovedad.MensajeDuplicado, ProblemasConflicto.Tipos.Duplicado,
        codigo is null ? null : new Dictionary<string, object?> { ["codigo"] = codigo });

    private static string Conteos(IReadOnlyDictionary<string, int> omitidas) =>
        omitidas.Count == 0 ? "0" : string.Join(", ", omitidas.OrderBy(o => o.Key, StringComparer.Ordinal).Select(o => $"{o.Key} {o.Value}"));
}
