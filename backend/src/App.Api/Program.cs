using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application;
using App.Infrastructure;
using App.Infrastructure.Erp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddErroresApi();                // ERP caído / registro ocupado / base caída → 503; cuerpo ilegible → 400
builder.AddSeguridadProfesiograma();             // DevAuth / Entra ID + políticas Admin/Gestor

var app = builder.Build();
app.Logger.LogInformation("ERP: modo {Modo}", ErpServiceCollectionExtensions.ModoEfectivo(builder.Configuration)); // TAREA-26a

app.UseEncabezadosSeguridad();                   // TAREA-7d-3: PRIMER middleware (nosniff, Referrer, X-Frame, frame-ancestors; HSTS por config)
app.UseExceptionHandler();
app.UseSpaEstaticos();                          // TAREA-7d-1: wwwroot ANTES de la seguridad (los archivos son anónimos)
app.UseRouting();                                // TAREA-7d-3: explícito, DESPUÉS de los archivos (ver SpaExtensions.MapSpa)
app.UseSeguridadProfesiograma();                 // UseAuthentication → UsuarioActualMiddleware → UseAuthorization

app.MapHealthEndpoints();                        // se mantiene /api/health/db (solo Development)
app.MapSaludEndpoints();                         // TAREA-7d-1: /api/salud/vivo y /api/salud/listo, anónimos
app.MapUsuarioEndpoints();
app.MapCatalogoEndpoints();
app.MapProyectoEndpoints();
app.MapErpEndpoints();
app.MapCronogramaEndpoints(); // TAREA-20
app.MapEmpleadoEndpoints();
app.MapCargoInforEndpoints(); // TAREA-25-3-1
app.MapNovedadEndpoints(); // TAREA-23-1
app.MapSpa();                                    // TAREA-7d-1: /api/** desconocido → 404; el resto → index.html

await app.SembrarDatosPruebaAsync();             // solo Development + DatosPrueba:SembrarAlIniciar

app.Run();
