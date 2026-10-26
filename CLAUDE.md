# PROFESIOGRAMA — Instrucciones para Claude Code

## Contexto
Rehacer la app Power Apps "PROFESIOGRAMA" (SEDEMI) como aplicación web. Solo se migran los DATOS; la app se rediseña.
Antes de cualquier tarea, leer `docs/00_ESTADO_ACTUAL.md` y SOLO los documentos que su «Índice de documentos» indique para la
tarea. `docs/fases/FASE_2_modelo_profesiograma.sql` solo si la tarea toca el modelo. `docs/HISTORIAL.md` solo si la tarea lo pide.

## Stack (no proponer alternativas)
- Backend: ASP.NET Core Web API .NET 10, C#, EF Core code-first (`backend/`). BD: SQL Server 2019 on-premise, compatibilidad 150.
- Frontend: React + TypeScript + Vite (`frontend/`); MSAL en la Fase 7.
- Auth: Entra ID single-tenant (Microsoft.Identity.Web). En Development: DevAuth (`X-Dev-User: admin|gestor|anonimo`).
- Capas: Domain → Application → Infrastructure → Api. Sin lógica de negocio en endpoints.

## Reglas de trabajo
1. Idioma: español (Ecuador) en respuestas, reportes y comentarios de código.
2. Flujo por fases: **Fase A** = plan de solo lectura, entregado en la conversación; **Fase B** = implementación del plan aprobado
   (reportar toda desviación). Trabajar SOLO la tarea indicada y detenerse al terminar.
3. Antes de modificar archivos, presentar un plan breve y esperar aprobación.
4. No inventar datos: marcar [PENDIENTE DE CONFIRMAR] o [SUPUESTO] y preguntar. Reportar contradicciones entre documentos y código.
5. Reporte al terminar: `docs/tareas/TAREA-XX-reporte.md` según `docs/tareas/PLANTILLA_REPORTE.md` (máximo 120 líneas,
   verificación en tabla, sin repetir el plan).
6. Git: prohibido git add, commit, push, reset, checkout o cualquier comando que modifique el repositorio. Solo lectura
   (git status, git diff, git log, git check-ignore). El usuario hace los commits.
7. Búsquedas: usar las herramientas Grep y Glob, nunca `grep -r` ni `find` en Bash (recorren node_modules, bin y obj).

## Base de datos — PROHIBIDO
- Servidor NIQUEL\SSDEV es COMPARTIDO: trabajar solo en PROFESIOGRAMA_DEV.
- NUNCA `dotnet ef database drop` (el login no puede crear bases). Para reiniciar: `dotnet ef database update 0`.
- No mostrar ni escribir la cadena de conexión: vive en `dotnet user-secrets` (ConnectionStrings:Profesiograma).
- No ejecutar SQL que modifique datos sin aprobación explícita.

## Datos sensibles
- `docs/origen/sharepoint/muestras/` contiene datos personales: no se lee en las tareas; nunca copiar valores a código, semillas ni reportes.
- La caché `Empleado` no guarda salario, BPR, fecha de nacimiento, teléfono, correo personal ni dirección.
- La cédula (D15) se guarda SOLO en `Empleado.Cedula`: nunca en un DTO, la caché de empleados, logs, mensajes de excepción ni
  respuestas de la API (salvo el reporte Excel). Viaja aparte en `CedulasEmpleados`, no en los `record` (su ToString la
  imprimiría). En pruebas y simulación, solo cédulas ficticias.
- Los CSV exportados de la lista NOVEDADES ASISTENCIA contienen permisos médicos (datos de salud): nunca se agregan al repo
  ni se leen en las tareas; su ruta va solo en user-secrets (`NovedadesExternas:RutaArchivo`).
- `docs/referencia/` contiene archivos para FUSIONAR a mano (Program.cs, appsettings.json). No copiarlos tal cual.

## API y pruebas manuales — PROHIBIDO
- Claude Code **nunca** llama a la API (curl, Invoke-RestMethod ni ningún cliente HTTP, ni siquiera «simulando»), **nunca**
  ejecuta los scripts de `backend/tests/manual/` (se escriben; los ejecuta el usuario) y **nunca** lee `resultado-*.txt`,
  `crear-proyecto-*.json` ni otros archivos que el usuario haya generado o completado (solo fecha y tamaño).
- Si una prueba local pudo haber tocado la API real, detenerse y reportarlo de inmediato.
- El detalle (estructura, simulación, banner, formato CONTROL) está en `.claude/rules/pruebas-manuales.md`.

## Backend (desde backend/)
- `dotnet build Profesiograma.slnx -c Release` y `dotnet test --solution Profesiograma.slnx -c Release` (xUnit v3 sobre
  Microsoft Testing Platform). La API suele estar corriendo (la ejecuta el usuario) y bloquea bin\Debug: usar Release
  (también `--configuration Release` en los comandos EF). Nunca detener la API; si se requiere Debug, pedirlo al usuario.
- User-secrets: `UserSecretsId = profesiograma-api-4d2f7c1e` en `App.Api.csproj`; no cambiarlo ni regenerarlo. Verificar solo
  la clave: `dotnet user-secrets list --project src/App.Api` (sin mostrar el valor).
- Toda consulta EF nueva o modificada lleva una prueba de traducción con ToQueryString() en App.Infrastructure.Tests (la
  consulta se expone como `internal static IQueryable<…>` y se prueba con la base `Consultas/BaseSql.cs`).
- EF (la primera vez: `dotnet tool restore`):
  `dotnet ef migrations add <Nombre> --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --output-dir Persistencia/Migraciones`
  `dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext`

## Frontend (desde frontend/)
- `npm run build` (tsc -b + vite build), `npm run lint` (oxlint), `npm test` (Vitest 5.0.3, lógica pura). `npm run dev` lo
  ejecuta el usuario.
- X-Dev-User solo se envía si `import.meta.env.DEV` y `VITE_AUTH_MODE=dev`; nunca en producción. La identidad se obtiene solo
  en `src/auth/`. Las llamadas usan "/api" (proxy de Vite → https://localhost:7180); no configurar CORS en la API.
