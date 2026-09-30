# ESTADO ACTUAL DEL PROYECTO — PROFESIOGRAMA (leer primero)

**Última actualización:** 2026-09-30 (TAREA-01: estructura inicial del repositorio con el Paso 1)

## 1. Documentos del proyecto (en orden)

| Doc | Contenido |
|---|---|
| `docs/fases/FASE_0_Inventario.md` | Inventario de archivos, faltantes, contradicciones C1–C14, respuestas y decisiones (sección 5). |
| `docs/fases/FASE_1_Analisis_Funcional.md` | Análisis funcional del módulo Proyectos: pantallas, reglas RN01–RN18, roles, problemas, hallazgos C15–C28. |
| `docs/fases/FASE_2_Modelo_Datos.md` | Mapeo lista → tabla, ER, justificación J1–J16, hallazgos C29–C31, respuestas (secc. 7), diagnóstico SQL (secc. 8) y entorno verificado (secc. 9). |
| `docs/fases/FASE_2_modelo_profesiograma.sql` | Script de referencia del modelo (22 tablas + 2 vistas + semillas). EF Core debe generar un esquema equivalente. |

## 2. Alcance acordado (primera entrega)

Solo módulo **Proyectos**: crear, control/listado, editar/estados, cronograma (+ reporte Excel), novedades (CRUD) y administración de permisos por departamento.
Fuera de alcance por ahora: Perfiles, EPP, Asistencia FOR SEI 11/12, AsignarPersonal, AST.

## 3. Decisiones clave (resumen)

- Pantalla vigente: `ConfigurarProyecto_1` (la otra se descarta).
- Fuente de datos para migrar: listas normalizadas (`SIG PROYECTOS` columnas planas, `SIG_ASIGNACIONES_PERSONAL`, `SIG_ASIGNACIONES_DETALLE` ≈5.000 filas, `SIG_HISTORIAL`, `SIG_HISTORIAL_ACTIVIDADES`, `NOVEDADES ASISTENCIA`). El JSON legado (`INF_GENERAL`, `ASIGNACIONES`) no se migra.
- Fila `666` de SIG PROYECTOS → tablas `Departamento` + `UsuarioDepartamento`.
- Empleados: API `POST https://backstack.sedemi.com:7048/api/EvolutionEmployee/EmployeesEvolution` (body: parameter, estado, codEmpresa, codDepartamento) → caché `Empleado` solo con activos (`estado = "A"`), **sin datos sensibles** (salario, BPR, nacimiento, teléfono, correo personal, dirección). `codPersona` (EKON) es único.
- Departamentos se comparan por **nombre** (no por código).
- Cargos Infor: tabla `CargoInfor` alimentada con los puestos de la API; el código Infor se asigna desde una interfaz.
- Visibilidad de proyectos: solo el propietario (parametrizable a DEPARTAMENTO en el futuro).
- Novedad GENERAL: solo administradores y usuarios autorizados.
- Novedades de la app PERMISO MEDICO: se sincronizan **manualmente** (botón de admin), solo lectura.
- Límites 20 principales / 20 backs, editables (`Parametro`).
- Fecha inicio, horario y almuerzo del proyecto pasan a ser editables.
- Fechas de negocio en `DATE`; auditoría en UTC; zona de negocio Ecuador (UTC-5).
- Correo: Microsoft Graph. Reporte Excel: ClosedXML en .NET. Servicios PDF externos se mantienen.
- Archivos a futuro: SharePoint (vía Graph), no file server.
- Frontend: React + Vite [PENDIENTE confirmar: el usuario mencionó "next 25"].

## 4. Entorno de desarrollo (verificado)

| Elemento | Valor |
|---|---|
| Repositorio | `D:\GitHub\profesiograma` (git, rama `main`): `backend/` (solución .NET), `frontend/` (React, Fase 6), `docs/` (estado, fases, origen, referencia, tareas) |
| Solución | `backend/Profesiograma.slnx`, .NET SDK 10.0.401 (`global.json`: 10.0.100 + `latestFeature`). Compilar desde `backend/`: `dotnet build Profesiograma.slnx` |
| Proyectos | `backend/src/App.Domain`, `App.Application`, `App.Infrastructure`, `App.Api` (+ `backend/sql/`, `backend/tests/` vacía) |
| Original | `D:\PROYECTOS\Profesiograma`: respaldo de **solo lectura** del Paso 1 (no modificar) |
| User-secrets | `UserSecretsId = profesiograma-api-4d2f7c1e` en `App.Api.csproj`; conservarlo para no perder la cadena de conexión |
| Implementado | `GET /api/health/db` (diagnóstico de conexión) con `IDatabaseDiagnostics` (Application) y `SqlDatabaseDiagnostics` (Infrastructure, Microsoft.Data.SqlClient) |
| Servidor SQL | `NIQUEL\SSDEV` — SQL Server 2019 Enterprise RTM (15.0.2190.7), compartido con ~42 bases de otros sistemas |
| Base | `PROFESIOGRAMA_DEV`, collation `Modern_Spanish_CI_AS`, compatibilidad 150 (vacía) |
| Login app | `profesiograma_dev` (db_owner solo de su base). Estándar futuro: `profesiograma_test`, `profesiograma_prod_app` + `profesiograma_prod_migrator` |
| Cadena de conexión | `dotnet user-secrets` → `ConnectionStrings:Profesiograma` (no está en el repo) |
| URL local | `https://localhost:7180` / `http://localhost:5180` |

## 5. Paso 2 — EF Core, DevAuth y datos de prueba (código integrado y compilando; migraciones pendientes: TAREA-03)

> Estado al 2026-09-30 (TAREA-02): el código del Paso 2 está integrado en `backend/src` y `dotnet build` termina con 0 advertencias y 0 errores. **Aún no hay migraciones** (TAREA-03). La API no se ha ejecutado contra la base.
> Paquetes: EF Core SqlServer 10.0.12 (Infrastructure), EF Core Design 10.0.12 y Microsoft.Identity.Web 4.15.0 (Api). Se subieron Microsoft.Data.SqlClient a 6.1.6 y Microsoft.Extensions.*.Abstractions a 10.0.12 (exigidos por EF 10.0.12).
> Integración: `AddInfrastructure` llama a `AddPersistencia`. `Program.cs` agrega la seguridad, los endpoints y el sembrador. `/` y `/api/health/db` son `AllowAnonymous`.
> Ajustes al paquete: el sembrador se omite si no hay migraciones en el ensamblado. Hay además una guarda explícita en `UsuarioActualMiddleware` (ver `docs/tareas/TAREA-02-reporte.md`).
> Respaldo del paquete original: `D:\temp\paso2_respaldo\` (solo lectura).

| Componente | Detalle |
|---|---|
| Entidades | 22 POCO en `App.Domain` (base `EntidadAuditable`, catálogos con `Id` byte, `CatalogoIds`). |
| Persistencia | `ProfesiogramaDbContext` + 16 configuraciones Fluent API en `App.Infrastructure/Persistencia/Configuraciones`. Nombres PK_/FK_/UQ_/UX_/IX_/CK_/DF_ iguales al script. `UseCompatibilityLevel(150)`. |
| Migraciones | [PENDIENTE: TAREA-03] `Inicial` (22 tablas + semillas `HasData`, fecha fija 2026-09-29 UTC) y `Vistas` (`vwProyectoResumen`, `vwNovedadDiaVigente` vía `VistasSql.*_V1`). Carpeta `Persistencia/Migraciones`. |
| Auditoría | `AuditoriaInterceptor`: CreadoPorId/FechaCreacion (Added, respeta valores asignados → Fase 3), ModificadoPorId/FechaModificacion (Modified), UTC truncado a segundos; sin usuario → SISTEMA (Id 1). |
| Seguridad | `Autenticacion:Modo` = `DevAuth` (solo Development; en otro entorno la API no arranca) o `EntraId` (Microsoft.Identity.Web, sección `AzureAd` [PENDIENTE App Registration]). FallbackPolicy autenticado; políticas `Politica.Admin` y `Politica.Gestor`. Encabezado `X-Dev-User: admin/gestor/anonimo`. `UsuarioActualMiddleware` provisiona `dbo.Usuario` por oid → email (caché 10 min). |
| Endpoints | `GET /api/health/db` (anónimo, solo Development), `GET /api/usuarios/me` (autenticado), `GET /api/catalogos` (Gestor), `GET /api/admin/parametros` (Admin). |
| Datos de prueba | `DatosPruebaSembrador` idempotente (Development + `DatosPrueba:SembrarAlIniciar=true`): 2 usuarios, compañía 9001, 8 empleados DEV001–DEV008, 3 proyectos PRY-DEV-0001..0003, 5 personal, 244 días, 5 etapas, 3 actividades, 4 novedades NOV-DEV-*, 10 días de novedad. |
| Verificación | `backend/sql/dev/verificacion_paso2.sql` (22 tablas, 2 vistas, 21 CHECK / 72 FK / 47 DEFAULT, conteos). |

Diferencias aceptadas frente a `FASE_2_modelo_profesiograma.sql`:
- `UQ_*` se implementan como índices únicos con el mismo nombre (no constraints), salvo `UQ_ProyectoPersonal_Clave` (clave alterna requerida por la FK compuesta desde `ProyectoAsignacionDia`; consecuencia: `ProyectoId`/`EmpleadoId` de `ProyectoPersonal` son inmutables en EF).
- Tabla adicional `__EFMigrationsHistory`.
- `FechaCreacion` de las semillas es fija (2026-09-29 00:00 UTC) para que el modelo sea determinista.
- Vistas creadas por migración SQL (no las modela EF).

Reset de la base de desarrollo: `dotnet ef database update 0` (NUNCA `database drop`: el login no puede crear bases).

## 6. Siguiente paso

Paso 3 (propuesto): Fase 4 (mapeo de flujos) y luego Fase 5 módulo Proyectos (crear/listar/editar/estados) sobre esta base.

**Decisión del usuario (2026-09-29):** la migración de datos (Fase 3, carga masiva desde SharePoint) se hace **al final**, justo antes del corte a producción. Mientras tanto se desarrolla con datos de prueba. "Migración de EF Core" (crear tablas) ≠ "migración de datos".

## 7. Pendientes abiertos

1. Propietario real de `PRY-20260831-e4776f`, `PRY-20260902-bbd352`, `PRY-20260911-5ceab4`: `gestor.sig@` o `gestor.administrativosig@` (C29).
2. Excel de cargos → código Infor (opcional; se puede cargar desde la interfaz).
3. Confirmar frontend React + Vite vs Next.js.
4. Plantilla Excel y Office Script del reporte de cronograma.
5. Zona horaria del sitio SharePoint (evidencia: Pacífico).
6. [Fase 5] El acceso a proyectos se decidirá por App Role `Gestor` o por `UsuarioDepartamento` (Fase 1 indica `UsuarioDepartamento`).
7. [Fase 7] App Registrations (API + SPA) con App Roles `Admin` y `Gestor`; al activar Entra ID marcar `/api/health/db` como `AllowAnonymous`.
8. Inconsistencia menor de documentación: FASE_2 secc. 8 menciona el login `profesiograma_app`; el vigente es `profesiograma_dev`.
9. Decidir si `docs/origen/powerapps`, FASE_0 y el script SQL (contienen correos @sedemi.com) pueden subirse al remoto. Hasta decidirlo: commits locales, sin push.
