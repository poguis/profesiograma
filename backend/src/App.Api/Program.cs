using App.Api.Endpoints;
using App.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => Results.Ok(new { app = "PROFESIOGRAMA API", entorno = app.Environment.EnvironmentName }));
app.MapHealthEndpoints();

app.Run();
