# PROFESIOGRAMA — Instrucciones para Claude Code

## Contexto
Rehacer la app Power Apps "PROFESIOGRAMA" (SEDEMI) como aplicación web.
Solo se migran los DATOS; la app se rediseña. Alcance actual: módulo Proyectos.
Antes de cualquier tarea, leer `docs/00_ESTADO_ACTUAL.md` y los documentos de `docs/fases/` que apliquen
(`FASE_0_Inventario.md`, `FASE_1_Analisis_Funcional.md`, `FASE_2_Modelo_Datos.md`, `FASE_2_modelo_profesiograma.sql`).

## Stack (no proponer alternativas)
- Backend: ASP.NET Core Web API .NET 10, C#, EF Core code-first (`backend/`).
- BD: SQL Server 2019 on-premise, compatibilidad 150.
- Frontend (futuro): React + TypeScript + Vite + @azure/msal-react (`frontend/`).
- Auth: Entra ID single-tenant (Microsoft.Identity.Web). En Development: DevAuth (`X-Dev-User: admin|gestor|anonimo`).
- Capas: Domain → Application → Infrastructure → Api. Sin lógica de negocio en endpoints.

## Reglas de trabajo
1. Idioma: español (Ecuador) en respuestas, reportes y comentarios de código.
2. Trabajar SOLO la tarea indicada. Al terminar, detenerse. No avanzar a la siguiente.
3. Antes de modificar archivos, presentar un plan breve y esperar aprobación.
4. No inventar datos. Si algo falta, marcarlo [PENDIENTE DE CONFIRMAR] y preguntar.
5. Reportar contradicciones entre documentos y código.
6. Al terminar, escribir `docs/tareas/TAREA-XX-reporte.md` con: qué se hizo, archivos tocados,
   comandos ejecutados con su resultado, errores y cómo se resolvieron, pendientes.
7. Git: prohibido ejecutar git add, commit, push, reset, checkout o cualquier comando que modifique el repositorio.
   Solo se permiten comandos de lectura (git status, git diff, git log, git check-ignore). El usuario hace los commits.

## Base de datos — PROHIBIDO
- Servidor NIQUEL\SSDEV es COMPARTIDO: trabajar solo en PROFESIOGRAMA_DEV.
- NUNCA `dotnet ef database drop` (el login no puede crear bases). Para reiniciar: `dotnet ef database update 0`.
- No mostrar ni escribir la cadena de conexión: vive en `dotnet user-secrets` (ConnectionStrings:Profesiograma).
- No ejecutar SQL que modifique datos sin aprobación explícita.

## Datos sensibles
- `docs/origen/sharepoint/muestras/` contiene datos personales: solo lectura, nunca copiar valores a código, semillas ni reportes.
- La caché `Empleado` no guarda salario, BPR, fecha de nacimiento, teléfono, correo personal ni dirección.

## Archivos de referencia
- `docs/referencia/` contiene archivos para FUSIONAR a mano (Program.cs, appsettings.json). No copiarlos tal cual.

## Compilar (desde backend/)
dotnet build Profesiograma.slnx
dotnet test --solution Profesiograma.slnx -c Release   (pruebas: xUnit v3 sobre Microsoft Testing Platform, activado en global.json)

- La API suele estar corriendo (la ejecuta el usuario) y bloquea bin\Debug.
  Para verificar compilación usa: dotnet build Profesiograma.slnx -c Release
  Para comandos EF agrega: --configuration Release
  Nunca detengas la API; si se requiere Debug, pide al usuario que la detenga.

- User-secrets: `App.Api.csproj` tiene `UserSecretsId = profesiograma-api-4d2f7c1e`. No cambiarlo ni regenerarlo:
  la cadena de conexión está asociada a ese Id. Verificar solo la clave: `dotnet user-secrets list --project src/App.Api` (sin mostrar el valor).

## Reglas de pruebas
- Toda consulta EF nueva o modificada debe tener una prueba de traducción con ToQueryString() en App.Infrastructure.Tests.
  Las pruebas con dobles no detectan consultas que EF no puede traducir a SQL.
  (Patrón: la consulta se expone como método `internal static IQueryable<…>` y se prueba con la base `Consultas/BaseSql.cs`.)

## Reglas de seguridad para pruebas
- Nunca ejecutar scripts, curl, Invoke-RestMethod ni ningún cliente HTTP contra la API, ni siquiera "simulando"
  respuestas. Los scripts de pruebas manuales se escriben, no se ejecutan (los ejecuta el usuario).
- Para probar la lógica de un script sin la API, usar un modo de simulación explícito que nunca invoque curl
  (p. ej. variable `SIMULAR=1` que lea las respuestas de archivos) o una URL base inválida (`https://localhost:1`).
  Nunca depender del orden de búsqueda de ejecutables (en este equipo `NoDefaultCurrentDirectoryInExePath=1`:
  un `curl.cmd` en la carpeta actual NO reemplaza a `curl.exe`).
- Si una prueba local pudo haber tocado la API real, detenerse y reportarlo de inmediato.
  (Origen: incidente de la TAREA-17, 05/10/2026, ver `docs/tareas/TAREA-17-reporte.md` §6.5.)

## Frontend (desde frontend/)
npm install
npm run dev        (lo ejecuta el usuario; no ejecutarlo)
npm run build      (tsc -b + vite build: verificación de tipos y compilación)
npm run lint       (oxlint)
npm test           (Vitest 5.0.3: lógica pura del frontend, p. ej. src/features/proyectos/formularioProyecto*.test.ts)

- X-Dev-User solo existe en desarrollo: se envía únicamente si import.meta.env.DEV y VITE_AUTH_MODE=dev.
  Nunca debe enviarse en producción. La identidad se obtiene solo en src/auth/ (Fase 7: MSAL).
- Las llamadas usan "/api" (proxy de Vite → https://localhost:7180). No configurar CORS en la API.

## Comandos EF (ejecutar desde backend/)
La primera vez en un equipo: `dotnet tool restore` (desde backend/).
dotnet ef migrations add <Nombre> --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --output-dir Persistencia/Migraciones
dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext
