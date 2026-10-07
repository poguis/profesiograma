using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application;
using App.Infrastructure;
using App.Infrastructure.Erp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddExceptionHandler<ErpNoDisponibleExceptionHandler>(); // ERP caído → 503
builder.Services.AddExceptionHandler<RegistroOcupadoExceptionHandler>(); // applock de asignaciones no obtenido → 503
builder.Services.AddProblemDetails();
builder.AddSeguridadProfesiograma();             // DevAuth / Entra ID + políticas Admin/Gestor

var app = builder.Build();
app.Logger.LogInformation("ERP: modo {Modo}", ErpServiceCollectionExtensions.ModoEfectivo(builder.Configuration)); // TAREA-26a

app.UseExceptionHandler();
app.UseSeguridadProfesiograma();                 // UseAuthentication → UsuarioActualMiddleware → UseAuthorization

app.MapGet("/", () => Results.Ok(new { app = "PROFESIOGRAMA API", entorno = app.Environment.EnvironmentName }))
    .AllowAnonymous();
app.MapHealthEndpoints();                        // se mantiene /api/health/db
app.MapUsuarioEndpoints();
app.MapCatalogoEndpoints();
app.MapProyectoEndpoints();
app.MapErpEndpoints();
app.MapEmpleadoEndpoints();

await app.SembrarDatosPruebaAsync();             // solo Development + DatosPrueba:SembrarAlIniciar

app.Run();
