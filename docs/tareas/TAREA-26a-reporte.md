# TAREA-26a — ERP real en Development: logs, scripts de sondeo y verificación

**Fecha:** 2026-10-07 (Fase A y Fase B)
**Resultado:** ✅ completada. Verificación del usuario del 07/10/2026: **A1–A11 OK** (sección 6). Sondeo A2 completo (sección 8.4). Dato de prueba: **Id 14**, creado con datos ERP reales (A6).
- `dotnet build Profesiograma.slnx -c Release`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **549/549** (543 anteriores sin cambios + 6 nuevas).
- Los 4 scripts se probaron **solo con `SIMULAR=1`**: 4/4 OK, sin llamadas de red (sección 5).

**Lo que no se hizo:**
- ninguna llamada a una API, ni a la nuestra ni a `backstack.sedemi.com`;
- `dotnet run`, `npm run dev`, `user-secrets set/remove`;
- comandos git que modifiquen el repositorio.

No se cambió el comportamiento funcional del backend: solo se agregaron dos logs.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| P1 | Solo la 26a. La 26b (sincronización) va después, con el contrato confirmado por el sondeo |
| P2 | Modo Http en Development por user-secrets (lo ejecuta el usuario; documentado en `LEEME.md`) |
| P3 | Se conservan los datos de prueba (Id 1–13, compañía 9001) |
| P4 | Largo de los Id del ERP: se decide con los datos de `erp-largos.cmd`. No se tocan validadores ni columnas |
| P5/P6 | Cuerpo confirmado por el usuario: `{ "parameter": "", "estado": "A", "codEmpresa": "", "codDepartamento": "" }` (todos los activos de todas las empresas). `parameter` = código o cédula, opcional. Se sincronizarán todas las empresas |
| P15 | El sondeo puede listar los nombres distintos de departamento, unidad, área y sección con su conteo (organizacionales) |

## 2. Qué se hizo
**Backend (solo logs):**
- **Log del modo al arrancar** (`Program.cs`, Information): `ERP: modo Http` o `ERP: modo Simulado`.
  - El modo lo calcula `ErpServiceCollectionExtensions.ModoEfectivo(IConfiguration)`, que usa la misma lectura de opciones que el registro.
  - No muestra URL ni secretos.
  - Un modo inválido nunca llega al log: `AddCatalogoErp` ya impide el arranque, como antes.
- **Log Debug por llamada real al ERP** (`CatalogoErpHttp.LeerAsync`): `ERP {operación}: HTTP {código} en {ms} ms`.
  - Se emite después de recibir la respuesta, sea exitosa o no.
  - Nunca incluye el cuerpo ni la URL.
  - Las respuestas en caché no pasan por `LeerAsync`, así que no se registran (caso A10).
  - Los errores de red y el timeout conservan su `Warning` de siempre.
  - En Development ya se ve: `appsettings.Development.json` tiene `"App": "Debug"` y la categoría es `App.Infrastructure.Erp.CatalogoErpHttp`.

**Scripts (`backend/tests/manual/tarea26a/`).** PowerShell 5.1, archivos ASCII, sin curl; ver `LEEME.md`.
- **`comun.ps1`:**
  - `Invocar-Http` es la **única** función que abre conexiones. En simulación lanza un error en lugar de conectarse.
  - Las respuestas simuladas se leen de archivos.
  - JSON con `JavaScriptSerializer` sin límite de tamaño (`ConvertFrom-Json` de PS 5.1 falla por encima de 2 MB).
  - El certificado de desarrollo se acepta **solo** para `localhost`; `backstack.sedemi.com` usa la validación estándar.
- **`conectividad.cmd`/`.ps1` (A1):** `Test-NetConnection` a los puertos 7048 y 7055 y GET a `list_company` mostrando solo código, tiempo y bytes.
- **`sondeo-empleados.cmd`/`.ps1` (A2):**
  - procesa cada `cuerpo-empleados-*.json` por separado;
  - muestra la forma de la respuesta (arreglo o envoltura, con las claves y los metadatos de página), la cantidad de registros y la advertencia de paginación;
  - por campo: tipos, ausentes, nulos, vacíos, largo máximo y la marca "POSIBLE SENSIBLE (no mapear)", solo con el nombre del campo;
  - valores distintos **solo** de la lista permitida, explícita al inicio del script: `estado`, `codEmpresa`, departamento, unidad, área, sección y familia de puesto;
  - análisis de `codPersona`: tipos, distintos, repetidos, no numéricos, con `.` o `,`, prefijo "DEV" y largos;
  - coincidencias con los 7 nombres de la tabla `Departamento`;
  - nunca muestra el valor de `parameter`, nombres, cédulas, correos, sueldos ni fechas;
  - un 401/403 se informa como "la API pide autenticación".
- **`erp-horarios.cmd`/`.ps1` (pendiente 13):** horarios activos de **nuestra** API con un resumen por tipo (M/D).
- **`erp-largos.cmd`/`.ps1` (pendiente 17):** compañías, proyectos y dimensiones, y actividades de los primeros N proyectos (`ERP_N_ACTIVIDADES`, 20 por defecto). Muestra el largo máximo, cuántos exceden 30/30/20 y los 5 Id más largos.
- **`cuerpo-empleados.ejemplo.json`:** exactamente el cuerpo confirmado, en JSON válido y sin comentarios.
- **`.gitignore` de la carpeta:** `resultado-*.txt`, `cuerpo-empleados-*.json` y `simulacion/`.
- **`LEEME.md`:**
  - orden de ejecución;
  - comandos `user-secrets` exactos desde `backend/` para activar y desactivar el modo Http y para la base inválida de A9;
  - reinicio de la API y cómo ver el log Debug;
  - qué pegar en el chat (solo `resultado-*.txt`) y la advertencia de no compartir respuestas crudas ni la salida de `user-secrets list`.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `backend/src/App.Api/Program.cs` | Log del modo del ERP al arrancar |
| `backend/src/App.Infrastructure/Erp/ErpServiceCollectionExtensions.cs` | `ModoEfectivo` (público con `IConfiguration`; interno con las opciones) |
| `backend/src/App.Infrastructure/Erp/CatalogoErpHttp.cs` | Cronómetro y log Debug por llamada |
| `backend/tests/App.Infrastructure.Tests/Erp/CatalogoErpHttpLogTests.cs` (nuevo) | 6 pruebas |
| `backend/tests/manual/tarea26a/` (nuevo) | `comun.ps1`, `conectividad.cmd/.ps1`, `sondeo-empleados.cmd/.ps1`, `erp-horarios.cmd/.ps1`, `erp-largos.cmd/.ps1`, `cuerpo-empleados.ejemplo.json`, `.gitignore`, `LEEME.md` |
| `docs/00_ESTADO_ACTUAL.md` | Fecha; §6 (26 adelantada, dividida en 26a y 26b; SharePoint al final); §7 (13, 17 y 20 "en verificación"; nuevo pendiente 40: contrato) |
| `docs/tareas/TAREA-26a-reporte.md` (nuevo) | Este reporte |

## 4. Pruebas nuevas (6, `CatalogoErpHttpLogTests`)
La prueba del log fue viable con un `ILogger<CatalogoErpHttp>` falso que guarda el nivel y el mensaje formateado:
- **Llamada real:** una sola línea Debug con el formato `^ERP compañías: HTTP 200 en \d+ ms$`, sin el host ni datos de la respuesta. La segunda consulta (en caché) no agrega líneas.
- **Respuesta 500:** Debug con "HTTP 500" más el `Warning` de siempre.
- **`ModoEfectivo`:** 4 casos (`Http`, `http`, `Simulado`, `SIMULADO`).

Las 543 pruebas existentes pasan sin cambios.

## 5. Comandos y simulación
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` (desde `backend/`) | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 549/549 |
| `run26a.cmd <script>` (scratchpad: `SIMULAR=1` + `SIMULACION=<carpeta de respuestas ficticias>`), para los 4 scripts | 4/4 OK (salida 0) |
| Sondeo con casos límite (respuesta que no es JSON, envoltura sin arreglo, solo nulos, código `DEV…`, `2.448`, número frente a texto, 401) | Mensajes correctos, sin mostrar contenido |
| `grep` de los valores sensibles ficticios en `resultado-sondeo.txt` (nombres, apellidos, cédula, correos, teléfono, dirección, fecha, salario, jefe y el `parameter` de prueba) | **0 coincidencias** |
| `grep "ERROR INTERNO" resultado-*.txt` (la protección de `Invocar-Http`) | Sin coincidencias: la función de red nunca se ejecutó |

**Cómo se garantizó que no hubo llamadas de red:**
1. `SIMULAR=1` en todas las ejecuciones (lanzador `run26a.cmd` en el scratchpad).
2. En simulación, `Solicitar` lee archivos y nunca llama a `Invocar-Http`, y esta lanza un error si se invocara.
3. Las URL base se fuerzan a `https://localhost:1`.
4. `Test-NetConnection` no se ejecuta en simulación (lee `tcp_7048.txt` / `tcp_7055.txt`).

Las respuestas ficticias se generaron con `sim26a.py` en el scratchpad: 300 empleados "PERSONA 001…", códigos "900001…" y campos sensibles inventados. No se usó nada de `docs/origen/sharepoint/muestras/`. Al terminar se borraron de la carpeta del repositorio los `resultado-*.txt` y los `cuerpo-empleados-1..3.json` de prueba.

**Errores encontrados al simular y su corrección:**
1. **`codPersona` anidado contado.** El análisis contaba también `jefe.codPersona`. Ahora solo cuenta el campo de primer nivel; lo mismo para la coincidencia de departamentos.
2. **Nulo contado como "(vacio)".** Dentro de `Analizar`, la variable `$texto` era la misma que el parámetro `[string]$Texto` (PowerShell no distingue mayúsculas), así que `$null` se convertía en `""`. Se renombraron el parámetro y la variable.
3. **Excepción sin códigos repetidos.** `Measure-Object -Sum` sobre una lista vacía provocaba `PropertyNotFoundException` con `StrictMode`; se reemplazó por una suma explícita.
4. **Comando no encontrado.** `cmd /c script.cmd` no lo encuentra en la carpeta actual (`NoDefaultCurrentDirectoryInExePath=1`); se usó la ruta completa.

**Salida simulada (valores inventados), extracto de `resultado-sondeo.txt`:**
```
---------------------------------------------------------------- cuerpo-empleados-1.json
Cuerpo: parameter=(vacio), estado="A", codEmpresa=(vacio), codDepartamento=(vacio)
Respuesta: HTTP 200, 0 ms, 192041 bytes
Forma: ARREGLO directo de registros.
Registros: 300
CAMPOS (nombre | tipos | ausentes | nulos | vacios | largo maximo | sensible?)
  codPersona | numero/texto | 0 | 0 | 0 | 7 |
  cedula | texto | 0 | 0 | 0 | 10 | POSIBLE SENSIBLE (no mapear)
  nombres | texto | 0 | 0 | 0 | 11 |
  mailEmpresa | texto | 0 | 0 | 0 | 25 |
  salario | numero | 0 | 0 | 0 | 9 | POSIBLE SENSIBLE (no mapear)
  codUnidad | null | 0 | 300 | 0 | 0 |
  unidad | texto | 0 | 0 | 240 | 15 |
  ...
VALORES DE LA LISTA PERMITIDA (valor = cantidad)
  codEmpresa (2 distintos):
    EF1 = 200
    EF2 = 100
  departamento (4 distintos):
    DEPARTAMENTO DE INFRAESTRUCTURA = 75
    DEPARTAMENTO FICTICIO UNO = 75
    ...
CODIGO EKON (codPersona)
  tipos JSON: numero/texto
  con valor: 300; distintos: 298; codigos repetidos: 2 (registros afectados: 4)
  no numericos: 1; con '.' o ',': 1; empiezan por DEV: 0
  largo minimo: 6; largo maximo: 7 (columna CodigoEkon: 20)
DEPARTAMENTOS de la tabla Departamento (...)
  UNIDAD SISTEMA INTEGRADO DE GESTION = 75
  ...
---------------------------------------------------------------- cuerpo-empleados-2.json
Cuerpo: parameter=(con valor de 10 caracteres; no se muestra), estado="A", codEmpresa="EF1", codDepartamento=(vacio), otro=(no se muestra)
Forma: ENVOLTURA (objeto). Claves: statusCode [numero], isSuccess [booleano], totalRecords [numero], page [numero], result [arreglo]
  meta totalRecords = 2500
  meta page = 1
Registros: 100
ADVERTENCIA: posible paginacion (cantidad redonda o claves de pagina/total). Verificar si faltan registros.
---------------------------------------------------------------- cuerpo-empleados-3.json
Respuesta: HTTP 401, 0 ms, 25 bytes
La API pide autenticacion (401/403): anotar para la 26b. No se muestra el contenido.
```
Las otras salidas simuladas fueron: conectividad (`TCP 7048: CONECTA`, `TCP 7055: NO CONECTA`, `GET list_company: HTTP 200, … bytes`), horarios (tabla y resumen `M: 1 | 540-540 | 480-480`, `D: 1 | 720-720 | 660-660`, `(vacio): 1`) y largos (un proyecto de 34 caracteres y una actividad de 24, con `<-- EXCEDE`, y 2 solicitudes con 404).

## 6. Verificación del usuario — A1–A11
Ver `backend/tests/manual/tarea26a/LEEME.md`. Pegue en el chat solo los `resultado-*.txt` y lo que pide la columna "Anotar".

| # | Caso | Pasos | Esperado | Anotar | ¿Escribe? | Resultado |
|---|---|---|---|---|---|---|
| A1 | Conectividad | `conectividad.cmd` | TCP 7048 y 7055 CONECTA; `list_company` HTTP 200 | `resultado-conectividad.txt` | No | OK |
| A2 | Contrato de EvolutionEmployee | Copiar la plantilla a `cuerpo-empleados-1.json` → `sondeo-empleados.cmd` | HTTP 200 con agregados (o 401/403 si pide autenticación) | `resultado-sondeo.txt` completo: forma, registros, campos, paginación, códigos y departamentos | No | OK |
| A3 | Modo Http | `user-secrets set ServiciosExternos:Modo Http` → reiniciar la API | Línea `ERP: modo Http` en la consola de la API | La línea del log | No | OK |
| A4 | "Nuevo proyecto" con el ERP real | Abrir la pantalla; elegir una compañía real, un grupo, proyecto ERP / actividad o dimensión, y un horario | Listas reales; ningún 500 | Cuántas compañías aparecen; si algún grupo o compañía no carga (mensaje exacto); tiempo aproximado de la primera carga | No | OK: catálogos reales correctos |
| A5 | Horarios y largos | `erp-horarios.cmd` y `erp-largos.cmd` | Archivos con datos | `resultado-horarios.txt` (significado de M/D) y `resultado-largos.txt` (¿algún `EXCEDE`?) | No | OK |
| A6 | Crear un proyecto con datos reales | "Nuevo proyecto" con compañía, proyecto ERP, actividad y horario reales, y un empleado DEV → Registrar | 201 y detalle con los datos ERP reales | Código del proyecto creado | **Sí (proyecto nuevo)** | OK: proyecto **Id 14** creado con datos ERP reales |
| A7 | Degradación con datos de prueba | Id 12 → "Editar datos generales" | Aviso "El horario actual no está activo en el ERP; se conserva si no elige otro." (si su horario no existe en el ERP real); la actividad muestra "No se pudo cargar la lista: …" (compañía 9001 inexistente); cambiar solo el almuerzo → Ver impacto → Registrar | Texto exacto del aviso de horario y del error de actividades; resultado del registro (versión) | **Sí (Id 12)** | OK |
| A8 | Sin 500 en los datos de prueba | Detalle, Historial, "Actualizar personal" (vista previa) y "Reactivar" (si hay un SUSPENDIDO) de los Id 1–13 | Todo funciona como en Simulado; ningún 500 | Cualquier pantalla con error (Id, pantalla y mensaje) | No | OK |
| A9 | 503 controlado | Base inválida (`user-secrets` de `LEEME.md`) → reiniciar → "Nuevo proyecto" | 503 "Servicio ERP no disponible" + "Reintentar". Restaurar las URL y reiniciar | Mensaje mostrado | No | OK |
| A10 | Caché | Con las URL reales, abrir "Nuevo proyecto" dos veces en menos de 5 min | En la consola de la API, líneas `ERP compañías: HTTP 200 en … ms` y `ERP horarios: …` solo la primera vez | Líneas de log de las dos aperturas | No | OK |
| A11 | Volver a Simulado | `user-secrets remove ServiciosExternos:Modo` → reiniciar | Línea `ERP: modo Simulado`; la compañía 9001 vuelve a aparecer | — | No | OK |

## 7. Corrección del sondeo de empleados (A2, 07/10/2026)
### 7.1 Problema
Con los datos reales (envoltura, 1631 registros, 2,5 MB), el script se detuvo en "VALORES DE LA LISTA PERMITIDA" justo después de la primera línea de `area`, con el mensaje "No se pudo analizar la respuesta como JSON (IOException)".

**Causa.** El JSON se había leído bien: los conteos ya estaban impresos. El error fue de **escritura**, y el `catch` que envolvía la lectura y el informe lo presentaba como un error de JSON. Hay dos causas posibles, y se corrigieron las dos:
1. **Consola.** Los `.cmd` ejecutaban `chcp 65001`. Con esa página de códigos, Windows PowerShell 5.1 (.NET Framework) puede lanzar `IOException` al escribir caracteres no ASCII con `Write-Host`. La primera área real se escribió (sin tildes) y la siguiente probablemente tenía tildes o ñ. Es la causa más probable.
2. **Archivo.** `Add-Content` abría y cerraba `resultado-sondeo.txt` en cada línea. Si otro proceso toma el archivo un instante (antivirus, indexador), se produce una `IOException`.

La simulación no reproduce la consola, porque allí la salida va redirigida y no a una consola.

### 7.2 Corrección (solo scripts de `backend/tests/manual/tarea26a/`)
- **`comun.ps1`:**
  - el archivo de resultado se escribe con **un solo `StreamWriter`** (UTF-8, `AutoFlush`), abierto al inicio y cerrado al final (`Cerrar-Salida`, con `try/finally` en el sondeo);
  - un error de escritura en el archivo se relanza como `IOException` con el nombre del archivo;
  - un error de la **consola** no detiene el script: la línea se repite sin tildes (`Sin-Tildes`) y el archivo la recibe completa;
  - `Texto-Linea` muestra los valores en una sola línea: saltos de línea y tabuladores pasan a un espacio y se recortan a 100 caracteres con "... (N caracteres)".
- **`.cmd`:** se quitó `chcp 65001`.
- **`sondeo-empleados.ps1`:**
  - la lectura del JSON y el informe tienen manejos de error separados:
    - JSON inválido → "La respuesta no es JSON valido";
    - error al escribir → "ERROR al escribir resultado-sondeo.txt …" (solo en consola);
    - otro error → "ERROR al generar el informe (tipo, línea del script)", **sin el mensaje de la excepción**, que podría incluir un valor;
  - **lista permitida** ampliada: `estado`, `codEmpresa`/`empresa`, departamento, unidad, área y sección (con sus códigos), `familiaPuesto`, `cargoTipo` y `tipoContrato`, con un **máximo de 30 valores por campo** + "... y N mas";
  - **campos sensibles** (solo el nombre, "POSIBLE SENSIBLE (no mapear)"), además de los anteriores: `barrio`, `callePrincipal`, `calleSecundaria`, `numeroCasa`, `provincia`, `canton`, `reportaA`, `nombresReportaA` (patrón `barrio|calle|numero_?casa|provincia|canton|reporta`).
- **`conectividad`, `erp-horarios` y `erp-largos`:** cierran el archivo al terminar.

### 7.3 Prueba (solo `SIMULAR=1`, datos inventados)
- **JSON simulado:**
  - envoltura `{statusCode, isSuccess, errorMessages: null, result[]}` con **1631** registros;
  - `area` con **120 valores distintos** (tildes, ñ, "Nº", raya larga, comillas dobles y simples, saltos de línea `\n` y `\r\n`, tabulador y un texto de 404 caracteres);
  - `seccion` con "Ó" y "Ñ";
  - `cargoTipo`, `tipoContrato`;
  - campos de dirección y "reporta a" inventados.
- **Resultado:** la salida **termina completa**:
  - `area` muestra 30 valores + "... y 90 mas";
  - los textos con salto de línea salen en una línea;
  - el texto largo se recorta con "... (404 caracteres)";
  - se imprimen la sección de `codPersona` y las coincidencias con los 7 departamentos;
  - los 8 campos nuevos salen como "POSIBLE SENSIBLE (no mapear)";
  - `grep` de los valores sensibles inventados en el resultado: **0**.
- **Clasificación del error:** una prueba local cerró el archivo a mitad de la escritura y el error se capturó como `IOException`, no como un error de JSON.
- **Los otros tres scripts** en simulación: salida 0.
- **Archivos del usuario:** antes de simular, se renombraron temporalmente sus `resultado-*.txt` y su `cuerpo-empleados-1.json`, y después se restauraron intactos (mismo tamaño y fecha). La simulación no los sobrescribió.

**Observación sobre el patrón de sensibles.** Tiene falsos positivos inofensivos: `fechaAntiguedad` (contiene "edad") y `nivelDireccion` (contiene "direcci"). Se marcan para revisión manual; en la 26b el mapeo es una lista explícita de campos permitidos.

## 8. Resultados reales del usuario (solo agregados)
### 8.1 Contrato observado de EvolutionEmployee (A2, 07/10/2026)
- `POST :7048/api/EvolutionEmployee/EmployeesEvolution` con `{ "parameter": "", "estado": "A", "codEmpresa": "", "codDepartamento": "" }`.
- **HTTP 200, sin autenticación**: 2190 ms y 2.564.287 bytes, una sola respuesta.
- **Envoltura:** `statusCode` (número), `isSuccess` (booleano), `errorMessages` (null), `result` (arreglo).
- **1631 registros.** No hay claves de página ni total, y la cantidad no es redonda: no se observa paginación.

**Campos** (tipo JSON, nulos y largo máximo; sin ningún valor de persona). "S" = sensible: **no se mapea** en la 26b.

| Campo | Tipo | Nulos | Largo máx. | Nota |
|---|---|---|---|---|
| `nombrecompleto` | texto | 0 | 41 | Nombre en minúsculas en la clave. → `NombreCompleto` (200) |
| `apellidos` / `nombres` | texto | 0 | 23 / 22 | → `Apellidos` / `Nombres` (120) |
| `codEmpresa` / `empresa` | texto | 0 | 3 / 63 | → `CodEmpresa` (10) / `Empresa` (200) |
| `codPersona` | texto | 0 | 5 | Código EKON → `CodigoEkon` (20). Análisis de duplicados y formato: pendiente de repetir el sondeo corregido |
| `mailEmpresa` | texto | 0 (2 vacíos) | 40 | → `CorreoEmpresa` (256) |
| `telefono` | null/texto | 50 | 14 | **S** |
| `mailPersonal` | texto | 0 (1 vacío) | 44 | **S** |
| `provincia` / `canton` | null/texto | 36 / 1619 | 30 / 6 | **S** (dirección) |
| `cedula` | texto | 0 | 10 | **S** (P10: no mapear hasta que algo la use) |
| `codPosicion` / `posicion` | texto | 0 | 5 / 137 | Sin columna (decidir en la 26b) |
| `codPerfil` | null/texto | 18 | 4 | Sin columna |
| `codPuesto` / `puesto` | texto | 0 | 4 / 69 | → `CodPuesto` (10) / `Puesto` (200); alimenta `CargoInfor` |
| `codDepartamento` / `departamento` | texto | 0 | 5 / 56 | → `CodDepartamento` (10) / `Departamento` (200) |
| `codPosicionJefe` | texto | 0 (1 vacío) | 5 | Sin columna |
| `nombresReportaA` / `cedulaReportaA` / `reportaA` | null/texto | 52 / 52 / 1 | 37 / 10 / 115 | **S** (datos de otra persona) |
| `codFamilia` / `familiaPuesto` | texto | 0 | 5 / 14 | → `FamiliaPuesto` (100) |
| `codSeccion` / `seccion` | texto | 0 | 5 / 64 | → `CodSeccion` (10) / `Seccion` (200) |
| `codArea` / `area` | texto | 0 | 5 / 72 | → `CodArea` (10) / `Area` (200) |
| `codUnidad` / `unidad` | texto | 0 | 5 / 45 | → `CodUnidad` (10) / `Unidad` (200) |
| `fechaNacimiento` | null/texto | 2 | 19 | **S** |
| `fechaAntiguedad` / `fechaIngreso` / `fechaSalida` | texto / texto / null | 0 / 0 / 1631 | 19 / 19 / 0 | No se mapean (no son necesarias) |
| `barrio`, `callePrincipal`, `calleSecundaria`, `numeroCasa` | null/texto | 737 / 735 / 978 / 1215 | 57 / 57 / 49 / 48 | **S** (dirección) |
| `fechaIniContrato` / `fechaFinContrato` / `tipoContrato` | texto / null/texto / texto | 0 / 5 / 0 | 23 / 19 / 34 | No se mapean (`tipoContrato` solo como dato de baja cardinalidad del sondeo) |
| `codNivelDir` / `nivelDireccion` | texto | 0 | 5 / 14 | Organizacional (el patrón la marcó por error) |
| `codCargoTipo` / `cargoTipo` | texto | 0 | 5 / 24 | Sin columna |
| `salario` | número | 0 | 11 | **S** |
| `sexo` | texto | 0 | 6 | **S** |
| `estado` | texto | 0 | 1 | → `EstadoErp` |
| `bpr` | null/número | 1109 | 5 | **S** |

**Observaciones para la 26b:**
- todos los códigos `cod*` caben en las columnas de 10;
- todas las descripciones caben en 200;
- falta repetir el sondeo corregido para obtener los valores permitidos (estado, empresas, departamentos…), el análisis de `codPersona` (duplicados, formato, prefijo DEV) y las coincidencias con los 7 departamentos.

### 8.2 A5 — Largos de los Id del ERP (pendiente 17)
Largo máximo real: **proyecto 9, dimensión 9, actividad 6**, frente a columnas de 30, 30 y 20. Ningún Id excede su columna. **Pendiente 17 cerrado sin cambios:** P4 no requiere validación de largo ni migración.

### 8.3 A5 — Horarios (pendiente 13)
**65 horarios de tipo M y 4 de tipo D.** Con los datos no se puede deducir el significado de M y D. **Pendiente 13 sigue abierto** [PENDIENTE DE CONFIRMAR con el área de nómina].

**Observaciones:**
1. **Horarios no laborales en la lista de horarios activos:** LIBRE, FERIADO, VACIO, VACACIONES, PERMISO MEDICO y CALAMIDAD DOMESTICA. Hoy se pueden elegir como horario de un proyecto. Hay que decidir si se filtran, por ejemplo por descripción o por un indicador del ERP [PENDIENTE DE DECISIÓN DEL USUARIO].
2. **Horarios que cruzan la medianoche** (salida anterior a la entrada). Hay que verificar cómo los tratan `ReglasHorarioAlmuerzo` y RN09 (rangos de almuerzo y regreso > salida), en la 26b o en una tarea propia.

### 8.4 A2 completo con el sondeo corregido (solo agregados)
- **1633 activos** (la primera ejecución, interrumpida, había contado 1631).
- **`codPersona`:** único (0 repetidos), siempre numérico, de 1 a 5 caracteres, **0 con prefijo "DEV"**. Por eso la exclusión de los DEV de la 26b no afecta a ningún empleado real.
- **Departamentos:** los 7 nombres de la tabla `Departamento` coinciden con empleados reales; **"UNIDAD SISTEMA INTEGRADO DE GESTION" = 85**.
- `codEmpresa` del empleado (ECU, SCE…) no es el Id de compañía del ERP de proyectos: no se cruzan (pendiente 43).
- La API trae además `codPerfil`, `posicion` y `cargoTipo` (organizacionales), que no se guardan (pendiente 44).

## 9. Pendientes
- **Pendiente 13** (M/D) abierto: consultar al ERP o a RR. HH.
- **Pendiente 41** (horarios no laborales en las listas) y **pendiente 42** (RN09 con horarios que cruzan la medianoche): TAREA-26c.
- **Pendientes 43** (`codEmpresa` del empleado ≠ compañía del ERP) y **44** (`codPerfil`, `posicion`, `cargoTipo` para Perfiles).
- **TAREA-26b:** sincronización de empleados (ver `TAREA-26b-reporte.md`).
