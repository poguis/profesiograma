# ESTADO ACTUAL DEL PROYECTO — PROFESIOGRAMA (leer primero)

**Última actualización:** 2026-10-29 (TAREA-25-3-2, cierre). Solo el estado **vigente** (TAREA-X1-B); lo cerrado está en [`docs/HISTORIAL.md`](HISTORIAL.md): **no leerlo salvo que la tarea lo pida**.

| Área | Estado al 29/10/2026 |
|---|---|
| Proyectos (backend y frontend) | Crear, listado, detalle, estados, edición, reactivación, cronograma y reporte Excel ✅ (TAREA-07 a 22c) |
| Empleados | Desde la API EvolutionEmployee con caché en memoria (opción C) ✅ (26a, 26d, 26c) |
| Novedades, backend | 23-1, 23-2 y 23-3 ✅ (N13 cerrado); 23-4 (Graph) bloqueada por los pendientes 7 y 63 |
| Novedades, frontend | 24a–24d ✅ verificadas por el usuario el 26/10/2026 (24d: S1–S6; 349/349). Sigue la 24e (opcional, B0) |
| Criterio de personal (TAREA-62) | ✅ Cerrada: 62-1 (backend) y 62-2 (frontend) verificadas por el usuario el 28/10/2026 (62-2: A1–A11; 362/362) |
| Usuarios y departamentos (TAREA-25) | ✅ **Completa:** 25-1, 25-2, 25-3-1 (backend 1158/1158; `cargos.cmd` 0 FALLA) y 25-3-2 (cargos Infor, frontend; 399/399) verificadas por el usuario el 29/10/2026. Filas conservadas: Id 4 y 7 de prueba (inactivos), Id 5 gestor2; `CargoInfor` con 499 filas (491 importadas) |
| Aprendizaje del usuario | Objetivo aparte del proyecto: Entra ID, entornos y bases de datos, con explicaciones paso a paso |
| **Siguiente tarea** | Cierre funcional de la primera entrega: decidir los pendientes menores (24e, 18, 36, 37, 38, 46, 60, 67, 69, 74) y preparar el pedido a TI para la Fase 7 |

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
| Entra ID, entornos y despliegue (Fase 7) | §4 de este documento; pendientes 7, 59, 63 y 65 |

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
| Proyectos | `backend/src/App.Domain`, `App.Application`, `App.Infrastructure`, `App.Api` (+ `backend/sql/`, `backend/tests/` vacía) |
| Original | `D:\PROYECTOS\Profesiograma`: respaldo de **solo lectura** del Paso 1 (no modificar) |
| User-secrets | `UserSecretsId = profesiograma-api-4d2f7c1e` en `App.Api.csproj`; conservarlo para no perder la cadena de conexión |
| Implementado | `GET /api/health/db` (diagnóstico de conexión) con `IDatabaseDiagnostics` (Application) y `SqlDatabaseDiagnostics` (Infrastructure, Microsoft.Data.SqlClient) |
| Servidor SQL | `NIQUEL\SSDEV` — SQL Server 2019 Enterprise RTM (15.0.2190.7), compartido con ~42 bases de otros sistemas |
| Base | `PROFESIOGRAMA_DEV`, collation `Modern_Spanish_CI_AS`, compatibilidad 150 (esquema creado por migraciones EF en la TAREA-04; sin datos de prueba aún) |
| Login app | `profesiograma_dev` (db_owner solo de su base). Estándar futuro: `profesiograma_test`, `profesiograma_prod_app` + `profesiograma_prod_migrator` |
| Cadena de conexión | `dotnet user-secrets` → `ConnectionStrings:Profesiograma` (no está en el repo) |
| ERP | `ServiciosExternos:Modo`: `Http` (`https://backstack.sedemi.com:7048` / `:7055`, TLS válido, sin autenticación). **Development usa el ERP real en modo Http desde la TAREA-26a**, activado por user-secrets (`dotnet user-secrets set "ServiciosExternos:Modo" "Http" --project src/App.Api`; `appsettings.Development.json` sigue diciendo `Simulado`, que vuelve con `user-secrets remove`). Ver `backend/tests/manual/tarea26a/LEEME.md` |
| Pruebas | `dotnet test --solution Profesiograma.slnx -c Release` (xUnit v3 sobre Microsoft Testing Platform): App.Domain.Tests, App.Application.Tests, App.Infrastructure.Tests |
| URL local | `https://localhost:7180` / `http://localhost:5180` |
| Frontend | `frontend/` (React 19.3 + TypeScript 6.0 strict + Vite 8.3, Fluent UI 9, TanStack Query 5, React Router 8, oxlint). `npm run dev` → `http://localhost:5173`, proxy `/api` → `https://localhost:7180`. Node 24.16.0 / npm 11.13.0 |

## 5. Hoja de ruta (lo que falta) y siguiente tarea

Las secciones 6 (hoja de ruta), 8 y 9 anteriores, con lo terminado, están en `docs/HISTORIAL.md`.

| Tarea | Alcance |
|---|---|
| 24e (opcional) | «Ver novedad» desde el cronograma (B0; `FASE_5_Novedades.md` §9.3) |
| 23-4 | Fuente real por Graph (bloqueada por los pendientes 7 y 63) |
| Después | Fase 3 (carga masiva), Fase 7 (Entra ID, IIS), corte a producción |

**Decisión del usuario (2026-09-29):** la migración de datos (Fase 3, carga masiva desde SharePoint) se hace **al final**, justo antes del corte a producción. Mientras tanto se desarrolla con datos de prueba. "Migración de EF Core" (crear tablas) ≠ "migración de datos".

## 7. Pendientes abiertos


1. Propietario real de `PRY-20260831-e4776f`, `PRY-20260902-bbd352`, `PRY-20260911-5ceab4`: `gestor.sig@` o `gestor.administrativosig@` (C29).
2. Excel de cargos → código Infor (opcional). **TAREA-25-3-1:** la importación desde la API (todos los activos) y la edición de `CodigoInfor` ya están en el backend (`FASE_5_Usuarios_Departamentos.md` §10); la alta puntual también da de alta el puesto. Queda abierto solo el Excel, como opcional.
4. Office Script del reporte de cronograma (lo que hace con los textos de fecha y hora). **TAREA-20:** la plantilla ya está en `docs/PLANTILLA_REPORTE_PROYECTOS.xlsx` (hoja `'PROYECTO '`, `Tabla1`, 14 columnas; nombres definidos `LABORABLE`/`LIBRE` rotos, `#REF!`); el análisis está en `FASE_5_Cronograma.md` §6. Falta el Office Script. **TAREA-22-2:** el flujo `ReporteProyectosSIG` sí está en el repo (`docs/origen/sharepoint/esquemas/ReporteProyectoSIG.json`): copiaba la plantilla, ejecutaba el Office Script (solo está su Id, no su código) y enviaba el archivo por correo. Sin el script, los tipos de EMPRESA, EKON y PROYECTO siguen como [SUPUESTO] Z7 (`FASE_5_Cronograma.md` §8).
5. Zona horaria del sitio SharePoint (evidencia: Pacífico).
7. [Fase 7, más adelante] App Registrations (API + SPA) con App Roles `Admin` y `Gestor`. (`/api/health/db` ya es `AllowAnonymous` desde la TAREA-02.) **Bloquea la TAREA-23-4** si se elige la opción A o B del pendiente 63 (C33 de `FASE_2_Modelo_Datos.md`). Texto anterior en `HISTORIAL.md`.
8. Inconsistencia menor de documentación: FASE_2 secc. 8 menciona el login `profesiograma_app`; el vigente es `profesiograma_dev`.
9. Decidir si `docs/origen/powerapps`, FASE_0 y el script SQL (contienen correos @sedemi.com) pueden subirse al remoto. Hasta decidirlo: commits locales, sin push.
10. **Resuelto (TAREA-03):** Smart App Control bloqueaba `dotnet ef`/`dotnet run` (eventos CodeIntegrity 3033/3077). El usuario lo desactivó (`VerifiedAndReputablePolicyState = 0`). Pendiente para TI: evaluar una política App Control for Business para equipos de desarrollo. [PENDIENTE DE DECISIÓN DEL USUARIO/TI]
11. Leer la zona horaria de negocio desde `Parametro.ZONA_HORARIA` en lugar de la constante `"SA Pacific Standard Time"` (`ProyectoConsultaServicio` y `DatosPruebaSembrador`). Origen: TAREA-07 (C3).
12. [Menor] El sembrador guardó `ProyectoErpEstado = "ABIERTO"` en PRY-DEV-0001; el ERP usa `status = "Activo"` (confirmado en TAREA-11). No se cambia por ahora.
13. Significado de `tipoHorario` del ERP (`M` / `D`) [PENDIENTE DE CONFIRMAR con el ERP o RR. HH.]. TAREA-26a: 65 horarios M y 4 D; el significado no se deduce de los datos. El modo Http ya se probó de extremo a extremo en Development (A6, proyecto Id 14). TAREA-26c (B1): M/D **no** indica turno nocturno; la aplicación lo deduce de las horas (salida < entrada).
15. Sin prueba de traducción SQL (fuera del alcance pedido en la TAREA-12): consultas de `UsuarioProvisionamiento` y `DatosPruebaSembrador` (Paso 2).
16. Un JSON mal formado en `POST /api/proyectos` (p. ej. fecha ilegible) lo rechaza ASP.NET con un 400 genérico en inglés antes del validador.
18. Los parámetros `ALMUERZO_SALIDA_OPCIONES` / `ALMUERZO_REGRESO_OPCIONES` quedan **sin uso**: el validador y `GET /api/proyectos/opciones-formulario` usan `ReglasAlmuerzo` (Application, rangos RN09 y opciones cada hora). Decidir si se eliminan o si `ReglasAlmuerzo` pasa a leerlos (servirían al validador y al formulario a la vez). Origen: TAREA-13 (B0). [PENDIENTE DE DECISIÓN DEL USUARIO]
19. [TAREA-13] Caso "usuario con varios departamentos" (selector de P5) sin verificación visual: los dos usuarios de desarrollo tienen un solo departamento. Cubierto por pruebas (servicio de opciones y estado inicial). Para verlo en pantalla habría que insertar un `UsuarioDepartamento` con SQL (requiere aprobación).
21. [TAREA-13] Desde la pantalla, el 409 solo ocurre por carrera (la vista previa ya muestra los cruces y deshabilita "Registrar"). Prueba V13b del reporte con dos pestañas.
24. [TAREA-14] La advertencia E6 ("Se eliminarán días ya transcurridos.") solo está cubierta por pruebas: los proyectos de prueba Id 7 y 8 son de 2027.
27. [TAREA-17b, H12] Datos migrados (Fase 3): un back sin días de descanso guardados después de su `FechaFin` se trata como recortado por una suspensión (histórico desde el día siguiente a su fin). Revisar en la carga masiva. [PENDIENTE DE CONFIRMAR]
36. [Menor, backend] Agregar `esPrincipalInicial` al GET `…/edicion` (`PersonaEdicionDto`): hoy la pantalla "Actualizar personal" lo toma del detalle (`GET /api/proyectos/{id}`) para la marca "Será el principal inicial (responsable)" (P3).
37. [Menor, backend] Agregar `principalRelacionadoId`, `cargo` y `observacion` a `PersonaReactivacionDto` (GET `…/reactivacion`) para mostrar la relación de los backs históricos en la pantalla de reactivación. Hoy la tabla de históricos de la TAREA-19c no muestra la relación, el cargo ni la observación. Origen: TAREA-19c (Fase A, sección A).
38. [Menor, frontend] Unificar `validarEdicionPersonal` (`edicionPersonal.ts`, TAREA-19b) y `validarPersonalReactivacion` (`reactivacion.ts`, TAREA-19c): una sola función que acepte la fecha fin del proyecto vacía. Hoy la reactivación tiene su propia versión con los mismos textos más la regla de inicio ≥ R. Origen: TAREA-19c.
43. [Menor] `codEmpresa` del empleado (ECU, SCE…) no es el Id de compañía del ERP de proyectos (`Compania.Id`): no se cruzan. Si en el futuro se necesita filtrar empleados por la compañía del proyecto, hará falta una tabla de equivalencias.
44. [Futuro] La API de empleados trae `codPerfil`, `posicion` y `cargoTipo` (organizacionales), útiles para el módulo Perfiles. Hoy no se guardan (`FASE_4_Empleados_API.md`).
45. [Fase 3] El ERP puede devolver `CorreoEmpresa` (`mailEmpresa`) sin formato de correo (p. ej. "SC" en el empleado 11358). Validar el formato antes de usarlo (Graph, notificaciones, `Usuario.EmpleadoId` desde `mailEmpresa` en la Fase 7); un valor no válido se trata como sin correo.
46. [Opcional, TAREA-26d-3 D1] Acción "Actualizar datos guardados del empleado": refrescar la fila de `Empleado` desde la API (con auditoría y control de concurrencia) cuando la comparación "guardado frente al ERP" muestra diferencias. Hoy los avisos solo informan y los datos se refrescan en la próxima alta puntual. `ProyectoPersonal.CargoAsignado` no se refresca (es histórico). [PENDIENTE DE DECISIÓN DEL USUARIO]
47. [Seguridad, lo ejecuta el usuario] Cambiar la contraseña del login `profesiograma_dev` (débil y expuesta en un chat): en SQL Server (NIQUEL\SSDEV) y en los user-secrets (`ConnectionStrings:Profesiograma`, `UserSecretsId` sin cambios). Claude Code no la ve ni la escribe.
48. [Observar] Posible lentitud de la primera llamada tras arrancar la API (~4,7 s en un 404, TAREA-26d-3). Solo observar; si se repite en llamadas normales, medir (arranque de EF, primera conexión, caché de empleados).
49. [TAREA-26c, PENDIENTE RR. HH.] Confirmar con RR. HH.:
    - la duración del almuerzo y los márgenes respecto de la entrada y la salida (hoy no hay regla: solo dentro del turno, o los rangos RN09 en el diurno);
    - la lista de horarios no laborales (`HORARIOS_NO_LABORALES`).
50. [TAREA-20–24, cronograma, reporte y novedades/asistencia] En los turnos nocturnos (salida < entrada), las horas después de las 00:00 pertenecen al **día de entrada**. Ningún cálculo actual usa `HoraEntrada`/`HoraSalida` por fecha (el motor del cronograma solo usa días), pero el cronograma, el reporte y la asistencia deben respetarlo.
53. [TAREA-22-2] Columna PROYECTO para PLANTA/OFICINAS (sin ProjectId del ERP): **vacía** [SUPUESTO aprobado el 20/10/2026, hasta que Infor confirme]. Implementado en la TAREA-22-2.
54. [TAREA-22-2] SALIDA DE JORNADA en turnos nocturnos (salida menor que el ingreso, p. ej. 17:00–04:00): se exporta **tal cual** [SUPUESTO aprobado el 20/10/2026, hasta que Infor confirme cómo lo interpreta]. Implementado en la TAREA-22-2.
58. [Mantenimiento, TAREA-22-2b, 21/10/2026] Retirar el pin de `System.Security.Cryptography.Pkcs` 10.0.13 (`App.Infrastructure.csproj`) cuando Microsoft.Data.SqlClient y Microsoft.Identity.Web resuelvan una versión corregida (≥ 9.0.21 o ≥ 10.0.13; avisos GHSA-846f-f764-76mx y GHSA-2fmv-rv2c-59x7). La publicación incluye la DLL 10.0.13, así que no depende del Hosting Bundle del servidor.
59. [Fase 7, TAREA-22b] Si la SPA y la API quedan en orígenes distintos, exponer `Content-Disposition` y `X-Reporte-Filas` (`Access-Control-Expose-Headers`): sin eso el navegador no deja leerlos y la descarga usa el nombre por defecto (`Reporte_{código}_{yyyyMMdd}_{yyyyMMdd}.xlsx`) y el aviso «Reporte generado.» sin la cantidad de filas. Hoy no aplica: mismo origen (proxy de Vite; IIS en la Fase 7) y CLAUDE.md pide no configurar CORS.
60. [Robustez, TAREA-22c] En Development el sembrador (`DatosPrueba:SembrarAlIniciar = true` en `appsettings.Development.json`) corre antes de `app.Run()`: con la base caída la API no arranca (y el 503 de la TAREA-22c no puede verse). Evaluar un sembrador tolerante a fallos (registrar y seguir) o `SembrarAlIniciar = false`. En producción el sembrador no corre. [PENDIENTE DE DECISIÓN DEL USUARIO]
61. [Robustez, TAREA-22c/22c-b] Probar desde casa con Sophos (conectado y desconectado) el Connect Timeout de 5 s con `BaseDatos:ForzarTcp=true` (por defecto). Si la conexión a la base real falla con `ForzarTcp=true` (p. ej. el servidor solo acepta Named Pipes; comprobar `net_transport` en SSMS), usar `BaseDatos:ForzarTcp=false`; si aparecen 503 falsos con la VPN conectada, subir `BaseDatos:TiempoEsperaConexionSegundos`. Nota de la verificación del 21/10/2026 en `HISTORIAL.md` H10. [PENDIENTE]
63. [TAREA-23-4, Fase 7 con TI] **Lectura de la lista NOVEDADES ASISTENCIA** (no es del usuario). Opciones: **A)** permiso de aplicación `Sites.Selected` con acceso de solo lectura al sitio; **B)** lectura con la sesión del Admin (permiso delegado); **C)** exportación periódica a CSV (fuente `ArchivoLocal`). Se decide en la Fase 7 con TI. La carga histórica de la Fase 3 no depende de esto. Texto anterior en `HISTORIAL.md`. [PENDIENTE DE TI]
65. [Nuevo, 26/10/2026; Fase 7] **Entornos y despliegue con buenas prácticas:** bases separadas DEV, QA y PRO; probar primero en pruebas y pasar a producción solo cuando todo esté OK; respaldo antes de cada cambio; migraciones aplicadas con scripts revisados (nunca `database update` directo en producción) y plan de reversa; secretos y configuración por entorno.
66. [Nuevo, 26/10/2026; para la 23-4] La lista de origen **nunca marca «anulada»** (pendiente 64 cerrado): un permiso médico borrado allá quedaría activo aquí, porque N10 impide anularlo a mano y R4 no anula por ausencia. Propuesta: opción explícita del Admin «anular ausentes», con vista previa de cuáles son y solo si la lectura fue completa. [PENDIENTE DE DECISIÓN DEL USUARIO]
67. [Menor, backend + frontend] Agregar una extensión estable al ProblemDetails de los 409 de novedades (por ejemplo `tipoConflicto`: duplicado, cambio, anulada, registroConcurrente); hoy el frontend reconoce el duplicado por `codigo` o por el title exacto (`MENSAJE_DUPLICADO`). [PENDIENTE DE DECISIÓN DEL USUARIO]
68. [Menor, Fase 3] Medir cuántas personas asignadas en SharePoint no son de un grupo asignable (podrán conservarse; no agregarse a proyectos nuevos).
69. [Menor] La comparación con el ERP podría marcar «ya no asignable» en el personal guardado (hoy solo informa).
71. [Fase 7] Correo de aviso de acceso (`Notificado = 0`) con Graph, al asignar un usuario a un departamento.
73. [Futuro] El join del reporte con `CargoInfor` distingue tildes y espacios internos: un cambio de grafía en el ERP deja el código Infor vacío sin aviso. Solución posible: columna normalizada con migración. Hoy lo mitigan los conteos de variantes y `personasGuardadas` (TAREA-25-3-1).
74. [Menor, frontend] Extraer otros componentes repetidos de administración cuando aparezca una cuarta pantalla. Repetidos hoy (TAREA-25-3-2): el hook de filtros de la URL y el cuerpo del listado (esqueleto, error, vacío, `DataGrid`, `Paginacion`) de `PaginaUsuarios` y `PaginaCargosInfor`; `MOVIL` y los estilos de los diálogos en cinco archivos (`FASE_5_Usuarios_Departamentos.md` §10.6).

## 7.1 Reglas técnicas obligatorias

1. **Bloqueo de asignaciones:** toda escritura de asignaciones (`ProyectoAsignacionDia` y el personal que las origina) — crear proyecto y, en el futuro, editar personal, cambiar fechas o aplicar fecha de corte — debe ejecutarse dentro de `ITransaccionAsignaciones`, que toma `sp_getapplock @Resource = 'profesiograma:asignaciones', @LockMode = 'Exclusive', @LockOwner = 'Transaction'` (espera máx. 15 s → 503). Dentro de la transacción se repite la consulta de cruces externos antes de guardar. Sin este bloqueo común, dos registros simultáneos podrían crear cruces.
2. **Pruebas de traducción SQL:** toda consulta EF nueva o modificada debe tener una prueba con `ToQueryString()` en `App.Infrastructure.Tests` (ver `CLAUDE.md` › Reglas de pruebas). Origen: error 500 de la TAREA-12 que las pruebas con dobles no detectaron.
3. **Toda escritura de un proyecto crea una etapa (el token de concurrencia depende de ello).** El token `versionProyecto` (TAREA-19x) es la última versión de etapa; una escritura sin etapa no lo cambiaría y otro cliente podría registrar sobre datos viejos sin 409. Hoy la cumplen la creación, el cambio de estado, la actualización de personal, la reactivación y la cabecera (con C9, registrar sin cambios no escribe). Nota para la Fase 3: las etapas migradas desde SIG_HISTORIAL conservan su VERSION; un proyecto migrado sin etapas tiene token 0 (su primera escritura crea la versión 1).
4. **Base de datos caída → 503 (TAREA-22c).** Todo acceso a la base pasa por EF (`ProfesiogramaDbContext`), cuyo `FalloBaseDatosInterceptor` convierte los fallos de conexión de SQL Server (números de `ClasificadorFalloSql`; el timeout -2 solo al abrir) en `BaseDatosNoDisponibleException` → 503 «Base de datos no disponible». Una conexión directa con `SqlConnection` (como `SqlDatabaseDiagnostics` de `/api/health/db`) no pasa por el interceptor y maneja su propio error. No se agregan `EnableRetryOnFailure` ni circuitos propios: el pool de SqlClient ya bloquea las aperturas tras un fallo. La cadena se fuerza a TCP (`tcp:` en el Data Source, TAREA-22c-b; se desactiva con `BaseDatos:ForzarTcp=false`) y lleva Connect Timeout `BaseDatos:TiempoEsperaConexionSegundos`: los dos ajustes viven en `DependencyInjection.CadenaConexionEfectiva`, en código (nunca en los user-secrets). Un servicio de escritura no vuelve a la base después del commit de `ITransaccionAsignaciones` (el 503 «no se guardó ningún cambio» de un fallo al abrir depende de ello).

