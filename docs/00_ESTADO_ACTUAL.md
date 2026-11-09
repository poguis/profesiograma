# ESTADO ACTUAL DEL PROYECTO — PROFESIOGRAMA (leer primero)

**Última actualización:** 2026-11-09 (TAREA-8). Solo el estado **vigente** (TAREA-X1-B); lo cerrado está en [`docs/HISTORIAL.md`](HISTORIAL.md): **no leerlo salvo que la tarea lo pida**.

| Área | Estado al 06/11/2026 |
|---|---|
| Proyectos (backend y frontend) | Crear, listado, detalle, estados, edición, reactivación, cronograma y reporte Excel ✅ (TAREA-07 a 22c) |
| Empleados | Desde la API EvolutionEmployee con caché en memoria (opción C) ✅ (26a, 26d, 26c) |
| Novedades, backend | 23-1, 23-2 y 23-3 ✅ (N13 cerrado); 23-4 (Graph) bloqueada por los pendientes 7 y 63 |
| Novedades, frontend | 24a–24d ✅ verificadas por el usuario el 26/10/2026 (24d: S1–S6; 349/349). Sigue la 24e (opcional, B0) |
| Criterio de personal (TAREA-62) | ✅ Cerrada: 62-1 (backend) y 62-2 (frontend) verificadas por el usuario el 28/10/2026 (62-2: A1–A11; 362/362) |
| Usuarios y departamentos (TAREA-25) | ✅ **Completa** (25-1, 25-2, 25-3-1 y 25-3-2), verificada el 29/10/2026. Filas conservadas: Id 4 y 7 de prueba (inactivos), Id 5 gestor2; `CargoInfor` con 499 filas |
| Entra ID en DEV (Fase 7b y 7b-2) | ✅ Verificadas el 05/11/2026 (M1–M7; backend 1196/1196, frontend 412/412) |
| QA (7d-1, 7d-3 y 7d-4) | ✅ Verificadas el 06/11/2026: `https://sdmapptest2.sedemi.local:7083` con inicio de sesión (Admin y Gestor) y `ssoSilent` en pestaña nueva, humo H1–H14 y F1–F4 (1221/1221, 430/430) |
| Aprendizaje del usuario | Objetivo aparte del proyecto: Entra ID, entornos y bases de datos, con explicaciones paso a paso |
| **Siguiente tarea** | Ajustes del usuario (visuales o lógicos, menores); después: Lote 2 (83, 77) antes de PRO y Fase 3 |

## 1. Índice de documentos

| Documento | Cuándo leerlo | Secciones |
|---|---|---|
| `docs/00_ESTADO_ACTUAL.md` (este) | Siempre | Todo |
| `docs/fases/FASE_0_Inventario.md` | Migración de datos (Fase 3) o dudas sobre el origen | Contradicciones C1–C14 y decisiones (§5) |
| `docs/fases/FASE_1_Analisis_Funcional.md` | Pantalla o regla funcional nueva del módulo Proyectos; pendiente 62 | Pantallas, RN01–RN18, roles; §2.7 (novedades del original) |
| `docs/fases/FASE_2_Modelo_Datos.md` | Cambio de modelo, migración EF o Fase 3 | Mapeo lista → tabla, J1–J16, §8–§9 |
| `docs/fases/FASE_2_modelo_profesiograma.sql` | **Solo** si la tarea toca el modelo (tablas, vistas, semillas) | — |
| `docs/fases/FASE_4_Empleados_API.md` | Empleados, buscador, ERP, alta puntual, criterio de personal | §4–§5, §10 |
| `docs/fases/FASE_5_Crear_Proyecto.md` | Crear proyecto, motor de cronograma, cruces | P1–P6 |
| `docs/fases/FASE_5_Estados_Proyecto.md` | Suspensión, cierre, reactivación | E1–E6; §8 |
| `docs/fases/FASE_5_Edicion_Cronograma.md` | Fecha de corte, actualización de personal | R1–R8; §8 |
| `docs/fases/FASE_5_Edicion_Cabecera.md` | Fechas, horario, almuerzo, cambio de actividad | C1–C11 |
| `docs/fases/FASE_5_Cronograma.md` | Cronograma y reporte Excel | Contrato, D1–D16; §7–§8 |
| `docs/fases/FASE_5_Novedades.md` | Novedades (backend o frontend) | §1–§2 contratos y validación; §7 edición; §8 sincronización; §9 frontend |
| `docs/fases/FASE_7_Entornos_Despliegue.md` | Entornos, bases, logins, migraciones, respaldos, IIS, Entra ID (Fase 7) | §1–§9 |
| `docs/fases/FASE_5_Usuarios_Departamentos.md` | Usuarios y departamentos (TAREA-25), administración; cargos Infor | §2–§3 contratos y reglas; §5 efecto de los departamentos; §10 cargos Infor |
| `docs/tareas/TAREA-XX-reporte.md` | Solo el reporte de la tarea que se continúa | — |
| `docs/tareas/PLANTILLA_REPORTE.md` | Al escribir un reporte | — |
| `docs/HISTORIAL.md` | **No leer** salvo que la tarea lo pida (trazabilidad, cifras de pruebas antiguas) | Índice al inicio |

| Tipo de tarea | Leer además de este documento |
|---|---|
| Novedades, frontend (24b–24e) | `FASE_5_Novedades.md` §1, §2, §7 y §9; el reporte de la subtarea anterior |
| Novedades, backend (23-4, B0) | `FASE_5_Novedades.md` §1–§8; pendientes 7, 63 y 66 |
| Frontend de proyectos | El FASE_5 de la pantalla y su último reporte |
| Cronograma o reporte Excel | `FASE_5_Cronograma.md` |
| Criterio de personal (TAREA-62) | `FASE_4_Empleados_API.md` §10 y §5, `FASE_5_Novedades.md` §2 |
| Usuarios y departamentos (TAREA-25) | `FASE_5_Usuarios_Departamentos.md`; el reporte de la subtarea anterior |
| Migración de datos (Fase 3) | `FASE_0`, `FASE_2` y el `.sql`; pendientes 27 y 45 |
| Entra ID, entornos y despliegue (Fase 7) | `FASE_7_Entornos_Despliegue.md`; §4 de este documento; pendientes 7, 59, 63, 65 y 75 |

## 2. Alcance acordado (primera entrega)

Solo módulo **Proyectos**: crear, control/listado, editar/estados, cronograma (+ reporte Excel), novedades (CRUD) y administración de permisos por departamento.
Fuera de alcance por ahora: Perfiles, EPP, Asistencia FOR SEI 11/12, AsignarPersonal, AST.

## 3. Decisiones clave (resumen)

- Pantalla vigente: `ConfigurarProyecto_1` (la otra se descarta).
- Fuente de datos para migrar: listas normalizadas (`SIG PROYECTOS` columnas planas, `SIG_ASIGNACIONES_PERSONAL`, `SIG_ASIGNACIONES_DETALLE` ≈5.000 filas, `SIG_HISTORIAL`, `SIG_HISTORIAL_ACTIVIDADES`, `NOVEDADES ASISTENCIA`). El JSON legado (`INF_GENERAL`, `ASIGNACIONES`) no se migra.
- Fila `666` de SIG PROYECTOS → tablas `Departamento` + `UsuarioDepartamento`.
- Empleados: **la API `POST https://backstack.sedemi.com:7048/api/EvolutionEmployee/EmployeesEvolution` es la ÚNICA fuente** (decisión del usuario del 07/10/2026, opción C; reemplaza a la caché sincronizada de la Fase 2 y a la TAREA-26b, descartada). Sin sincronización ni tarea programada: el buscador lee la API con caché EN MEMORIA (10 min); la tabla `Empleado` contiene SOLO a las personas asignadas, con una "alta puntual" desde la API al asignarlas (crear proyecto, actualizar personal, reactivar). Solo activos (`estado = "A"`), **sin datos sensibles**. Departamentos por **nombre**. Ver `docs/fases/FASE_4_Empleados_API.md`.
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
- Frontend: React + Vite (decidido 2026-09-30). UI Fluent UI React v9, TanStack Query, React Router (TAREA-08).

## 4. Entorno de desarrollo (verificado)

| Elemento | Valor |
|---|---|
| Repositorio | `D:\GitHub\profesiograma` (git, rama `main`): `backend/` (solución .NET), `frontend/` (React, Fase 6), `docs/` (estado, fases, origen, referencia, tareas) |
| Solución | `backend/Profesiograma.slnx`, .NET SDK 10.0.401 (`global.json`: 10.0.100 + `latestFeature`). Compilar desde `backend/`: `dotnet build Profesiograma.slnx` |
| Proyectos | `backend/src/App.Domain`, `App.Application`, `App.Infrastructure`, `App.Api` (+ `backend/sql/`, `backend/tests/`) |
| Original | `D:\PROYECTOS\Profesiograma`: respaldo de **solo lectura** del Paso 1 (no modificar) |
| User-secrets | `UserSecretsId = profesiograma-api-4d2f7c1e` en `App.Api.csproj`; conservarlo para no perder la cadena de conexión |
| Implementado | `GET /api/health/db` (diagnóstico de conexión) con `IDatabaseDiagnostics` (Application) y `SqlDatabaseDiagnostics` (Infrastructure, Microsoft.Data.SqlClient) |
| Servidor SQL | `NIQUEL\SSDEV` — SQL Server 2019 Enterprise RTM (15.0.2190.7), compartido con ~42 bases de otros sistemas |
| Base | `PROFESIOGRAMA_DEV` (recuperación SIMPLE desde el 29/10/2026), collation `Modern_Spanish_CI_AS`, compatibilidad 150 (esquema creado por migraciones EF en la TAREA-04; sin datos de prueba aún) |
| Respaldos | Manuales con COPY_ONLY en `D:\BACKUP` del servidor, antes de cada cambio de esquema; DEV no tenía respaldos de TI |
| Accesos del usuario | sysadmin (login `sa`) en NIQUEL\SSDEV; RDP a SDMAPPTEST2 (IIS de QA) |
| Login app | `profesiograma_dev` (db_owner solo de su base). Estándar futuro: `profesiograma_test`, `profesiograma_prod_app` + `profesiograma_prod_migrator` |
| Cadena de conexión | `dotnet user-secrets` → `ConnectionStrings:Profesiograma` (no está en el repo) |
| ERP | `ServiciosExternos:Modo`: `Http` (`https://backstack.sedemi.com:7048` / `:7055`, TLS válido, sin autenticación). Development usa el ERP real (`Http` por user-secrets, TAREA-26a; `appsettings.Development.json` dice `Simulado`). Ver `backend/tests/manual/tarea26a/LEEME.md` |
| Pruebas | `dotnet test --solution Profesiograma.slnx -c Release` (xUnit v3 sobre Microsoft Testing Platform): App.Domain.Tests, App.Application.Tests, App.Infrastructure.Tests, App.Api.Tests |
| URL local | `https://localhost:7180` / `http://localhost:5180` |
| QA | Sitio IIS `SIG_Profesiograma` (id 42) en SDMAPPTEST2: **`https://sdmapptest2.sedemi.local:7083`** (certificado autofirmado, vence el **06/11/2027**) y `http://192.168.1.77:7082` (redirige); grupo `SIG_Profesiograma`, entorno `QA`; cadena en la variable `ConnectionStrings__Profesiograma` del grupo; despliegue en `deploy/qa/` (FASE_7 §6, §9 y §10) |
| Frontend | `frontend/` (React 19.3 + TypeScript 6.0 strict + Vite 8.3, Fluent UI 9, TanStack Query 5, React Router 8, oxlint). `npm run dev` → `http://localhost:5173`, proxy `/api` → `https://localhost:7180`. Node 24.16.0 / npm 11.13.0 |

## 5. Hoja de ruta (lo que falta) y siguiente tarea

| Tarea | Alcance |
|---|---|
| 1. **Fase 7a** ✅ | Entornos DEV/QA/PRO, base QA, login y respaldos (pendiente 65); el sitio IIS sigue en la 7d |
| 2. **Fase 7b** ✅ | Entra ID y MSAL + 7b-2 (UPN), verificadas en DEV el 05/11/2026; QA y PRO tras el pendiente 75 |
| 3. Fase 7c | Graph: 23-4 y correo 71 (pendientes 63 y 66) |
| 4. Fase 7d | 7d-1, 7d-3 y 7d-4 ✅ (QA con HTTPS provisional); certificado de confianza y PRO: pendiente 75 |
| 5. Fase 3 | Carga desde SharePoint, con ensayos en QA |
| 6. Pruebas con usuarios | — |
| 7. Corte | Paso a producción |
| [Después de producción] 24e (opcional) | «Ver novedad» desde el cronograma (B0; `FASE_5_Novedades.md` §9.3) |

**Decisión del usuario (2026-09-29):** la migración de datos (Fase 3, carga masiva desde SharePoint) se hace **al final**, justo antes del corte a producción. Mientras tanto se desarrolla con datos de prueba. "Migración de EF Core" (crear tablas) ≠ "migración de datos".

## 7. Pendientes abiertos


1. Propietario real de `PRY-20260831-e4776f`, `PRY-20260902-bbd352`, `PRY-20260911-5ceab4`: `gestor.sig@` o `gestor.administrativosig@` (C29).
2. [Opcional] Excel de cargos → código Infor (la importación desde la API y la edición ya existen, TAREA-25-3-1). Texto anterior en `HISTORIAL.md`.
4. Office Script del reporte de cronograma: sin su código, EMPRESA, EKON y PROYECTO siguen como [SUPUESTO] Z7 (`FASE_5_Cronograma.md` §6 y §8). Texto anterior en `HISTORIAL.md`.
5. Zona horaria del sitio SharePoint (evidencia: Pacífico).
9. Decidir si `docs/origen/powerapps`, FASE_0 y el script SQL (contienen correos @sedemi.com) pueden subirse al remoto. Hasta decidirlo: commits locales, sin push. Pregunta abierta: ¿el remoto `origin` es privado?
10. Pendiente para TI: evaluar una política App Control for Business para equipos de desarrollo (Smart App Control se desactivó en la TAREA-03; texto anterior en `HISTORIAL.md`). [PENDIENTE DE DECISIÓN DEL USUARIO/TI]
13. Significado de `tipoHorario` del ERP (`M` / `D`) [PENDIENTE DE CONFIRMAR con el ERP o RR. HH.]. No indica turno nocturno (TAREA-26c, B1): la aplicación lo deduce de las horas. Texto anterior en `HISTORIAL.md`.
27. [TAREA-17b, H12] Datos migrados (Fase 3): un back sin días de descanso guardados después de su `FechaFin` se trata como recortado por una suspensión (histórico desde el día siguiente a su fin). Revisar en la carga masiva. [PENDIENTE DE CONFIRMAR]
36. [Después de producción] [Menor, backend] Agregar `esPrincipalInicial` al GET `…/edicion` (`PersonaEdicionDto`): hoy la pantalla "Actualizar personal" lo toma del detalle (`GET /api/proyectos/{id}`) para la marca "Será el principal inicial (responsable)" (P3).
37. [Después de producción] [Menor, backend] Agregar `principalRelacionadoId`, `cargo` y `observacion` a `PersonaReactivacionDto` (GET `…/reactivacion`) para mostrar la relación de los backs históricos en la pantalla de reactivación. Hoy la tabla de históricos de la TAREA-19c no muestra la relación, el cargo ni la observación. Origen: TAREA-19c (Fase A, sección A).
38. [Después de producción] [Menor, frontend] Unificar `validarEdicionPersonal` (`edicionPersonal.ts`, TAREA-19b) y `validarPersonalReactivacion` (`reactivacion.ts`, TAREA-19c): una sola función que acepte la fecha fin del proyecto vacía. Hoy la reactivación tiene su propia versión con los mismos textos más la regla de inicio ≥ R. Origen: TAREA-19c.
43. [Menor] `codEmpresa` del empleado (ECU, SCE…) no es el Id de compañía del ERP de proyectos (`Compania.Id`): no se cruzan. Si en el futuro se necesita filtrar empleados por la compañía del proyecto, hará falta una tabla de equivalencias.
44. [Futuro] La API de empleados trae `codPerfil`, `posicion` y `cargoTipo` (organizacionales), útiles para el módulo Perfiles. Hoy no se guardan (`FASE_4_Empleados_API.md`).
45. [Fase 3] `CorreoEmpresa` del ERP puede venir sin formato de correo (p. ej. «SC»): validarlo antes de usarlo; uno no válido = sin correo. Texto anterior en `HISTORIAL.md`.
46. [Después de producción] [Opcional, TAREA-26d-3 D1] Acción «Actualizar datos guardados del empleado» desde la API, con auditoría y concurrencia. Texto anterior en `HISTORIAL.md`. [PENDIENTE DE DECISIÓN DEL USUARIO]
47. [Seguridad, lo ejecuta el usuario] Cambiar la contraseña del login `profesiograma_dev` (débil y expuesta en un chat): en SQL Server (NIQUEL\SSDEV) y en los user-secrets (`ConnectionStrings:Profesiograma`, `UserSecretsId` sin cambios). Claude Code no la ve ni la escribe.
48. [Observar] Posible lentitud de la primera llamada tras arrancar la API (~4,7 s en un 404, TAREA-26d-3). Solo observar; si se repite en llamadas normales, medir (arranque de EF, primera conexión, caché de empleados).
49. [TAREA-26c, PENDIENTE RR. HH.] Confirmar con RR. HH.:
    - la duración del almuerzo y los márgenes respecto de la entrada y la salida (hoy no hay regla: solo dentro del turno, o los rangos RN09 en el diurno);
    - la lista de horarios no laborales (`HORARIOS_NO_LABORALES`).
53. [TAREA-22-2, implementado] PROYECTO vacío para PLANTA/OFICINAS [SUPUESTO aprobado el 20/10/2026, hasta que Infor confirme].
54. [TAREA-22-2, implementado] SALIDA DE JORNADA nocturna (p. ej. 17:00–04:00) tal cual [SUPUESTO aprobado el 20/10/2026, hasta que Infor confirme].
58. [Mantenimiento, TAREA-22-2b] Retirar el pin de `System.Security.Cryptography.Pkcs` 10.0.13 (`App.Infrastructure.csproj`) cuando Microsoft.Data.SqlClient y Microsoft.Identity.Web dependan de una versión corregida. La publicación incluye la DLL 10.0.13 (C7 de la TAREA-7d-1). Texto anterior en `HISTORIAL.md`.
59. [Fase 7] Si la SPA y la API quedan en orígenes distintos, exponer `Content-Disposition` y `X-Reporte-Filas`. Hoy no aplica: mismo origen (Vite en DEV; ASP.NET Core sirve la SPA en QA, TAREA-7d-1). Texto anterior en `HISTORIAL.md`.
60. [Después de producción] En Development el sembrador corre antes de `app.Run()`: con la base caída la API no arranca. Evaluar un sembrador tolerante a fallos o `SembrarAlIniciar = false`. Texto anterior en `HISTORIAL.md`. [PENDIENTE DE DECISIÓN DEL USUARIO]
61. [Robustez] Probar con Sophos (conectado y desconectado) el Connect Timeout de 5 s y `BaseDatos:ForzarTcp` (si falla: `ForzarTcp=false` o subir el tiempo). QA ya funciona con TCP y `NIQUEL,1433`. Texto anterior en `HISTORIAL.md` (H10, H28). [PENDIENTE]
63. [TAREA-23-4, Fase 7 con TI] **Lectura de la lista NOVEDADES ASISTENCIA** (no es del usuario). Bloquea la TAREA-23-4 si se elige la opción A o B. Opciones: **A)** permiso de aplicación `Sites.Selected` con acceso de solo lectura al sitio; **B)** lectura con la sesión del Admin (permiso delegado); **C)** exportación periódica a CSV (fuente `ArchivoLocal`). Se decide en la Fase 7 con TI. La carga histórica de la Fase 3 no depende de esto. Texto anterior en `HISTORIAL.md`. [PENDIENTE DE TI]
66. [Nuevo, 26/10/2026; para la 23-4] La lista de origen **nunca marca «anulada»** (pendiente 64 cerrado): un permiso médico borrado allá quedaría activo aquí, porque N10 impide anularlo a mano y R4 no anula por ausencia. Propuesta: opción explícita del Admin «anular ausentes», con vista previa de cuáles son y solo si la lectura fue completa. [PENDIENTE DE DECISIÓN DEL USUARIO]
68. [Menor, Fase 3] Medir cuántas personas asignadas en SharePoint no son de un grupo asignable (podrán conservarse; no agregarse a proyectos nuevos).
69. [Después de producción] [Menor] La comparación con el ERP podría marcar «ya no asignable» en el personal guardado (hoy solo informa).
71. [Fase 7] Correo de aviso de acceso (`Notificado = 0`) con Graph, al asignar un usuario a un departamento.
73. [Futuro] El join del reporte con `CargoInfor` distingue tildes y espacios internos: un cambio de grafía en el ERP deja el código Infor vacío sin aviso. Solución posible: columna normalizada con migración. Hoy lo mitigan los conteos de variantes y `personasGuardadas` (TAREA-25-3-1).
74. [Después de producción] [Menor, frontend] Extraer otros componentes repetidos de administración cuando aparezca una cuarta pantalla. Repetidos hoy (TAREA-25-3-2): el hook de filtros de la URL y el cuerpo del listado (esqueleto, error, vacío, `DataGrid`, `Paginacion`) de `PaginaUsuarios` y `PaginaCargosInfor`; `MOVIL` y los estilos de los diálogos en cinco archivos (`FASE_5_Usuarios_Departamentos.md` §10.6).
75. [Fase 7, TI] Pedido a TI enviado el ____. Respuesta pendiente. QA ya tiene HTTPS provisional (autofirmado, TAREA-7d-3). Temas:
    - un certificado de **Let's Encrypt** (como el de `backstack.sedemi.com`) para un nombre `.sedemi.com` de QA (`SDMAPPTEST2`, sitio id 42; nombre [PENDIENTE DE CONFIRMAR]) y para PRO;
    - el servidor IIS y el SQL de PRO; la base (propuesta `SIG_PROFESIOGRAMA`: ¿existe la PROFESIOGRAMA antigua?); la cuenta de administración de PRO;
    - los consentimientos de Graph (pendientes 63 y 71);
    - desactivar TLS 1.0 y 1.1 en SDMAPPTEST2 («Deshabilitar las versiones TLS heredadas: Not Set»);
    - la consulta sobre `X-Powered-By: Sedemi` (configurada a nivel de servidor);
    - actualizar el Hosting Bundle de SDMAPPTEST2 (runtime 10.0.10; la aplicación usa paquetes 10.0.12).
76. [Después de producción] El alta de usuarios reconoce el duplicado por usuarioId o por el texto (usuariosAdmin.ts, MENSAJE_DUPLICADO); aplicar tipoConflicto con el helper ProblemasConflicto.
77. [Fase 7, antes de PRO] D4 de la TAREA-7d-1: evaluar la configuración del frontend en tiempo de ejecución (servida por la API) para promover un solo build de QA a PRO; hoy `vite build --mode qa` con `frontend/.env.qa`.
81. [Reabierto] `vite` 8.3.1 (alta, solo desarrollo; E7 pospuesta). Desde el 13/11/2026: comparar el lockfile y ver con `npm view <paquete> time` vite y cada dependencia nueva; si tiene < 14 días, esperar.
83. [Antes de PRO] CSP completa (E4 de la 7d-3; hoy solo `frame-ancestors`): probarla con `Content-Security-Policy-Report-Only` en DEV (Griffel, MSAL).
84. [K6] tarea22c, 23-1, 23-2, 23-3, 25, 25-3 y 62 de `backend/tests/manual/`: lista para `git add -f` entregada en la Fase A de la TAREA-8; se cierra cuando el usuario confirme el commit.
85. Renovar el certificado de QA antes del **06/11/2027** (o reemplazarlo: pendiente 75).

## 7.1 Reglas técnicas obligatorias

1. **Bloqueo de asignaciones:** toda escritura de asignaciones (`ProyectoAsignacionDia` y el personal que las origina) — crear proyecto y, en el futuro, editar personal, cambiar fechas o aplicar fecha de corte — debe ejecutarse dentro de `ITransaccionAsignaciones`, que toma `sp_getapplock @Resource = 'profesiograma:asignaciones', @LockMode = 'Exclusive', @LockOwner = 'Transaction'` (espera máx. 15 s → 503). Dentro de la transacción se repite la consulta de cruces externos antes de guardar. Sin este bloqueo común, dos registros simultáneos podrían crear cruces.
2. **Pruebas de traducción SQL:** toda consulta EF nueva o modificada debe tener una prueba con `ToQueryString()` en `App.Infrastructure.Tests` (ver `CLAUDE.md` › Reglas de pruebas). Origen: error 500 de la TAREA-12 que las pruebas con dobles no detectaron.
3. **Toda escritura de un proyecto crea una etapa (el token de concurrencia depende de ello).** El token `versionProyecto` (TAREA-19x) es la última versión de etapa; una escritura sin etapa no lo cambiaría y otro cliente podría registrar sobre datos viejos sin 409. Hoy la cumplen la creación, el cambio de estado, la actualización de personal, la reactivación y la cabecera (con C9, registrar sin cambios no escribe). Nota para la Fase 3: las etapas migradas desde SIG_HISTORIAL conservan su VERSION; un proyecto migrado sin etapas tiene token 0 (su primera escritura crea la versión 1).
4. **Base de datos caída → 503 (TAREA-22c).** Todo acceso a la base pasa por EF (`ProfesiogramaDbContext`), cuyo `FalloBaseDatosInterceptor` convierte los fallos de conexión de SQL Server (números de `ClasificadorFalloSql`; el timeout -2 solo al abrir) en `BaseDatosNoDisponibleException` → 503 «Base de datos no disponible». Una conexión directa con `SqlConnection` (como `SqlDatabaseDiagnostics` de `/api/health/db`) no pasa por el interceptor y maneja su propio error. No se agregan `EnableRetryOnFailure` ni circuitos propios: el pool de SqlClient ya bloquea las aperturas tras un fallo. La cadena se fuerza a TCP (`tcp:` en el Data Source, TAREA-22c-b; se desactiva con `BaseDatos:ForzarTcp=false`) y lleva Connect Timeout `BaseDatos:TiempoEsperaConexionSegundos`: los dos ajustes viven en `DependencyInjection.CadenaConexionEfectiva`, en código (nunca en los user-secrets). Un servicio de escritura no vuelve a la base después del commit de `ITransaccionAsignaciones` (el 503 «no se guardó ningún cambio» de un fallo al abrir depende de ello).
5. **Turnos nocturnos (antes pendiente 50):** en un turno con la salida menor que la entrada, las horas después de las 00:00 pertenecen al **día de entrada**. El cronograma, el reporte y la asistencia deben respetarlo (hoy el motor solo usa días).
