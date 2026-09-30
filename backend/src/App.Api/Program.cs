using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.AddSeguridadProfesiograma();             // DevAuth / Entra ID + políticas Admin/Gestor

var app = builder.Build();

app.UseExceptionHandler();
app.UseSeguridadProfesiograma();                 // UseAuthentication → UsuarioActualMiddleware → UseAuthorization

app.MapGet("/", () => Results.Ok(new { app = "PROFESIOGRAMA API", entorno = app.Environment.EnvironmentName }))
    .AllowAnonymous();
app.MapHealthEndpoints();                        // se mantiene /api/health/db
app.MapUsuarioEndpoints();
app.MapCatalogoEndpoints();

await app.SembrarDatosPruebaAsync();             // solo Development + DatosPrueba:SembrarAlIniciar

app.Run();
