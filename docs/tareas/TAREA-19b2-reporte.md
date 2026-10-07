# TAREA-19b2 — Corrección: token de concurrencia basado en los datos que vio el usuario (pendiente 35)

**Fecha:** 2026-10-07 (Fase A y Fase B)
**Resultado:** ✅ completada. Verificación visual del usuario (B1, B2, C1, C2, E1, E2): todo OK (sección 5). Resuelve P10 de la TAREA-19b.
- `npm run build`: sin errores ni advertencias.
- `npm run lint` (oxlint): sin hallazgos.
- `npm test`: **177/177**. Son las 170 anteriores (3 ajustadas, aprobadas) más 7 nuevas: 4 de `tokenConcurrencia.test.ts` y 1 en cada uno de `cambioEstado.test.ts`, `edicionCabecera.test.ts` y `edicionPersonal.test.ts`.

**Lo que no se hizo:** cambios en `backend/`, `npm run dev`, `dotnet run`, clientes HTTP contra la API, ni comandos git que modifiquen el repositorio. `X-Dev-User` sigue solo en `src/auth/` (verificado con `grep`).

## 1. Defecto (P10 de la TAREA-19b, 06/10/2026)
**Lo que pasó:** en el Id 13, con dos pestañas en "Actualizar personal":
1. A cambió la observación del back 1 y registró (versión 6).
2. B tenía el formulario armado con el GET anterior (versión 5). Cambió el fin del back 1, generó la vista previa **después** del registro de A y registró.
3. Resultado: 200, sin 409, y la observación de A quedó sobrescrita con la vieja.

**Causa:** los registros enviaban el `versionProyecto` de la **vista previa** (decisión de la 19x). Si la vista previa se genera después del cambio de otro usuario, trae el token nuevo, y el cuerpo, armado con datos viejos, pasa. El mismo hueco existía en `DialogoEditarCabecera` (19a) y en `DialogoCambioEstado` (15).

**El backend no cambia:** compara correctamente el token que recibe. El defecto estaba en qué token enviaba el cliente.

## 2. Decisiones aprobadas
| # | Decisión |
|---|---|
| Token base | La versión de los datos con que se armó el formulario: GET `…/edicion` (personal), GET `…/cabecera` (cabecera), la última etapa del detalle (máx de `etapas[].version`, 0 si no hay; cambio de estado) y GET `…/reactivacion` (19c, preparado en `tokenConcurrencia.ts`) |
| Registro | Siempre con el token base, nunca con el de la vista previa |
| Detección temprana | Si la vista previa trae otra versión que la base: "El proyecto cambió desde que abriste esta pantalla. Recarga los datos para continuar." + "Recargar datos del proyecto", y el registro queda deshabilitado |
| P1 | La base se fija al abrir el diálogo o la pantalla y solo cambia con "Recargar" |
| P2 | Personal: confirmación "¿Recargar los datos del proyecto?" / "Se perderán los cambios que no registraste." ("Seguir editando" / "Recargar") en los dos botones "Recargar" (aviso de cambio y 409), solo si hay cambios sin registrar |
| P3 | Cabecera y cambio de estado sin un segundo diálogo |
| P4 | "Ver impacto" y "Generar vista previa" siguen habilitados con el aviso; solo se bloquea el registro |
| P5 | 3 pruebas ajustadas (sección 4.1) |
| P6 | Verificación del cambio de estado E1/E2 en el orden indicado por el usuario (sección 5) |

## 3. Qué se hizo
**Lógica común (`tokenConcurrencia.ts`, nuevo)**
- `MENSAJE_CAMBIO_POR_OTRO`.
- `versionDeEtapas(etapas)`.
- `cambioPorOtro(vista, versionBase)`: hay vista previa y su versión es distinta de la base. La vista previa lee la versión antes que los datos, así que nunca es menor que la base.
- `conToken(cuerpo, versionBase)`.

**Personal**
- `edicionPersonal.ts`:
  - `EstadoEdicionPersonal.versionBase` sale de `dto.versionProyecto`;
  - `aSolicitudRegistroPersonal(estado)` usa la base;
  - `puedeRegistrarPersonal` y `motivoSinRegistroPersonal` bloquean y explican con `cambioPorOtro`.
- `ActualizarPersonal.tsx`:
  - aviso con "Recargar datos del proyecto";
  - `pedirRecarga` muestra la confirmación de P2 si hay cambios (también en el botón del 409);
  - recargar vuelve a montar el formulario con el GET nuevo, y por lo tanto con una base nueva.

**Cabecera**
- `edicionCabecera.ts`:
  - `EstadoEdicion.versionBase` sale de `cabecera.versionProyecto`, y `reiniciar` la toma de la cabecera recargada;
  - `aSolicitudRegistroCabecera(estado)`;
  - `puedeRegistrar` y `motivoSinRegistro` usan `cambioPorOtro`.
- `DialogoEditarCabecera.tsx`: aviso con "Recargar".

**Cambio de estado**
- `cambioEstado.ts`:
  - `aSolicitudRegistroCambio(estado, versionBase)`;
  - `puedeConfirmar(vista, estado, enviando, versionBase?)`: el parámetro es opcional y, si viene con una vista previa "de otro", devuelve false.
- `DialogoCambioEstado.tsx`:
  - `ProyectoCambio.versionBase`, fijado al abrir con `useState`;
  - `onRecargar` devuelve la versión nueva (`Promise<number | undefined>`);
  - aviso con "Recargar".
- `DetalleProyecto.tsx`: pasa `versionDeEtapas(proyecto.etapas)` y, al recargar, lee el detalle nuevo con `getQueryData` y devuelve su última etapa.

**Documentación**
- `FASE_5_Edicion_Cronograma.md` §9, `FASE_5_Edicion_Cabecera.md` §8 y `FASE_5_Estados_Proyecto.md` §9: el token que se envía es el de los datos que vio el usuario. Corrige la decisión de la 19x.
- `00_ESTADO_ACTUAL.md`:
  - §6: 19b y 19b2;
  - §7: pendiente 35;
  - §9: filas 19b y 19b2.
- `TAREA-19b-reporte.md`: P1–P15 con los resultados del usuario, con P10 como defecto corregido aquí.

## 4. Pruebas
### 4.1 Pruebas existentes ajustadas (aprobadas, P5)
| Archivo | Antes | Ahora |
|---|---|---|
| `cambioEstado.test.ts` (antes líneas 215-219) | "el registro lleva la versionProyecto de la vista previa…" (esperaba 7, de la vista) | "el registro lleva el token base…": `aSolicitudRegistroCambio(estado, 5)` → 5 |
| `edicionCabecera.test.ts` (antes 379-384) | ídem, esperaba 7 | cabecera con versión 5 → `versionBase` 5 y registro con 5 |
| `edicionPersonal.test.ts` (antes 286-291) | ídem, esperaba 7 | DTO con versión 3 → `versionBase` 3 y registro con 3 |

Ninguna otra prueba cambió de resultado. Las fixtures ya usaban la misma versión en la base y en la vista previa, y el parámetro nuevo de `puedeConfirmar` es opcional.

### 4.2 Pruebas nuevas (7)
- **`tokenConcurrencia.test.ts` (4):**
  - `versionDeEtapas` (vacío → 0, máximo);
  - `cambioPorOtro` (null, igual, distinta);
  - `conToken`;
  - el texto del mensaje.
- **`cambioEstado.test.ts` (1):** `puedeConfirmar` con la base 5 y una vista previa 6 → false; con la base 6 → true; sin base → true, como antes.
- **`edicionCabecera.test.ts` (1):** una vista previa "de otro" → `puedeRegistrar` false y el motivo nuevo; con la misma versión → true; `reiniciar` toma la base nueva.
- **`edicionPersonal.test.ts` (1):** el caso de P10 → `puedeRegistrarPersonal` false y el motivo nuevo; con la misma versión → true.

## 5. Verificación visual (usuario) — B1–E2 OK
- Usuario `gestor`.
- **Escrituras:** solo las de la pestaña A, en el Id 13 (personal) y en el Id 12 (cabecera).

| # | Proyecto | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| B1 | Id 13 (personal) | Pestañas A y B en "Actualizar personal". A cambia la observación del back 1 → vista previa → Registrar. B cambia el fin del back 1 → genera la vista previa **después** del registro de A | B: aviso "El proyecto cambió desde que abriste esta pantalla. Recarga los datos para continuar." y "Registrar" deshabilitado (motivo junto al botón). "Recargar datos del proyecto" → "¿Recargar los datos del proyecto?" / "Se perderán los cambios que no registraste." → Recargar → B ve la observación de A | Sí (solo A) | OK |
| B2 | Id 13 (personal) | A y B abiertas. B cambia el fin del back 1 y genera la vista previa **antes**. A cambia la observación y registra. B → Registrar | B: 409 "El proyecto cambió; vuelve a cargarlo." + "Recargar datos del proyecto" (con confirmación, porque B tiene cambios) | Sí (solo A) | OK |
| C1 | Id 12 (cabecera) | A y B con "Editar datos generales" abierto. A cambia el almuerzo y registra. B cambia el horario → "Ver impacto" | B: aviso y "Registrar" deshabilitado; "Recargar" reinicia el formulario con el almuerzo de A | Sí (solo A) | OK |
| C2 | Id 12 (cabecera) | B → "Ver impacto" primero. A registra otro cambio. B → Registrar | B: 409 + "Recargar" | Sí (solo A) | OK |
| E1 | Id 12 (02/11–25/11) | B abre "Cambiar estado" → Suspender con fecha 24/11. A cambia **solo el almuerzo** (cabecera) y registra. B → "Ver impacto" | B: vista previa 200 (con la versión nueva), aviso de cambio y "Confirmar suspensión" deshabilitado. **Si quedara habilitado, NO confirmar y reportarlo** | Sí (solo A, cabecera) | OK |
| E2 | Id 12 | B → "Ver impacto" con 24/11 (antes). **Después**, A acorta el fin a 20/11 y registra. B → "Confirmar suspensión" | B: 409 + "Recargar". Si el control del token fallara, la relectura rechazaría la fecha 24/11 (> fin) y no se suspendería | Sí (solo A, cabecera: fin 20/11) | OK |

## 6. Contradicciones y observaciones
1. **Se revierte la decisión de la 19x** ("se registra con el token de la vista previa vigente"). W1 de la 19x salió bien solo porque B generó su vista previa **antes** del registro de A. Se corrigieron los tres documentos FASE_5.
2. **Cambio de estado:** la base sale del detalle (última etapa), porque no hay un GET propio. Si el detalle estaba viejo al abrir el diálogo, el usuario ve datos viejos y el registro responde 409, que es lo correcto.
3. **Reactivación (19c):** usará la base del GET `…/reactivacion` con las mismas funciones de `tokenConcurrencia.ts`.

## 7. Pendientes
- Menor: agregar `esPrincipalInicial` al GET …/edicion (hoy el frontend lo toma del detalle; pendiente 36).
