# FASE 0 — Inventario (App PROFESIOGRAMA → .NET + SQL Server + React)
 
Fecha: 2026-09-29 · Estado: **pendiente de respuestas del usuario**
 
## 1. Inventario de archivos
 
### 1.1 Código Power Apps (YAML de pantallas) — 17 archivos
 
| # | Archivo | Qué contiene | Módulo |
|---|---|---|---|
| 1 | App.pa.yaml | `Formulas`: ColEppDetalle (78 EPP), colEppsVU (vida útil). `OnStart`: esquemas de colecciones, catálogo de novedades (6 tipos), `varEsSuperAdmin` (5 correos hardcodeados), lectura de la fila "666" de SIG PROYECTOS → PermisosGenerales → `varUegpsCalculadas` | Global / seguridad |
| 2 | _EditorState.pa.yaml | Orden de las 15 pantallas | Metadatos |
| 3 | PantallaPrincipal | Menú lateral con visibilidad por rol; popup (solo SuperAdmin) para asignar usuarios por departamento (7 grupos) → Patch fila "666" + flujo ReporteProyectosSIG("NOTIFICAR") | Navegación / permisos |
| 4 | GestionProyecto | Alta de proyecto: compañía/proyecto/dimensión/actividad/horario desde ERP (flujo ConsultarProyecto); principales y backs; generación de bloques por jornada (22-8, 11-4, 5-2, especial); detección de cruces; guardado en SIG PROYECTOS + SIG_ASIGNACIONES_PERSONAL + SIG_ASIGNACIONES_DETALLE + SIG_HISTORIAL + SIG_HISTORIAL_ACTIVIDADES; código `PRY-yyyymmdd-xxxxxx` | Proyectos |
| 5 | ConfigurarProyecto | Edición **legada** (lee/escribe JSON ASIGNACIONES/Etapas). Ninguna pantalla navega a ella | Proyectos (obsoleta) |
| 6 | ConfigurarProyecto_1 | Edición vigente: máquina de estados (SUSPENSION/CIERRE/REACTIVACION), regenerar desde fecha de corte (delta-write), cambio de actividad con vigencia | Proyectos |
| 7 | ResumenProyectos | Listado (filtros estado/fecha; admin ve todo, resto solo `CORREO_CREADOR`), carga para edición, acceso a AsignarPersonal (solo admin) | Proyectos |
| 8 | ControlMensualProyectos | Cronograma HTML (máx. 90 días) desde SIG_ASIGNACIONES_DETALLE + NOVEDADES; exportación Excel vía ReporteProyectosSIG (mapeo "CARGO INFOR" hardcodeado) | Cronograma |
| 9 | RegistrarNovedadAsistencia | Novedades PERSONA/PROYECTO/GENERAL → NOVEDADES ASISTENCIA (DETALLE_JSON) | Novedades |
| 10 | ControlPerfiles | Listado PERFIL_USUARIO, ficha HTML (6 secciones), edición del tarifario de exámenes y catálogo de equipos (fila EKON="DATOS"), QR de pasaporte/carnet | Perfiles |
| 11 | RegistroPerfiles (570 KB) | Formulario completo del perfil (datos generales, educación, certificados, calificaciones, experiencia, vacunas/dosis, exámenes con tarifario, equipos, EPP, empleado del mes, buenas prácticas); subida de archivos, pasaporte y carnet vía ArchivosPerfilUsuario; lee R_FOR_SO_02_RESTRICCIONES_TRABAJO, Catálogo_Certificados, ESTRUCTURA GENERAL; CIUO vía ConsultarProyecto | Perfiles |
| 12 | AsignarPersonal | Selección de personal (ESTRUCTURA GENERAL) → SIG PROYECTOS.ASIGNAR_PERSONAL (JSON) | Asistencia |
| 13 | FOR_SEI_11 | Registro de asistencia a capacitación → SIG ASISTENCIAS (upsert proyecto+fecha) + ReporteAST("FORSEI11") → PDF por correo | Asistencia |
| 14 | ASISTENCIAVISUALIZACION | Historial de asistencias por proyecto | Asistencia |
| 15 | FOR_SEI_12 | Detalle de asistencia y registro de HoraSalida | Asistencia |
| 16 | ControlEPPS | Catálogo EPP (ADMIN_EPPS), filtros, edición, foto | EPP |
| 17 | RegistroEPP | Alta/edición EPP (3 columnas JSON + imagen) | EPP |
 
### 1.2 Flujos Power Automate — 4 archivos
 
| # | Archivo | Disparador | Acciones | Conectores / externos |
|---|---|---|---|---|
| 18 | ConsultarProyecto.json | PowerApps V2 (compania, projecto, proceso) | Switch ACTIVIDADES / DIMENSIONES / PROYECTOS / COMPANIAS / HORARIOS / CIUO / CONTRATO / FIG → HTTP y devuelve JSON | `backstack.sedemi.com:7048` y `:7055` (sin autenticación visible) |
| 19 | ReporteProyectoSIG.json (ReporteProyectosSIG) | PowerApps V2 (json, proyecto, correo) | "NOTIFICAR": correos de acceso (asunto *ACCESO - PLATAFORMA PROFESIOGRAMA*). Otro valor: copia plantilla Excel → Office Script → correo con adjunto | Office 365 Outlook, OneDrive, Excel Online (Office Script) |
| 20 | ReporteAST.json | PowerApps V2 (metodo, registrosJSON) | FORSEI11: HTTP a Netlify → DOCX → PDF → correo → borra DOCX. Default: AST → carpeta LEVANTAMIENTOS/{codigoAST} (**no lo invoca esta app**) | `seguridad-industrial.netlify.app`, OneDrive, Outlook |
| 21 | ArchivosPerfilUsuario.json | PowerApps V2 (Ekon, Seccion, ID_Fila, Archivo, Accion) | OBTENER (lista archivos + links anónimos), PASAPORTE (PDF en Render + QR quickchart + URL_PASAPORTE/URL_QR), EXAMEN (carnet vacunas + QR), default (sube archivo a /PERFIL USUARIO/{Ekon}/{Seccion}) | `servidorpdf-n0d3.onrender.com`, `quickchart.io`, OneDrive, SharePoint (lista en OneDrive personal de sig4@sedemi.com) |
 
### 1.3 Datos (exportaciones SharePoint con `ListSchema` embebido) — 13 archivos
 
| # | Archivo | Filas en muestra | Campos esquema | Uso en la app | Notas |
|---|---|---|---|---|---|
| 22 | SIG PROYECTOS 9.csv | 4 (1 fila config "666" + 3 proyectos) | 33 | Lectura/escritura | ASIGNACIONES hasta ~1 MB por fila. Índices: ESTADO, PROYECTO_UID, CORREO_CREADOR, MIGRADO |
| 23 | SIG_ASIGNACIONES_DETALLE 2.csv | 627 | 18 | Lectura/escritura | Índices: Title, PROYECTO_UID, FECHA, EKON, CORREO_CREADOR |
| 24 | SIG_ASIGNACIONES_PERSONAL 2.csv | 55 | 20 | Lectura/escritura | |
| 25 | SIG_HISTORIAL 2.csv | 24 | 14 | Escritura/lectura | FECHA_CORTE vacía en 24/24 |
| 26 | SIG_HISTORIAL_ACTIVIDADES 1.csv | 3 | 13 | Escritura/lectura | |
| 27 | NOVEDADES ASISTENCIA 1.csv | 5 | 11 | Escritura/lectura | Tiene columna "Datos adjuntos" |
| 28 | SIG ASISTENCIAS.csv | 12 | 8 | Escritura/lectura | ProyectoID con valor solo en 3/12 |
| 29 | ESTRUCTURA GENERAL 5.csv | 2 (muestra) | 64 | Solo lectura (maestro de colaboradores) | Nombres internos field_1…field_19; contiene Sueldo, BPR e indicadores (sensibles); sincronizada por proceso externo |
| 30 | PERFIL_USUARIO 4.csv | 4 (1 fila "DATOS" + 3 perfiles) | 20 | Lectura/escritura | Datos médicos; fila "DATOS" = configuración (tarifario, equipos, EPP) |
| 31 | ADMIN_EPPS.csv | 5 | 6 | Lectura/escritura | Foto (Thumbnail). Nombre interno `Categoria` ≠ nombre visible `DATOS_GENERALES_EPPS` |
| 32 | AST_PUESTO_TRABAJO.csv | 4 | 28 | **No referenciada** en ninguna pantalla | Probablemente de otra app (AST) |
| 33 | Catálogo_Certificados.csv | 3 | 3 | Solo lectura | |
| 34 | R_FOR_SO_02_RESTRICCIONES_TRABAJO.csv | 7 | 31 | Solo lectura | Datos médicos; imágenes Thumbnail; nombres internos Cedula/Nombre ≠ visibles CEDULA/NOMBRE |
 
## 2. Faltantes / incompletos
 
1. Conteo real de ítems por lista (las exportaciones son muestras).
2. Definición de orígenes de datos del .msapp (URL del sitio y GUID de cada lista). PERFIL_USUARIO vive en el OneDrive personal de sig4@sedemi.com.
3. Lista SIG_ASIGNACIONES_BLOQUES: no aparece en app ni en los archivos.
4. Office Script del reporte Excel y la plantilla Excel.
5. Contratos JSON (request/response) de Netlify `generate-ast` y Render (`generar-pdf-pasaporte`, `generar-pdf-vacunas`).
6. Documentación de las APIs `backstack.sedemi.com` (autenticación, red, Swagger).
7. Flujos externos que tocan las mismas listas (sincronización de ESTRUCTURA GENERAL, notificaciones de vencimientos, etc.).
8. Otras apps que comparten listas (PERFIL_USUARIO, ESTRUCTURA GENERAL, R_FOR_SO_02, AST_PUESTO_TRABAJO).
9. Volumen de archivos en OneDrive (PERFIL USUARIO, PASAPORTES, LEVANTAMIENTOS), imágenes Thumbnail y adjuntos.
10. Recursos multimedia (logos).
## 3. Contradicciones y defectos detectados
 
| # | Hallazgo | Evidencia |
|---|---|---|
| C1 | SIG_HISTORIAL.FECHA_CORTE es **Text** pero la app hace Patch con fecha | 0/24 filas con valor |
| C2 | Dos columnas de UID en SIG PROYECTOS: PROJECT_UID (Note, legado) y PROYECTO_UID (Text, nuevo). Las pantallas usan ambas | GestionProyecto/RegistrarNovedad usan PROJECT_UID; ResumenProyectos/ConfigurarProyecto_1 usan PROYECTO_UID |
| C3 | EKON con tipos distintos: Number (DETALLE, PERSONAL) vs Text (PERFIL_USUARIO, R_FOR_SO_02, ESTRUCTURA GENERAL). El CSV exporta "2.448" (separador de miles) | Esquemas |
| C4 | SIG ASISTENCIAS.ProyectoID es Note (multilínea) y la app filtra por igualdad (no delegable); 9/12 filas sin ProyectoID → no se ven en ASISTENCIAVISUALIZACION. DatosProyectoJSON existe pero la app no la escribe | FOR_SEI_11, ASISTENCIAVISUALIZACION |
| C5 | FOR_SEI_11 compara "Capacitación"/"Diálogo"/"Socialización" contra ítems en mayúsculas ("CAPACITACIÓN") → los checks del PDF nunca se marcan | Payload `checkCapacitacion` |
| C6 | Detección de cruces inconsistente: GestionProyecto y RegistrarNovedadAsistencia parsean JSON ASIGNACIONES; ConfigurarProyecto_1 consulta SIG_ASIGNACIONES_DETALLE | OnVisible |
| C7 | Límites de principales/backs: 20/20 (GestionProyecto, ConfigurarProyecto_1), 5/5 (ConfigurarProyecto), JSON `Reglas` dice 5/5 | Botones "Principal"/"Back" |
| C8 | ESTADO incluye INACTIVO, pero la máquina de transiciones nunca permite llegar a él | estadoEdit_1.OnChange |
| C9 | ResumenProyectos: "Sin Back" se muestra cuando `CountRows = 10` (debería ser 0) | lblNombreBack |
| C10 | Visibilidad de menú con `User().Email in Text(PermisosGenerales)` → coincidencia por subcadena | PantallaPrincipal |
| C11 | ArchivosPerfilUsuario: rama "Existe_link_carnet" asigna el literal "URL_CARNET_VACUNAS" en vez del valor de la columna | Set_variable_1 |
| C12 | Fechas DateOnly guardadas a las 07:00Z/08:00Z → la zona horaria del sitio SharePoint parece Pacífico (UTC-8/-7), no Ecuador (UTC-5) | Todas las listas |
| C13 | Nombres internos ≠ nombres visibles (ADMIN_EPPS, R_FOR_SO_02, ESTRUCTURA GENERAL field_N). La app usa los visibles; la API REST usa los internos | Esquemas |
| C14 | Documentos personales/médicos compartidos con links **anónimos** (scope Anonymous) y QR públicos | ArchivosPerfilUsuario |
 
## 4. Preguntas (ver respuesta en el chat)
 
Pendientes de respuesta. Grupos: A. Alcance, B. Datos, C. Integraciones, D. Seguridad y roles, E. Técnico.
 
---
 
## 5. Respuestas del usuario y decisiones (cierre Fase 0 — 2026-09-29)
 
| # | Tema | Decisión |
|---|---|---|
| 1 | Alcance inicial | Solo módulo **Proyectos**: crear, control/listado, edición/estados, **cronograma** (+ reporte Excel) y **novedades**. Perfiles, EPP, Asistencia FOR SEI 11/12 se migran después. |
| 2 | Pantalla legada | Se usa `ConfigurarProyecto_1`. `ConfigurarProyecto` se descarta. |
| 3 | AST | `AST_PUESTO_TRABAJO` y rama default de `ReporteAST` fuera de alcance. |
| 4 | Otras apps | Según usuario, PROFESIOGRAMA es la única app que escribe en estas listas. [VER C15 Fase 1] |
| 5 | Volumen | `SIG_ASIGNACIONES_DETALLE` ≈ 4.192 filas; resto < 1.000. |
| 6 | SIG_ASIGNACIONES_BLOQUES | No se usa. Se ignora. |
| 7 | JSON legado | `INF_GENERAL` / `ASIGNACIONES` son el modelo antiguo; no se replica. Se migra desde las listas normalizadas. [PENDIENTE: proyectos con MIGRADO=false] |
| 8 | ESTRUCTURA GENERAL | Se reemplaza por la API de empleados del servidor. [PENDIENTE contrato] |
| 9 | Versiones SharePoint | No se migran. Solo último estado. |
| 10 | Archivos | Fuera de alcance ahora. A futuro se desea almacenar en **SharePoint** (cambia la decisión de stack "file server"). |
| 11 | APIs backstack | Red interna; se probarán paso a paso. |
| 12 | Servicios externos | Servicios PDF (Netlify/Render) se mantienen. IIS con salida a internet. |
| 13 | Reporte Excel | Se genera en .NET (ClosedXML). [PENDIENTE plantilla + Office Script] |
| 14 | Correo | Microsoft Graph. |
| 15 | Roles | Mantener el modelo actual, mejorado. |
| 16 | Datos médicos | Fuera de alcance por ahora. |
| 17 | Zona horaria | Usuario cree que es Ecuador. [PENDIENTE verificar: la evidencia de datos indica Pacífico] |
| 18 | Exportación | REST JSON (decidido). |