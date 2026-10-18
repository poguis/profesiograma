# TAREA-26c — Horarios del ERP: no laborales (pendiente 41) y turno nocturno (pendiente 42)

**Fecha:** 2026-10-16 (Fase A: plan; B1: evidencia; B2: implementación)
**Resultado:** implementada; **pendiente de la verificación del usuario** (`backend/tests/manual/tarea26c/verificacion.cmd`, después de aplicar la migración).
- `dotnet build Profesiograma.slnx -c Release`: 0 advertencias, 0 errores.
- `dotnet test --solution Profesiograma.slnx -c Release`: **691/691** (607 anteriores: 2 cambiadas por el mensaje nuevo; 84 nuevas).
- Frontend:
  - `npm test`: **238/238**;
  - `npm run build`: OK;
  - `npm run lint`: OK (0 avisos).
- Migración `ParametroHorariosNoLaborales`: **solo datos** (un `InsertData` en `Parametro`). **Sin cambios de esquema.**

**Lo que no se hizo:**
- llamadas a la API o al ERP;
- `dotnet ef database update` (lo aplica el usuario);
- SQL;
- `dotnet run` / `npm run dev`;
- comandos git que modifiquen el repositorio.

## 1. Evidencia (B1, resumen; datos de catálogo)
Fuente: `resultado-horarios-analisis.txt` del usuario, resumido por él en el chat; Claude Code no lo leyó (ver §6.1).
- 72 horarios activos.
- 11 cruzan la medianoche.
- 0 con "2400".
- `JORNADA_DIF` = 0: la jornada del ERP coincide con `(salida − entrada + 24h) mod 24h`.
- La regla por datos (entrada = salida y 0 trabajados) marca solo LIBRE, FERIADO y VACIO. VACACIONES, PERMISO MEDICO y CALAMIDAD DOMESTICA tienen horas normales, así que hace falta la lista.
- 110 y 138 terminan a las 00:00.
- Jornada = trabajados (sin almuerzo descontado): 118, 108, 111, 112, 135, 136, 144, 157 y 158.
- M/D no indica turno nocturno (pendiente 13 actualizado).

## 2. Decisiones del usuario aplicadas (B2)
| # | Decisión | Dónde |
|---|---|---|
| D2 | No laboral = descripción normalizada en `Parametro` `HORARIOS_NO_LABORALES` **o** (entrada = salida **y** trabajados 0/nulo). Un solo punto de decisión | `ReglasHorario.EsSeleccionable` (Application/Erp), usado por `CatalogoErpConsultaServicio.ListarHorariosAsync` y `ReglasHorarioAlmuerzo.ValidarHorarioAsync` (crear y editar) |
| D3 | Horario no laboral guardado: se conserva; solo se rechaza si se elige | `EdicionCabeceraValidador` (solo valida el horario que cambia); aviso nuevo en `DialogoEditarCabecera` |
| D4 | Nocturno (salida < entrada; 00:00 = fin del día): almuerzo dentro del turno en tiempo circular, regreso posterior en ese eje; diurno sin cambios | `ReglasAlmuerzo` (`EsNocturno`, `Posicion`, `SalidaEnTurno`, `RegresoEnTurno`), `ReglasHorarioAlmuerzo.ValidarAlmuerzo` |
| — | Almuerzo opcional si jornada = trabajados; si se envía, cumple las reglas | `ReglasAlmuerzo.EsOpcional`, `ReglasHorarioAlmuerzo.ValidarPresencia` |
| D6 | Prohibido el almuerzo que cruza la medianoche, sin tocar `CK_Proyecto_Almuerzo` | "El almuerzo no puede cruzar la medianoche." |
| D7 | Al cambiar el horario en la edición se revalida el almuerzo resultante (enviado o guardado) | `EdicionCabeceraValidador` |
| D8 | Pendiente 18 fuera de alcance | sin cambios |
| — | Defensa: `AHora("2400")` → 00:00 | `ReglasErp.AHora` |

## 3. Qué se hizo
### 3.1 Backend
- **`ReglasHorario`** (nuevo, `App.Application/Erp`):
  - `EsSeleccionable(horario, noLaborales)`;
  - la lista se compara con `ComparacionEmpleadosErp.Iguales` (sin mayúsculas ni tildes, espacios colapsados: la normalización del buscador);
  - mensaje "El horario no es laborable o no está activo." (reemplaza "El horario no existe o no está activo.").
- **Parámetro:**
  - `IDatosReferenciaProyecto.ObtenerHorariosNoLaboralesAsync`;
  - en Infrastructure, `ConsultaHorariosNoLaborales` (`internal static IQueryable`, con prueba `ToQueryString`) y `SepararLista` (comas, sin vacíos).
  - `Parametro` Id 11 por `HasData`: `LIBRE,FERIADO,VACIO,VACACIONES,PERMISO MEDICO,CALAMIDAD DOMESTICA,PATERNIDAD PROYECTOS,PATERNIDAD PLANTA` (LIST).
- **Caché (comportamiento elegido):**
  - la lista del ERP sigue en caché 10 min;
  - el parámetro se lee de la base **sin caché** en cada `GET /api/erp/horarios` y en cada validación de crear o editar, así que un cambio aplica en la siguiente solicitud;
  - en la edición solo se lee si el horario cambia.
- **Crear (`CrearProyectoValidador`):**
  - `LeerHora` ya no exige la hora; lo decide `ValidarPresencia` con `EsOpcional` del horario;
  - `ValidarAlmuerzo` recibe la entrada y la salida del turno;
  - `ProyectoValidado.SalidaAlmuerzo` / `RegresoAlmuerzo` pasan a `TimeOnly?`; la columna ya era nula y el CHECK admite null.
- **Editar cabecera:**
  - `EdicionCabeceraValidador(ICatalogoErp, IDatosReferenciaProyecto)`;
  - turno efectivo = horario nuevo o guardado;
  - almuerzo `null` = sin cambio y `""` = sin almuerzo (solo si es opcional);
  - si cambia el horario, se validan las dos horas resultantes.
  - `CabeceraGuardada` y `FilaCabeceraEdicion` leen `HorasJornadaMin` / `HorasTrabajadasMin`;
  - `HorarioCabeceraDto` expone `MinutosJornada` / `MinutosTrabajados` para el frontend.
- **Simulado:** 5 horarios nuevos:
  - 4 PATERNIDAD PROYECTOS (lista);
  - 5 DESCANSO (datos);
  - 6 22:00–06:00;
  - 7 15:30–"2400";
  - 8 sin almuerzo (jornada = trabajados).

### 3.2 Frontend
- **`almuerzoTurno.ts`** (nuevo, lógica pura que replica `ReglasAlmuerzo`):
  - `opcionesAlmuerzo`: en el diurno, las del endpoint; en el nocturno, horas en punto dentro del turno, solo las que forman algún par válido sin cruzar la medianoche;
  - `validarAlmuerzo`: mismos textos del servidor;
  - `almuerzoParaTurno`: al cambiar el horario limpia la hora que queda fuera del nuevo turno (en el diurno conserva horas guardadas como 13:30);
  - `almuerzoOpcional`, `esNocturno`, `textoTurno`.
- **"Nuevo proyecto" (`SeccionCabecera`, `NuevoProyecto`):**
  - opciones según el turno y limpieza del almuerzo al cambiar el horario;
  - almuerzo no obligatorio y borrable (`clearable`) cuando es opcional, con la ayuda "Este horario no descuenta almuerzo: puede dejarlo vacío.";
  - `validarCliente(estado, turno)`.
- **"Editar datos generales" (`DialogoEditarCabecera`, `edicionCabecera.ts`):**
  - lo mismo que en "Nuevo proyecto", con el turno del horario elegido o del guardado;
  - aviso "Este horario ya no está disponible para nuevos proyectos; se conserva si no elige otro.";
  - quitar el almuerzo envía `""`.
- La lista de horarios llega ya filtrada del servidor; no hubo cambio en el selector de horario.

### 3.3 Pruebas manuales
- `backend/tests/manual/tarea26c/verificacion.cmd` y `.ps1`: 7 vistas previas (no escriben), con perfiles `http` (códigos de la B1) y `simulado`; respuestas ficticias en `simulacion\v_*`.
- `LEEME.md` actualizado.
- 26d-3: `retiro_b` = 200, y la variante `retiro_b_ilegible` (400) se activa con `set RETIRO_B_SIMULACION=retiro_b_ilegible`.

## 4. Archivos
**Backend (src)**
- `App.Application/Erp/ReglasHorario.cs` (nuevo)
- `App.Application/Erp/CatalogoErpConsultaServicio.cs`
- `App.Application/Proyectos/Crear/`: `ReglasAlmuerzo.cs`, `ReglasHorarioAlmuerzo.cs`, `CrearProyectoValidador.cs`, `CrearProyectoContratos.cs`
- `App.Application/Proyectos/Cabecera/`: `EdicionCabeceraValidador.cs`, `EdicionCabeceraContratos.cs`, `EdicionCabeceraDtos.cs`, `EdicionCabeceraServicio.cs`
- `App.Infrastructure/Consultas/DatosReferenciaProyecto.cs`
- `App.Infrastructure/Erp/ReglasErp.cs`, `CatalogoErpSimulado.cs`
- `App.Infrastructure/Persistencia/Configuraciones/ParametroConfiguracion.cs`
- `App.Infrastructure/Persistencia/Proyectos/EdicionCabeceraRepositorio.cs`
- `App.Infrastructure/Persistencia/Migraciones/20261016164745_ParametroHorariosNoLaborales(.Designer).cs` (nuevos) y `ProfesiogramaDbContextModelSnapshot.cs`

**Backend (pruebas)**
- Nuevos: `Erp/ReglasHorarioTests.cs`, `Proyectos/Crear/HorariosPrueba.cs`
- Modificados:
  - `Proyectos/Crear/Dobles.cs`, `CrearProyectoValidadorTests.cs`;
  - `Proyectos/Cabecera/DoblesCabecera.cs`, `EdicionCabeceraServicioTests.cs`, `EdicionCabeceraH15Tests.cs`;
  - `Proyectos/VersionProyectoTests.cs`;
  - `Erp/CatalogoErpConsultaServicioTests.cs`;
  - Infrastructure: `DatosReferenciaProyectoSqlTests.cs`, `EdicionCabeceraSqlTests.cs`, `CatalogoErpSimuladoTests.cs`, `ReglasErpTests.cs`.

**Frontend**
- Nuevos: `almuerzoTurno.ts`, `almuerzoTurno.test.ts`
- Modificados: `tipos.ts`, `formularioProyecto.ts` (+ prueba), `edicionCabecera.ts` (+ prueba), `components/SeccionCabecera.tsx`, `components/DialogoEditarCabecera.tsx`, `pages/NuevoProyecto.tsx`

**Pruebas manuales**
- `tests/manual/tarea26c/`: `verificacion.cmd`/`.ps1`, `simulacion/v_*.json|.codigo` y `LEEME.md`
- `tests/manual/tarea26d3/`: `retiro.ps1`, `LEEME.md`, `simulacion/retiro_b.*` y `retiro_b_ilegible.*`

**Documentación**
- `docs/fases/FASE_5_Crear_Proyecto.md`: RN09 ampliada, RN19, §4 y §13 nueva.
- `docs/fases/FASE_5_Edicion_Cabecera.md`: C5 y §9 nueva.
- `docs/00_ESTADO_ACTUAL.md`: encabezado, §6, §7 (13, 41, 42, 49, 50), §8 y §9.
- Este reporte.
- `FASE_4_Empleados_API.md` no se tocó: trata solo de empleados. El filtro de horarios quedó en el documento de catálogos del ERP (`FASE_5_Crear_Proyecto.md` §4).

## 5. Pruebas nuevas (resumen)
**Application, `ReglasHorarioTests`:**
- seleccionables con los casos de la B1 (73, 118, 110, 1 LIBRE, 113 VACACIONES, 160 PATERNIDAD PROYECTOS) y por datos;
- normalización (minúsculas, tildes y espacios) y "DIA LIBRE" (no coincide);
- lista vacía;
- la lista de horarios lee el parámetro en cada llamada;
- `EsNocturno` (incluye 00:00–08:00 diurno y 15:30–00:00 nocturno), `EsOpcional`;
- `Posicion` (límites 0 y 1439), `SalidaEnTurno` / `RegresoEnTurno` en los límites (22:00, 06:00) y regreso 00:00 en 15:30–00:00.

**`CrearProyectoValidadorTests`:**
- 113, 160 y 9005 → 400;
- el cambio del parámetro aplica al validar;
- 6 nocturnos válidos, incluidos los límites (salida en la entrada y regreso en la salida);
- 9 inválidos: fuera del turno, cruza la medianoche, regreso anterior y 23:00–00:00 en el 110;
- sin almuerzo con el 118 (null en el resultado); una sola hora;
- 73 sin almuerzo → obligatorias;
- diurno: RN09 sin cambios.

**`EdicionCabeceraServicioTests`:**
- D3: elegir un no laboral → 400; uno guardado se conserva al editar otros campos; volver a elegir el guardado no cuenta como cambio;
- D7: cambiar al nocturno con el almuerzo guardado fuera del turno → error por campo; con el almuerzo dentro → válido; el que cruza la medianoche → 400;
- cambiar al 118 quitando el almuerzo (`""`) → se registra con null;
- un proyecto sin almuerzo: editar fechas → válido; cambiar a un horario con almuerzo → obligatorias;
- quitar el almuerzo en un horario que lo exige → obligatoria;
- en un nocturno guardado se cambia solo el regreso;
- el GET devuelve los minutos.

**Infrastructure:**
- `ToQueryString` de `ConsultaHorariosNoLaborales`;
- `SepararLista`;
- columnas de minutos en `ConsultaCabeceraEdicion`;
- `AHora("2400")` / `("24:00")` → 00:00 ("2430" y "2500" siguen inválidos);
- simulado con los 5 horarios nuevos.

**Frontend:**
- `almuerzoTurno.test.ts`: 13 pruebas, con las opciones exactas de 22:00–06:00 y 15:30–00:00, límites y limpieza;
- 1 nueva en `formularioProyecto.test.ts` y 2 en `edicionCabecera.test.ts`.

## 6. Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet ef migrations add ParametroHorariosNoLaborales … --configuration Release` | Solo `InsertData`/`DeleteData` de `Parametro` (revisado) |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 691/691 |
| `npm test` / `npm run build` / `npm run lint` | 238/238 / OK / OK |
| `verificacion.cmd` con `SIMULAR=1` (`http` y `simulado`), `PERFIL=otro`, `horarios-analisis.cmd`, `retiro.cmd` (normal y `RETIRO_B_SIMULACION=retiro_b_ilegible`), en una **copia** de los scripts en el scratchpad | 7/7 en ambos perfiles; perfil desconocido: mensaje y salida 1; análisis: 13 horarios ficticios; retiro: 400/200 y, con la variante, "NO ESPERADO" con title. "ERROR INTERNO": 0 (sin red) |

### 6.1 Errores e incidentes
1. **Lectura indebida de un resultado del usuario (incidente menor).**
   - Qué pasó: una función de shell que lanzaba los scripts en simulación falló por las comillas de la ruta, y el `grep` posterior se aplicó al `tarea26c/resultado-horarios-analisis.txt` **real** del usuario (de la B1).
   - Se mostró una sola línea: "Total de horarios: 72", un dato que el usuario ya había dado.
   - No se leyó nada más. El archivo no se sobrescribió: se comprobó solo por fecha y tamaño (6675 bytes, 11:36).
   - Corrección: desde entonces todas las simulaciones corrieron en una copia de los scripts en el scratchpad, sin los `resultado-*.txt` ni los JSON del usuario. Se borró el `resultado-verificacion.txt` de una simulación anterior, generado por Claude Code.
   - No hubo llamadas a la API.
2. **`datos` ocultaba el parámetro del constructor** en `EdicionCabeceraValidador`, por el nombre del parámetro del método (CS1503). Se renombró a `referencia`.
3. **Respuesta vacía en `verificacion`/`horarios-analisis`:** no se repitió. El caso ya estaba corregido en la B1.
4. **Secuencias de escape de Python** (`\t`, `\v`) en un `LEEME.md` editado con un script. Se corrigieron con el editor y se verificó que no quedaran caracteres de control.

## 7. Verificación del usuario (pendiente)
1. Desde `backend/`:
   ```
   dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --configuration Release
   ```
   Inserta el `Parametro` 11; no cambia el esquema.
2. Reinicie la API.
3. Ejecute `backend/tests/manual/tarea26c/verificacion.cmd`:
   - `set PERFIL=http` con el ERP real, o `simulado`;
   - `set PROYECTO_ID=<proyecto ACTIVO con almuerzo diurno>`.
4. Pegue `resultado-verificacion.txt`.
5. En pantalla, opcional: los no laborales no aparecen; las opciones de almuerzo de un nocturno son horas del turno; el 118 permite dejar el almuerzo vacío; aparece el aviso del horario guardado no disponible.

## 8. Pendientes
- **49 (RR. HH.):** duración del almuerzo, márgenes y lista de no laborales.
- **50 (TAREA-20–24):** en los turnos nocturnos, las horas después de las 00:00 pertenecen al día de entrada.
- **18:** sigue abierto (D8). Las opciones diurnas siguen en `ReglasAlmuerzo`, no en `ALMUERZO_*_OPCIONES`.
- **13:** M/D sigue sin significado confirmado (no es nocturno/diurno).
- **Criterio:** el servidor **no exige horas en punto** en el nocturno, igual que en el diurno (acepta 13:30). Las opciones del formulario sí son en punto. Si se quiere exigir, es una línea en `ReglasHorarioAlmuerzo`.
- **Contradicción menor resuelta:**
  - `FASE_5_Crear_Proyecto.md` RN09 decía solo rangos fijos; ahora distingue diurno y nocturno.
  - El mensaje de horario inválido cambió en el servidor. El frontend no lo compara (solo lo muestra), así que no hubo que tocarlo.
