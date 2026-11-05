# TAREA-24d — Reporte: frontend de novedades, sincronización de permisos médicos (Admin)

**Fecha:** 26/10/2026 · **Fase:** B (implementación del plan de la TAREA-24) · **Estado:** ✅ Verificada por el usuario el 26/10/2026

## 1. Qué se hizo
- Botón «Sincronizar permisos médicos» en la barra de `/novedades`, junto a «Nueva novedad», visible solo si `esAdmin(usuario)`. Reutiliza `useUsuarioActual` (la misma consulta y caché de la barra superior); no se agregó otra.
- `DialogoSincronizacion`: confirmación → en curso (spinner; no se cierra con Esc ni clic fuera: `modalType="alert"` y cierre ignorado mientras envía) → resultado (fecha con `formatearFechaHora`, fuente, conteos con ceros, nota de ausentes, omitidas por categoría o «Ninguna.», códigos con la nota de 100) o error dentro del diálogo.
- `POST /api/admin/novedades/sincronizar-permisos-medicos` sin cuerpo ni `escenario`; mutación sin reintentos, ref contra el doble clic; solo un 200 llama a `invalidarTrasNovedad`.
- `sincronizacion.ts` (lógica pura): `esAdmin`, `etiquetaOmision` (las 11 de §8.5; clave desconocida tal cual), `resumenSincronizacion` e `interpretarErrorSincronizacion` (503 y red con «Reintentar»; 403 con «Cierre el diálogo y recargue la página.», sin «Reintentar»).
- Documentación: `FASE_5_Novedades.md` §9.5 (24d), §9.3 solo con la 24e y la tabla del encabezado; `00_ESTADO_ACTUAL.md` (24c ✅, 24d implementada, siguiente tarea, **pendiente 67**; **24 974 B**; lo reemplazado va textual a `HISTORIAL.md` H10, con 0 líneas perdidas); `TAREA-24c-reporte.md` marcada verificada.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/features/novedades/components/DialogoSincronizacion.tsx` | Nuevo |
| `…/sincronizacion.ts` (+test) | Nuevo |
| `…/api.ts`, `…/hooks.ts`, `…/tipos.ts` | `sincronizarPermisosMedicos`, `useSincronizarPermisosMedicos`, `ResultadoSincronizacion` |
| `…/pages/ListadoNovedades.tsx` | Botón (solo Admin) y diálogo |
| `docs/fases/FASE_5_Novedades.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md`, `docs/tareas/TAREA-24c-reporte.md` | §9.3, §9.5, estado, pendiente 67, H10 |

## 3. Comandos y resultado (desde `frontend/`)
| Comando | Resultado |
|---|---|
| `npm run build` | OK, sin errores ni advertencias |
| `npm run lint` (oxlint) | Sin hallazgos (exit 0) |
| `npm test` | **349/349** (340 + 9 de `sincronizacion.test.ts`). Ninguna prueba existente se ajustó |

## 4. Desviaciones, hallazgos y decisiones
- **Hallazgo sobre S2:** el script `tarea23-3/sincronizacion.cmd` termina con **s11** (escenario `anuladas`), que deja las 6 NOV-SIMPM **anuladas**. Si se corrió, la primera sincronización desde la interfaz (sin `escenario` = `base` en la fuente Simulada) da **reactivadas 6**, no «nuevas 0 y sin cambios 6». Los tres casos están en §5.
- Con la fuente Simulada (10 filas en memoria) la sincronización es muy rápida: el estado «en curso» (S5) puede verse solo un instante.
- Las 4 filas omitidas de la fuente Simulada son 0004 `tipoNoSoportado`, 0005 `jsonInvalido`, 0006 `sinEmpleado` y 0010 `otroOrigen`. Que sus códigos lleguen como `NOV-SIMPM-000x` y no como «fila ID n» es [SUPUESTO] (el código sale del Título de la fila, §8.3).
- Para el 401 se usa el título común de `clienteHttp` («No autenticado.»), sin «Reintentar».
- Sin cambios de backend (sin B0). El `escenario` no aparece en la interfaz ni en la solicitud.

## 5. Verificación para el usuario (`npm run dev`; DevAuth)
**Preparación**
- **(a) ¿Ya existen las NOV-SIMPM?** Como **admin**, en `/novedades` elegir Origen «Permisos médicos» [nombre del catálogo], Buscar `NOV-SIMPM` y encender «Incluir anuladas». Si aparecen anuladas, las dejó el script de la 23-3 (s11).
- **(b) Limpieza:** son ficticias (año 2075, DEV001–DEV006) y se pueden dejar. Para dejarlas anuladas otra vez, el usuario ejecuta `backend\tests\manual\tarea23-3\sincronizacion.cmd` (termina con s11). La interfaz no puede anularlas: PERMISOS_MEDICOS es de solo lectura (N10).
- **(c) Sin modo de fuente (S3):** desde `backend/`, `dotnet user-secrets set "NovedadesExternas:Modo" "Graph" --project src/App.Api` y reiniciar la API (Graph aún no existe → 503 «no configurada»). **Volver a Simulado:** `dotnet user-secrets remove "NovedadesExternas:Modo" --project src/App.Api` y reiniciar (vuelve el `Simulado` de `appsettings.Development.json`). Lo ejecuta el usuario.

| # | Caso (usuario DevAuth) | Esperado |
|---|---|---|
| S1 | gestor en `/novedades` | No ve «Sincronizar permisos médicos»; sí «Nueva novedad» |
| S2 | admin, fuente Simulada → «Sincronizar» | Fuente «Simulado», leídas 10. **Si existían anuladas (script s11):** reactivadas 6, nuevas 0, sin cambios 0. **Si existían activas:** nuevas 0, sin cambios 6. **Si no existían:** nuevas 6. En los tres: actualizadas 0; ausentes ≥ 1 (NOV-DEV-0004) con «No se anulan; solo se cuentan.»; «Omitidas (4)»: Tipo de novedad no soportado 1, Detalle ilegible 1, Empleado no encontrado 1, Otro origen (no es PERMISOS_MEDICOS) 1; 4 códigos omitidos |
| S2b | Repetir la sincronización | nuevas 0, reactivadas 0, sin cambios 6 |
| S2c | Cerrar el diálogo: el listado se refresca solo. Filtrar Origen «Permisos médicos» y abrir una NOV-SIMPM | El detalle (admin) muestra «Editar» y «Anular» deshabilitados con «Las novedades de PERMISOS_MEDICOS son de solo lectura.» |
| S3 | admin con `Modo = Graph` (paso c) → «Sincronizar» | Dentro del diálogo: «Fuente de novedades externas no configurada: …» con «Reintentar»; el diálogo no se cierra |
| S4 | «Sincronizar permisos médicos» → «Cancelar» | Se cierra sin llamar a la API (en las herramientas del navegador, pestaña Red, no aparece el POST) |
| S5 | Durante «Sincronizando…» pulsar Esc o hacer clic fuera | No se cierra (con la fuente Simulada puede durar un instante) |
| S6 | 375 px | Botones de la barra uno debajo del otro; diálogo a todo el ancho; tablas de conteos sin desplazamiento horizontal; botones a todo el ancho |
| R | Regresión: «Nueva novedad», detalle, edición y anulación | Igual que en la 24c |

## 6. Pendientes
- Verificación visual (§5) y, si se cambió, volver `NovedadesExternas:Modo` a Simulado.
- Decidir la siguiente tarea: 24e (opcional, requiere una B0) o el criterio de personal (pendiente 62).
- Pendiente 67: extensión estable para los 409 de novedades.
