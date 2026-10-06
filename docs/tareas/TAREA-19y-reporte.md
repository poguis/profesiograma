# TAREA-19y — Principal opcional (pendiente 33)

**Fecha:** 2026-10-06 (Fase A y Fase B)
**Resultado:** implementada; **prueba manual (sección 6) y verificación visual (sección 7) pendientes**. La migración `20261006173252_ParametroExigePrincipal` está **pendiente de aplicar** (pendiente 34).

**Backend**
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: 0 advertencias y 0 errores.
- `dotnet test`: **543/543**. Son 510 anteriores más 33 nuevas. Se aplicaron los 5 ajustes aprobados en pruebas existentes (sección 5.1).
- `has-pending-model-changes` (después de agregar la migración): sin cambios.
- `migrations list`: `Inicial`, `Vistas` y `ActividadVigenteVista` aplicadas; `20261006173252_ParametroExigePrincipal` **(Pending)**.

**Frontend**
- `npm run build` y `npm run lint`: sin errores ni advertencias.
- `npm test`: **128/128** (123 anteriores + 5 nuevas).

**Lo que no se hizo:** `dotnet run`, `npm run dev`, `dotnet ef database update`, SQL que modifique datos, ningún cliente HTTP contra la API, ni comandos git que modifiquen el repositorio. Los scripts manuales se probaron solo en modo simulación (sección 6.4).

## 1. Decisión y respuestas aprobadas
**Decisión del negocio (06/10/2026).** En SharePoint, más de la mitad de los ~50 proyectos no tiene principal, solo backs. Deben migrarse tal cual y seguir siendo editables. Por eso el principal pasa a ser opcional y basta **1 persona (principal o back)**. El parámetro `PROYECTO_EXIGE_PRINCIPAL` (inicial 0; con 1 vuelve C10) controla la regla. Esto revierte en parte C10 / el pendiente 28.

| # | Respuesta aprobada |
|---|---|
| P1 | En la actualización de personal se cuenta el personal resultante = históricas + vigentes enviadas + nuevas (sin eliminadas) |
| P2 | Clave `personal` para "Se requiere al menos 1 persona (principal o back)." |
| P3 (ampliada) | Si ningún principal guardado que permanece es `EsPrincipalInicial` (no hay principales, o el inicial se eliminó), el **primer principal nuevo** del cuerpo se guarda como inicial. Ajusta D6 |
| P4 | Reactivación solo con backs: al menos uno empieza en R; si no, 400 `backs` "Al menos un back debe empezar en la fecha de reactivación (dd/MM/yyyy).". Se corrige `ReactivacionServicio.cs:269` y `ReactivacionAplicar` |
| P5 | El listado muestra "Sin responsable" (texto secundario) cuando `responsable` es null |
| P6 | `advertencias` en la vista previa de creación (backend y tipo TS), mostradas en `VistaPrevia` |
| P7 | `parte2.cmd` con el parámetro en 1 (no escribe) |

## 2. Estado previo (verificado en la Fase A)
- **Creación:** exigía al menos 1 principal (C10, `CrearProyectoValidador`, constante `MinimoPrincipales`).
- **Reactivación:** exigía al menos 1 principal (R5, `ReactivacionValidador`).
- **Actualización de personal:** no tenía mínimo.
- **Motor:** `Generar` ya funcionaba solo con backs (B6b, B6c). `Regenerar` no tenía prueba con solo backs; ahora la tiene (X18).
- **Vista y detalle:** `vwProyectoResumen` devuelve el responsable null con `OUTER APPLY` y sin errores. El detalle no tiene responsable.
- **Fallo encontrado:** la reactivación solo con backs habría lanzado una excepción en `ReactivacionServicio.cs:269` (`c.Plan.Principales[0]`). Está corregido.

## 3. Qué se hizo
**Parámetro**
- `ParametroConfiguracion.HasData` agrega Id 10 `PROYECTO_EXIGE_PRINCIPAL` = '0' (`BOOL`).
- Migración solo de datos `20261006173252_ParametroExigePrincipal` (`InsertData`/`DeleteData`, sin cambio de esquema) y script idempotente `backend/sql/ef/003_parametro_exige_principal.sql`.
- **Lectura:** `DatosReferenciaProyecto.ConsultaLimites` incluye la clave; `LeerExigePrincipal` interpreta "1" o "true" (sin importar mayúsculas) como true y cualquier otro valor, o la ausencia, como false. El valor queda en `LimitesProyecto.ExigePrincipal` (último parámetro, por defecto false).
- **Exposición:** `OpcionesFormularioProyectoDto.ExigePrincipal` (GET `opciones-formulario`) y `LimitesEdicionDto.ExigePrincipal` (GET de edición y de reactivación).

**Regla común**
- `MinimoPersonal` (Application/Proyectos/Crear) define `Validar(principales, backs, exigePrincipal)`, los mensajes, las claves y la advertencia "El proyecto no tendrá principal: el responsable quedará vacío.".

**Por flujo**
- **Creación:**
  - `CrearProyectoValidador` usa `MinimoPersonal` (principales + backs).
  - `PrevisualizacionDto` agrega `Advertencias`; `CrearProyectoServicio` pone la advertencia cuando no hay principales.
- **Actualización de personal:**
  - `EdicionPersonalValidador.Validar` aplica el mínimo sobre el resultante (P1), con plan válido.
  - `CalculoPersonal.ClaveInicialNueva` y `QuedaSinResponsable` implementan P3 y la advertencia.
  - `CambioPersonal.ClaveInicialNueva` es un parámetro nuevo opcional.
  - `EdicionPersonalRepositorio`: `EsPrincipalInicial = n.Clave == (Reactivacion?.ClavePrincipalInicial o ClaveInicialNueva)`.
- **Reactivación:**
  - `ReactivacionValidador` (R5/R6): mínimo por parámetro; con principales, el primero en R; solo con backs, alguno en R.
  - `ReactivacionServicio`: la clave del inicial es `Principales.FirstOrDefault()?.Clave`. La advertencia sale si no hay principales nuevos ni inicial histórico.
  - `ReactivacionAplicar.ClavePrincipalInicial` pasa a `string?`.

**Frontend**
- **`formularioProyecto.ts`:**
  - `ayudaPersonal(estado, exigePrincipal)` reemplaza a `ayudaPrincipales`. Con 0 y sin nadie: "Agrega al menos 1 persona (principal o back) para generar la vista previa."; con 1 y sin principales: la ayuda de P6.
  - `puedeGenerarVistaPrevia(estado, enviando, exigePrincipal)`.
- **`NuevoProyecto.tsx`:** usa `opciones.exigePrincipal`.
- **`VistaPrevia.tsx`:** muestra `advertencias` (MessageBar warning).
- **`TablaProyectos.tsx`:** "Sin responsable" en texto secundario.
- **`tipos.ts`:** `advertencias`, `exigePrincipal` (opciones y `LimitesEdicion`).

## 4. Archivos
| Capa | Archivo | Cambio |
|---|---|---|
| Application | `Proyectos/Crear/MinimoPersonal.cs` (nuevo) | Regla, mensajes y advertencia |
| Application | `Crear/CrearProyectoContratos.cs` | `LimitesProyecto.ExigePrincipal` |
| Application | `Crear/CrearProyectoValidador.cs` | Mínimo por parámetro (doc. de `MinimoPrincipales`) |
| Application | `Crear/CrearProyectoDtos.cs`, `Crear/CrearProyectoServicio.cs` | `PrevisualizacionDto.Advertencias` y advertencia sin principal |
| Application | `Crear/OpcionesFormularioProyecto.cs` | `ExigePrincipal` |
| Application | `Personal/EdicionPersonalValidador.cs` | Mínimo sobre el resultante (P1) |
| Application | `Personal/CalculoPersonal.cs` | `ClaveInicialNueva`, `QuedaSinResponsable` (P3) |
| Application | `Personal/EdicionPersonalContratos.cs` | `ReactivacionAplicar.ClavePrincipalInicial` anulable; `CambioPersonal.ClaveInicialNueva` |
| Application | `Personal/EdicionPersonalDtos.cs`, `Personal/EdicionPersonalServicio.cs` | `LimitesEdicionDto.ExigePrincipal`; advertencia; P3 |
| Application | `Reactivacion/ReactivacionValidador.cs`, `Reactivacion/ReactivacionServicio.cs` | R5/R6 con solo backs; corrección `Principales[0]`; advertencia |
| Infrastructure | `Consultas/DatosReferenciaProyecto.cs` | Clave en `ConsultaLimites`; `LeerExigePrincipal` |
| Infrastructure | `Persistencia/Configuraciones/ParametroConfiguracion.cs` | `HasData` Id 10 |
| Infrastructure | `Persistencia/Proyectos/EdicionPersonalRepositorio.cs` | `EsPrincipalInicial` con P3 |
| Infrastructure | `Persistencia/Migraciones/20261006173252_ParametroExigePrincipal.cs` + `.Designer.cs` (nuevos), `ProfesiogramaDbContextModelSnapshot.cs` | Migración solo de datos |
| SQL | `backend/sql/ef/003_parametro_exige_principal.sql` (nuevo) | Script idempotente (no ejecutado) |
| Tests | `Proyectos/PrincipalOpcionalTests.cs` (nuevo) | 25 pruebas (sección 5.2) |
| Tests | `App.Domain.Tests/.../RegeneracionCronogramaTests.cs` | X18 (`Regenerar` solo con backs) |
| Tests | `App.Infrastructure.Tests/Consultas/DatosReferenciaProyectoSqlTests.cs`, `ConsultasExistentesSqlTests.cs` | `LeerExigePrincipal` (7 casos); ToQueryString con la clave nueva |
| Tests | `Crear/CrearProyectoValidadorTests.cs`, `Personal/EdicionPersonalServicioTests.cs`, `Reactivacion/ReactivacionServicioTests.cs` | Ajustes aprobados (sección 5.1) |
| Manual | `backend/tests/manual/tarea19y/parte1.cmd`, `parte2.cmd`, `e-crear.json`, `e-vacio.json`, `parametro-exige-principal.sql` (nuevos) | Prueba manual |
| Frontend | `formularioProyecto.ts`, `pages/NuevoProyecto.tsx`, `components/VistaPrevia.tsx`, `components/TablaProyectos.tsx`, `tipos.ts` | Sección 3 |
| Frontend | `formularioProyecto.test.ts`, `formularioProyecto.envio.test.ts` | Ajustes aprobados y pruebas nuevas |
| Docs | `FASE_5_Crear_Proyecto.md` (P1, §11, §12 nuevo), `FASE_5_Edicion_Cronograma.md` (§8.3 D6, §10 nuevo), `FASE_5_Estados_Proyecto.md` (R5/R6, §10 nuevo), `00_ESTADO_ACTUAL.md` | Documentación |

## 5. Pruebas
### 5.1 Pruebas existentes ajustadas (aprobadas)
Ninguna otra prueba cambió de resultado. Al cambiar el código, solo fallaron (o no compilaron) estas.

| Prueba | Motivo | Ajuste |
|---|---|---|
| `CrearProyectoValidadorTests.SinPrincipales_400_C10MinimoUno` | Con el parámetro en 0, el error pasa a `personal` | Preparación con `ExigePrincipal: true`; la aserción no cambia |
| `ReactivacionServicioTests.SinPrincipales_400` | Ídem (R5) | Preparación con `ExigePrincipal: true` (parámetro opcional `datos` en su `Crear`); la aserción no cambia |
| `EdicionPersonalServicioTests` (P1, línea 53) y `ReactivacionServicioTests` (GET, línea 63) | `LimitesEdicionDto` tiene un campo más | Esperado `new LimitesEdicionDto(20, 20, 20, false)` |
| `ConsultasExistentesSqlTests.DatosReferencia_…` | `ConsultaLimites` modificada | Aserción agregada: el SQL contiene `PROYECTO_EXIGE_PRINCIPAL` |
| Frontend `formularioProyecto.test.ts` (4 de P6) | Firma nueva | Pasan `exigePrincipal = true`; `ayudaPrincipales` → `ayudaPersonal`; mismos resultados |
| Frontend `formularioProyecto.envio.test.ts` | Tipo `Previsualizacion` con `advertencias` | Fixture con `advertencias: []` |

`OpcionesFormularioProyectoServicioTests` no compara el DTO completo: no cambió.

### 5.2 Pruebas nuevas
**Backend (33)**

| Grupo | Casos |
|---|---|
| `MinimoPersonal` (teoría, 5) | 0p+1b y 1p+0b válidos con 0; nada → `personal`; 1p válido con 1; solo backs → `principales` con 1 |
| Creación (5) | Parámetro en 0: solo backs válido (back sin relación); 1 principal sin backs válido; sin nadie → `personal`. Parámetro en 1: solo backs → `principales`. Vista previa solo con backs → advertencia y sin cruces; con principales, sin advertencia |
| Personal (9) | Parámetro en 0: quitar todas sin históricas → `personal`; con históricas → válido y con advertencia; quitar el único principal dejando backs → válido y con advertencia; con inicial → sin advertencia. Parámetro en 1: sin principales en el resultante → `principales`. P3: solo backs + nuevo → inicial; principales sin inicial + nuevo → inicial; inicial eliminado + nuevo → inicial; con inicial guardado → el nuevo no es inicial |
| Reactivación (6) | Parámetro en 0: solo backs con uno en R → válido, registro sin excepción y `ClavePrincipalInicial` null; sin inicial histórico → advertencia; ninguno en R → `backs`; sin nadie → `personal`. Parámetro en 1: solo backs → `principales`. Con principal → "p1" inicial, como antes |
| Dominio (1) | X18: `Regenerar` solo con backs (vigente + nuevo), sin cruces |
| Infrastructure (7) | `LeerExigePrincipal`: "1", "true", " TRUE " → true; "0", "false", "SI", null → false |

**Frontend (5):**
- con el parámetro en 1, solo backs → bloqueado y ayuda de P6;
- con el parámetro en 0: sin nadie → bloqueado y ayuda nueva; solo backs → permitido; solo principal → permitido; el back se envía con `principalRelacionado` null.

**Errores durante la implementación**
- Fallaron 2 pruebas de las mías, por datos de prueba y no por el código:
  - en X18 esperé el bloque 1 y el motor asigna el bloque 2 al back 2, igual que `Generar`;
  - en una prueba de reactivación usé un proyecto doble con días base de un principal que había quitado del personal (KeyNotFound): se usó P1 sin la marca de inicial.
- Hubo 1 advertencia CS8602 en una prueba nueva: corregida.

## 6. Prueba manual (la ejecuta el usuario) — [PENDIENTE]
**Antes de empezar**
1. Aplicar la migración (pendiente 34), desde `backend/`:

```
dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --configuration Release
```

   Otra opción es `backend/sql/ef/003_parametro_exige_principal.sql` en SSMS. Sin la migración, la parte 1 funciona igual, porque el parámetro ausente vale 0. La parte 2 sí la necesita.
2. Con la API corriendo, ejecutar desde `backend\tests\manual\tarea19y\`:
   - `parte1.cmd`, con el parámetro en 0;
   - en SSMS, el bloque 1 de `parametro-exige-principal.sql` (Valor = '1');
   - `parte2.cmd`;
   - el bloque 2 (Valor = '0').

### 6.1 `parte1.cmd` (escribe: crea E y registra 1 actualización de personal en E)
Fechas relativas a hoy (H): E de H+20 a H+40; back 1 de H+20 a H+25; back 2 de H+30 a H+35.

**Protección:** si existe `resultado-parte1.txt`, no se ejecuta; el archivo se crea justo antes de c.

**Elección de empleados:** como en la TAREA-17b.
- Primero DEV007 / DEV008, luego los demás pares de DEV001 a DEV008.
- O se indican por argumento o en `empleados.txt` (EMP_K1= / EMP_K2=).
- Cada par se prueba con la vista previa (no guarda) y se usa el primero sin cruces y con la advertencia.

| Paso | Solicitud | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|
| a | Vistas previas de creación solo con 2 backs (por par) | 200, sin cruces y con "El proyecto no tendrá principal: el responsable quedará vacío." | No | [PENDIENTE] |
| b | Vista previa de creación sin nadie | 400 `personal` "Se requiere al menos 1 persona (principal o back)." | No | [PENDIENTE] |
| c | **Crear E** solo con backs ("Sin relación") | 201 | **Sí (crea E)** | [PENDIENTE] |
| d1 / d2 | GET detalle de E; GET listado con el código de E | 2 BACK y ningún PRINCIPAL (ningún inicial); `responsable` null | No | [PENDIENTE] |
| e2 | Vista previa de personal quitando a todos | 400 `personal` | No | [PENDIENTE] |
| e3 | Vista previa: el back 2 termina un día antes | 200 sin cruces, con `versionProyecto` | No | [PENDIENTE] |
| e4 | **Registrar** e3 con el token | 200, versión 2 | **Sí (E)** | [PENDIENTE] |
| f | GET detalle de E | Back 2 acortado; etapas v1 CREACION y v2 ACTUALIZACION_PERSONAL | No | [PENDIENTE] |
| g2 | Reactivación del Id 2: vista previa solo con un back (empleado del back 1) desde R = `fechaMinima` | 200 sin errores | No | [PENDIENTE] |
| g3 | Ídem con el back desde R+1 | 400 `backs` "Al menos un back debe empezar en la fecha de reactivación (…)." | No | [PENDIENTE] |

### 6.2 `parte2.cmd` (parámetro en 1; no escribe; se puede repetir)
| Paso | Solicitud | Esperado | Resultado |
|---|---|---|---|
| h0 | GET `opciones-formulario` | `exigePrincipal` true. Si no: termina sin enviar nada más | [PENDIENTE] |
| h1 | Vista previa de creación solo con los backs de la parte 1 | 400 `principales` "Se requiere al menos 1 principal(es)." | [PENDIENTE] |
| h2 | Vista previa de reactivación del Id 2 solo con un back desde R | 400 `principales` | [PENDIENTE] |

### 6.3 SQL del parámetro (`parametro-exige-principal.sql`, lo ejecuta el usuario en SSMS)
Para poner el parámetro en 1 antes de `parte2.cmd`:

```sql
UPDATE dbo.Parametro SET Valor = '1' WHERE Clave = 'PROYECTO_EXIGE_PRINCIPAL';
```

Para volver a 0 después:

```sql
UPDATE dbo.Parametro SET Valor = '0' WHERE Clave = 'PROYECTO_EXIGE_PRINCIPAL';
```

La API lee el parámetro en cada petición, así que no hace falta reiniciarla.

### 6.4 Prueba de la lógica de los scripts (Claude, solo simulación)
**Cómo se ejecutó**
- Copias de los scripts en la carpeta temporal de la sesión, con `SIMULAR=1`, BASE `https://localhost:1`, `HOY=2026-10-06` y un PATH sin System32 (solo PowerShell y una copia de `chcp.com`). Así `curl.exe` no se podía ejecutar.
- Las respuestas simuladas salieron de archivos generados con `sim19y.py`.
- **No hubo ninguna llamada a la API.**

| Escenario | Resultado |
|---|---|
| normal | Par DEV007/DEV008. b y e2 400 `personal` "OK"; c crea E; d2 responsable null; e3 token 1; e4 200 versión 2; f back 2 hasta H+34; g2 200; g3 400 `backs`; parte 2: h0 True, h1 y h2 400 `principales` "OK" |
| segunda ejecución | "Ya existe resultado-parte1.txt…", salida 1 |
| descarta | El par por defecto tiene cruces: se descarta y se elige DEV001/DEV002 |
| ninguno | Los 56 pares tienen cruces: "NO se creo nada", sin `resultado-parte1.txt` |
| exige0 | parte 2: `exigePrincipal` False → "no esta en 1" y no envía h1 ni h2 |

**Cuerpos JSON generados:** se revisaron. La creación lleva `principales: []` y los backs con `principalRelacionado: null`; e4 lleva el token `versionProyecto`; g2 y g3 llevan `principales: []` y el back desde R o desde R+1.

## 7. Verificación visual (usuario) — [PENDIENTE]
| # | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|
| Y1 | "Nuevo proyecto" con el parámetro en 0, sin nadie | "Generar vista previa" deshabilitado; ayuda neutra "Agrega al menos 1 persona (principal o back) para generar la vista previa." bajo Principales y en la barra | No | [PENDIENTE] |
| Y2 | Ídem, agregando solo un back (relación "Sin relación") | La ayuda desaparece; la vista previa muestra la advertencia "El proyecto no tendrá principal: el responsable quedará vacío." | No | [PENDIENTE] |
| Y3 | Registrar Y2 | 201 y detalle; en el listado, la columna Responsable muestra "Sin responsable" en gris | **Sí (proyecto nuevo)** | [PENDIENTE] |
| Y4 | Con el parámetro en 1 (bloque 1 del SQL), "Nuevo proyecto" con solo un back | Botón deshabilitado y ayuda "Agrega al menos 1 principal para generar la vista previa."; luego volver a 0 | No | [PENDIENTE] |
| Y5 | Listado: proyecto E de la parte 1 | "Sin responsable" | No | [PENDIENTE] |

## 8. Contradicciones y observaciones
1. **C10 / pendiente 28 se revierte en parte** por decisión del negocio. Queda documentado en `CrearProyectoValidador`, `MinimoPersonal`, `FASE_5_Crear_Proyecto.md` §11–§12 y `00_ESTADO_ACTUAL.md`.
2. **"Sin responsable" no significa solo "sin principal".** También aparece cuando hay principales pero ninguno inicial (por ejemplo, si un recorte eliminó al inicial). Con P3, la siguiente edición que agregue un principal lo vuelve inicial.
3. **Advertencia de la edición.** Se muestra en toda vista previa de personal de un proyecto que ya está sin responsable (por ejemplo, los migrados solo con backs), aunque la edición no cambie eso. Es informativa y no bloquea.
4. **Texto de R7 sin cambios.** La advertencia del GET de reactivación "El proyecto no tiene principales; agrega uno." se mantiene, como se pidió, aunque ahora el principal sea opcional.
5. **Mínimo de la edición solo con plan válido.** En la actualización de personal, el mínimo se comprueba cuando el resto del cuerpo es válido (necesita el plan para contar históricas, vigentes y nuevas). Si hay otros errores, este aparece al corregirlos.
6. **Id 10 de la migración.** La migración inserta el parámetro con Id 10. Si alguien hubiera agregado a mano un parámetro con ese Id en PROFESIOGRAMA_DEV, la migración fallaría. Las semillas actuales llegan al Id 9.

## 9. Pendientes
- **Pendiente 34:** aplicar la migración `ParametroExigePrincipal` (la aplica el usuario).
- Prueba manual (sección 6) y verificación visual Y1–Y5 (sección 7).
- La 19b (personal) y la 19c (reactivación) deben usar `limites.exigePrincipal`, la clave `personal`, P3 y la advertencia en sus pantallas.
