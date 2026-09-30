# TAREA-10 — Motor de cronograma y cruces internos (Domain) + pruebas unitarias

**Fecha:** 2026-09-30
**Resultado:** completada.
- `dotnet build -c Release`: **0 advertencias, 0 errores**.
- `dotnet test`: **25 de 25 correctas**.
- No se tocaron la base de datos, la API ni el frontend. No se modificaron `App.Infrastructure`, `App.Api`, `frontend/` ni migraciones.
- No se ejecutaron `dotnet run`, SQL ni comandos git que modifiquen el repositorio.

## 1. Incidencia inicial y cómo se resolvió
- Al iniciar la Fase A, **`docs/fases/FASE_5_Crear_Proyecto.md` no existía** en el repositorio ni en `D:\PROYECTOS` / `D:\temp`. La tarea se detuvo sin inventar reglas.
- El usuario copió el documento, con la sección 11 de decisiones.
- El usuario confirmó que la referencia es `docs/origen/powerapps/GestionProyecto.pa.yaml` (con punto; la tarea decía `_pa.yaml`), y que la versión vigente de cada fórmula es la que queda fuera de `/* ... */`.

## 2. Fase A — Contraste documento ↔ app original
Se extrajeron las fórmulas vigentes, sin los bloques comentados, de los botones **Generar** (línea 1040) y **Registrar** (línea 1902). **No hay contradicciones con FASE_5 §5–§6.**

| Regla | App original (fórmula vigente) | Motor |
|---|---|---|
| §5.1 Principal | `RoundUp(total/ciclo)` bloques; fechas **de la persona**; fin de bloque = `min(…, p.FechaFin)` | Igual |
| §5.2 Back | DESCANSO si `TipoRegistro = "Descanso"`, si no BACK; MANUAL; bloque = `BackId`; descanso posterior solo si es Jornada y `DiasDescanso > 0`, sin recortar | Igual (P3) |
| §6 Cruces internos | Sobre `colAsignacionesDiarioTmp`: sin descansos automáticos y sin deduplicar; ignora DESCANSO; cruce si hay más de 1 registro | Igual (D1) |
| §5.3 Descansos automáticos (Registrar) | Primer día siguiente libre en `colAsignacionesDiario` (días base), hasta la fecha fin del principal, mismo bloque | Igual (P4, D3) |
| §5.4 Deduplicación (Registrar) | `Distinct(CLAVE)` con proyecto\|EKON\|fecha\|rol: la primera aparición | Igual |

### Decisiones aprobadas
| # | Decisión |
|---|---|
| D1 | `DiasBase` sin deduplicar; los cruces se calculan ahí (la misma persona como dos principales con días solapados **es cruce**). `DiasFinales` se deduplica |
| D2 | `Tramos` incluye los tramos DESCANSO/AUTO al final (para la vista previa) |
| D3 | El "primer día libre" se revisa contra `DiasBase` (cualquier rol de la persona) |
| D4 | El motor usa las fechas de cada persona. Del proyecto solo verifica `fin >= inicio`; RN08 (personal dentro del rango) queda para Application |
| D5 | Orden por generación: principales → backs (tramo, luego descanso posterior) → descansos automáticos. Sin ordenar por fecha |
| D6 | `ArgumentException` para fin < inicio (proyecto, principal o back) y `DiasTrabajo = 0`; `ArgumentNullException` para null. No valida duplicados ni reglas de usuario |
| xUnit | **Opción A:** `xunit.v3` 4.0.1 + `"test": { "runner": "Microsoft.Testing.Platform" }` en `backend/global.json` |

### Por qué se modificó `global.json` (verificación de xUnit)
Se probó en un proyecto desechable del scratchpad, fuera del repositorio:

| Combinación | Resultado |
|---|---|
| xunit.v3 4.0.1 + xunit.runner.visualstudio 4.0.0 + Microsoft.NET.Test.Sdk 18.10.1 (VSTest) | ❌ `Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later` (xunit.v3 4.x usa MTP v2) |
| **xunit.v3 4.0.1 + `global.json` con `test.runner = Microsoft.Testing.Platform`** | ✅ (elegida) |
| xunit.v3 3.2.2 + runner.visualstudio 3.1.5 + Test.Sdk 18.10.1 (VSTest) | ✅ (alternativa descartada) |

Consecuencias:
- Con MTP no se necesitan `Microsoft.NET.Test.Sdk` ni `xunit.runner.visualstudio`.
- El comando pasa a ser `dotnet test --solution Profesiograma.slnx -c Release`.
- Sin `Microsoft.NET.Test.Sdk` no se agrega el `using Xunit` global, así que se declara `<Using Include="Xunit" />` en el `.csproj`.

## 3. Qué se hizo

### Domain (`backend/src/App.Domain/Proyectos/Cronograma/`, sin NuGet)
| Archivo | Contenido |
|---|---|
| `Enumeraciones.cs` | `RolCronograma : byte` (Principal = 1, Back = 2, Descanso = 3 = `CatalogoIds.RolAsignacion`), `TipoAsignacionCronograma` (Auto, Manual), `TipoRegistroBack` (Jornada, Descanso) |
| `Entradas.cs` | `PrincipalEntrada(short Numero, int EmpleadoId, DateOnly Inicio, DateOnly Fin, byte DiasTrabajo, byte DiasDescanso)`, `BackEntrada(short Numero, int EmpleadoId, DateOnly Inicio, DateOnly Fin, TipoRegistroBack TipoRegistro, byte DiasDescanso, short? PrincipalRelacionado = null)`, `SolicitudCronograma(DateOnly InicioProyecto, DateOnly FinProyecto, IReadOnlyList<PrincipalEntrada>, IReadOnlyList<BackEntrada>)` |
| `Salidas.cs` | `PersonaProyecto(RolCronograma Rol, short Numero)` (struct), `Tramo(Rol, Tipo, short Bloque, Persona, EmpleadoId, Inicio, Fin)` + `Dias`, `DiaAsignado(EmpleadoId, Fecha, Rol, Tipo, Bloque, Persona)`, `CruceInterno(EmpleadoId, Fecha, IReadOnlyList<PersonaProyecto> Involucrados)`, `ResultadoCronograma(Tramos, DiasBase, DiasFinales, CrucesInternos)` + `TieneCrucesInternos` |
| `MotorCronograma.cs` | `static ResultadoCronograma Generar(SolicitudCronograma)`: §5.1 → §5.2 → cruces sobre la base (§6) → §5.3 (P4) → §5.4 (deduplicación con `HashSet`, conserva el primero). No lee la fecha del sistema |
| `CruceDetector.cs` | `static IReadOnlyList<CruceInterno> DetectarInternos(IEnumerable<DiaAsignado>)`: ignora DESCANSO, agrupa por (EmpleadoId, Fecha), cruce si hay más de 1, ordenado por fecha y empleado |

Tipos alineados con las entidades (`short` Numero y Bloque, `byte` días) para que la TAREA-12 los mapee directo a `ProyectoPersonal` y `ProyectoAsignacionDia`. `App.Domain.csproj` no se modificó: sigue sin paquetes.

### Pruebas (`backend/tests/App.Domain.Tests/`)
| Archivo | Contenido |
|---|---|
| `App.Domain.Tests.csproj` | `OutputType Exe`, `IsTestProject`, `xunit.v3` 4.0.1, `<Using Include="Xunit" />`, referencia a App.Domain. `net10.0`/nullable desde `Directory.Build.props` |
| `Proyectos/Cronograma/Cron.cs` | Atajos: `F("yyyy-MM-dd")`, `Principal(...)`, `Back(...)`, `Solicitud(...)` (proyecto 01–31/10/2026 por defecto), `T(...)` (tramo), `D(...)`/`Dias(...)` (días esperados) |
| `Proyectos/Cronograma/MotorCronogramaTests.cs` | E1–E6, E10, B1–B7 (20 pruebas) |
| `Proyectos/Cronograma/CruceDetectorTests.cs` | E7–E9 (a través del motor) + 2 pruebas directas del detector |

**Cada prueba compara la secuencia completa** de `Tramos` y `DiasFinales` (y `DiasBase` donde aplica) con la esperada, elemento por elemento: fecha, rol, tipo, bloque, persona y empleado. Los records tienen igualdad por valor. No se limitan a conteos.

### Otros cambios
- `backend/Profesiograma.slnx`: carpeta `/tests/` con `tests/App.Domain.Tests/App.Domain.Tests.csproj`.
- `backend/global.json`: sección `"test": { "runner": "Microsoft.Testing.Platform" }`.
- `CLAUDE.md` ("Compilar"): `dotnet test --solution Profesiograma.slnx -c Release`.

## 4. Casos probados (año 2026)
| Prueba | Entrada | Resultado verificado |
|---|---|---|
| E1 | Principal TIPO_2 (11/4) 01–31/10 | PRINCIPAL/AUTO b1 01–11, b2 16–26, b3 31; DESCANSO/AUTO b1 12–15, b2 27–30; nada el 01/11 |
| E2 | TIPO_1 (22/8) 01–10/10 | 1 bloque 01–10; sin descanso |
| E3 | ESPECIAL (3/0) 01–07/10 | 01–03, 04–06, 07; sin descansos |
| E4 | Back JORNADA 12–15/10, 2 días | BACK/MANUAL 12–15; DESCANSO/MANUAL 16–17 (bloque 1, persona Back 1) |
| E5 | Back DESCANSO 12–15/10 (DiasDescanso 2) | DESCANSO/MANUAL 12–15; nada el 16–17 |
| E6 | E1 + la misma persona como back JORNADA 12/10 | Sin DESCANSO/AUTO del 12 al 15; sí 27–30; BACK el 12; sin cruce |
| E7 | Principal emp 1 + back emp 2 el 05/10 | Sin cruces; ambos días presentes |
| E8 | Principal emp 1 + back emp 1 JORNADA el 05/10 | 1 cruce: emp 1, 05/10, [Principal 1, Back 1]; ambos días conservados |
| E9 | Principal emp 1 + back emp 1 DESCANSO el 05/10 | Sin cruce; PRINCIPAL y DESCANSO/MANUAL conservados |
| E10 | TIPO_2 01–01/10 (proyecto de 1 día) | 1 bloque de 1 día |
| B1 | TIPO_3 (5/2) 05–18/10 (2 ciclos exactos) | b1 05–09, DESC 10–11, b2 12–16, DESC 17–18 (14 días) |
| B2 | P1 emp 1 TIPO_2 01–31 + P2 emp 2 TIPO_3 01–14 | P2: 01–05, 08–12 + DESC 06–07, 13–14; P1 como E1; sin cruces |
| B3 | E1 + back DESCANSO de la misma persona 14–15/10 | DESCANSO/AUTO solo 12–13; **una sola fila** el 14 y el 15 (DESCANSO/MANUAL del back) |
| B4 | Back JORNADA 28–31/10, 3 días | BACK 28–31; DESCANSO/MANUAL **01–03/11 completo** (P3) |
| B5 (×5) | Proyecto fin < inicio · principal fin < inicio · back fin < inicio · DiasTrabajo = 0 · solicitud null | `ArgumentException` ×4 · `ArgumentNullException` |
| B6a–c | Principal sin backs · sin principales ni backs · sin principales con 1 back | Solo días del principal (31) · todo vacío sin error (P1) · solo días del back |
| B7 | La misma persona como P1 y P2, TIPO_2 01–31 | `DiasBase` 46 (duplicados); 23 cruces [P1, P2] (uno por día de trabajo); `DiasFinales` = días de P1 + descansos de P1 |
| Detector | Lista mixta / vacía | Ignora DESCANSO, agrupa por persona y fecha, ordena por fecha y empleado; vacío → sin cruces |

## 5. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| Extracción con Python de las fórmulas vigentes de Generar y Registrar (solo lectura) | Reglas confirmadas (sección 2) |
| `curl` a nuget.org (versiones y `.nuspec` de xunit.v3, runner, Test.Sdk) | xunit.v3 4.0.1 (net8.0 → MTP v2), runner 4.0.0, Test.Sdk 18.10.1 |
| `dotnet test` en proyectos desechables del scratchpad (3 combinaciones) | Ver sección 2 |
| `dotnet build Profesiograma.slnx -c Release` | **0 advertencias, 0 errores** |
| `dotnet test --solution Profesiograma.slnx -c Release` | **25/25 correctas** (resumen abajo) |
| Mutaciones temporales del motor (restaurado y verificado con `cmp`) | 1) Sin verificar el primer día ocupado (P4) → **falla E6**. 2) Recortar el descanso del back al fin del proyecto (P3) → **falla B4**. Las pruebas detectan esas regresiones |
| `dotnet test … --list-tests` | 25 pruebas descubiertas (nombres en la sección 4) |

### Resumen completo de `dotnet test` (ejecución final)
```
Ejecutando pruebas desde D:\GitHub\profesiograma\backend\tests\App.Domain.Tests\bin\Release\net10.0\App.Domain.Tests.dll (net10.0|x64)
D:\GitHub\profesiograma\backend\tests\App.Domain.Tests\bin\Release\net10.0\App.Domain.Tests.dll (net10.0|x64) correcto (1s 308ms)
Resumen de la serie de pruebas: Correcta!
  total: 25
  error: 0
  correcto: 25
  omitido: 0
  duración: 1s 883ms
```
| Total | Correctas | Fallidas | Omitidas |
|---|---|---|---|
| **25** | **25** | **0** | **0** |

## 6. Errores y correcciones
- **Referencia faltante:** `FASE_5_Crear_Proyecto.md` no existía al inicio. Se detuvo la tarea y el usuario copió el documento.
- **xUnit 4.x + VSTest** incompatible con el SDK .NET 10. Se resolvió con la opción A (MTP en `global.json`), aprobada por el usuario.
- En la prueba desechable de la opción A faltó `using Xunit` (sin Test.Sdk no hay using global). En el proyecto real se resolvió con `<Using Include="Xunit" />`.
- El código del motor y las pruebas no tuvieron errores de compilación ni fallos.

## 7. Pendientes
- **TAREA-11:** `ICatalogoErp` (Http + Simulado), endpoints ERP y `GET /api/empleados`.
- **TAREA-12:**
  - caso de uso `CrearProyecto`: mapear `PrincipalEntrada`/`BackEntrada` desde el DTO, aplicar RN08/RN18/P1 en el validador y convertir `DiasFinales` en `ProyectoAsignacionDia` (`RolCronograma` → `RolAsignacionId`, `TipoAsignacionCronograma` → `"AUTO"`/`"MANUAL"`, `PersonaProyecto` → `ProyectoPersonalId`);
  - cruces externos y transacción.
- Test Explorer de Visual Studio con MTP requiere VS 2022 17.14 o superior (VS 2026).
- `App.Domain.csproj` conserva el comentario "(se completa en Fase 5)"; no se cambió.
