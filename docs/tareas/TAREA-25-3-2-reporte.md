# TAREA-25-3-2 — Reporte: cargos Infor, frontend

**Fecha:** 29/10/2026 · **Fase:** B (implementación del plan de la Fase A corta) · **Estado:** ✅ Verificada por el usuario (29/10/2026)

Pantalla, decisiones P1–P3 y W1–W9: `docs/fases/FASE_5_Usuarios_Departamentos.md` §10.6. Sin cambios de backend.

## 1. Qué se hizo
- **Tercera tarjeta «Cargos Infor»** en `/admin` y ruta diferida `/admin/cargos-infor`, protegida con `AdminRequerido`.
- **Listado:**
  - filtros en la URL, con «Sin código» por defecto;
  - columnas Cargo, Código Infor, Estado, Personas guardadas y Activos ERP («—» si viene null);
  - insignia «Afecta al reporte» (P1).
- **Diálogo de edición:**
  - vista previa del código normalizado y validación local con los mensajes del backend;
  - interruptor «Activo», «Se guardará: …» y confirmación de la baja;
  - si se cierra con cambios, pide confirmar.
- **Diálogo de importación:**
  - la vista previa se pide solo al abrirlo (P2);
  - muestra «Lista de las HH:mm», el aviso `esAnterior`, los conteos, hasta 20 variantes con «y N más» y la nota fija;
  - el resultado reemplaza a la vista previa.
- **`BarraErrores` extraída** a `features/admin/BarraErrores.tsx` (P3), sin cambios de comportamiento ni de textos. `PaginaUsuario.tsx` la importa.
- **`formatearHoraUtc`** en `utils/formato.ts` (America/Guayaquil, HH:mm), con su prueba.
- **Invalidación:** listados, vista previa (marcada vencida sin volver a pedirse) y `['proyectos','reporte-resumen']`.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/features/admin/cargos-infor/cargosInfor.ts` (+ `.test.ts`) | Nuevo: tipos, filtros, código, insignia, edición, importación y errores (19 pruebas) |
| `frontend/src/features/admin/cargos-infor/api.ts` | Nuevo: listado, vista previa, importación, PUT e invalidaciones |
| `frontend/src/features/admin/cargos-infor/PaginaCargosInfor.tsx`, `DialogoEditarCargo.tsx`, `DialogoImportarCargos.tsx` | Nuevos |
| `frontend/src/features/admin/BarraErrores.tsx` | Nuevo: extraído de `PaginaUsuario.tsx` |
| `frontend/src/features/admin/usuarios/PaginaUsuario.tsx` | Quita la copia interna e importa `BarraErrores` |
| `frontend/src/features/admin/PaginaAdmin.tsx`, `frontend/src/app/App.tsx` | Tarjeta y ruta |
| `frontend/src/utils/formato.ts` (+ `.test.ts`) | `formatearHoraUtc` (2 pruebas) |
| `docs/fases/FASE_5_Usuarios_Departamentos.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` (H18) | Documentación |

## 3. Comandos y resultado (desde `frontend/`)
| Comando | Resultado |
|---|---|
| `npm run build` | Correcto (tsc -b + vite build; chunk `PaginaCargosInfor` aparte) |
| `npm run lint` | Correcto, código de salida 0 |
| `npm test` | **399/399** (378 + 21 nuevas); ninguna prueba existente se ajustó |
| Backend | Sin cambios; no se ejecutó |
| `00_ESTADO_ACTUAL.md` | **24 989 B**. Lo reemplazado va textual a `HISTORIAL.md` H18. Al comparar con `HEAD` solo difiere la línea «Última actualización», que se actualiza en su lugar, como en las tareas anteriores |

## 4. Desviaciones, errores y decisiones
- **Ajuste 1, nombre de la función:** `formatearHora` ya existe en `utils/formato.ts`. Recorta un TimeOnly `"HH:mm:ss"` y la usan 5 archivos de proyectos y del cronograma. La nueva se llama **`formatearHoraUtc`** para no cambiar esa función. No usa `slice` sobre `formatearFechaHora`.
- **Ubicación de `BarraErrores` (P3):** va en `features/admin/`, no en `components/`. Depende del tipo `ErroresDialogo` de `features/proyectos`, y `components/` no importa tipos de features. Solo la usan pantallas de administración. Su prop pasó de `ErroresUsuario<string>` a `ErroresDialogo<string>`, el tipo base: es el mismo contrato, porque `ErroresUsuario` lo extiende.
- **404 del PUT:** muestra «El cargo no existe.» y ofrece **«Recargar»**, que cierra el diálogo y vuelve a leer la lista. La Fase A decía que el diálogo se cerraba y la lista se refrescaba; con el botón, la acción queda explícita.
- **Vista previa tras importar:** se marca vencida con `refetchType: 'none'` para no llamar otra vez al ERP mientras el diálogo muestra el resultado. Con `gcTime: 0`, la próxima apertura la pide de nuevo.
- **«Importar» deshabilitado sin vista previa:** si la vista previa falla (503 del ERP), el botón queda deshabilitado; solo se ofrece «Reintentar» la vista previa.
- **V13 del pedido:** `/admin/usuarios/999999` muestra «El usuario no existe.» con la barra **propia** de la página de detalle, no con `BarraErrores`, igual que en la 25-2. Para ejercitar la barra extraída agregué el paso V13b: un 409 de «Datos» con dos pestañas.
- **§9 de FASE_5** sigue diciendo «dos tarjetas»: es el texto de la 25-2. La tercera tarjeta está en §10.6. No lo cambié porque el pedido limitaba la documentación.
- **Pendiente 74 (nuevo):** hay duplicaciones concretas, nombradas en §10.6 y en el pendiente: el hook de filtros, el cuerpo del listado y los estilos `MOVIL` de los diálogos.
- **Textos [SUPUESTO] que agregué** (no venían en el pedido):
  - el aviso de `esAnterior`: «El ERP no respondió: la vista previa usa la última lista obtenida, de antes de la falla.»;
  - la explicación de las variantes;
  - la descripción de la tarjeta;
  - el título de la insignia;
  - «¿Cerrar sin guardar?»;
  - «Cargo actualizado.».

## 5. Verificación para el usuario
Requisitos: API en modo Http (sin reiniciar: no hay cambios de backend) y `npm run dev`. DevAuth `admin`, salvo V1.

| # | Pasos | Esperado | Estado de la base |
|---|---|---|---|
| V1 | Como `gestor`: menú y `/admin/cargos-infor` por URL | «No tienes permiso para ver esta página.» y ninguna llamada a `/api/admin` (F12 → Red) | — |
| V2 | `/admin` | Tres tarjetas: Personal elegible, Usuarios y departamentos, Cargos Infor | — |
| V3 | Tarjeta «Cargos Infor» | Filtro «Sin código» (unos 496), el texto fijo del código Infor y «—» en Activos ERP si la caché no tiene lista | — |
| V4 | «Todos», buscar `PARAMEDICO`, «Incluir inactivos» y F5 | Los filtros quedan en la URL y se conservan; con «Sin código», `filtro` no aparece en la URL | — |
| V5 | Buscar un cargo con personas guardadas y sin código | Insignia «Afecta al reporte» (al pasar el ratón, la explicación) | — |
| V6 | Abrir el Id 193 (ABOGADO CORPORATIVO) y escribir ` p-25.3 ` | «Se guardará como: P-25.3» y «Se guardará: (sin código) (Activo) → P-25.3 (Activo)»; Guardar → «Cargo actualizado.» | Id 193 con `P-25.3` |
| V7 | En el mismo diálogo: `P 1` y luego 21 caracteres | El error bajo el campo, Guardar deshabilitado y ninguna llamada a la API | — |
| V8 | Dos pestañas con el Id 193: guardar en una y luego en la otra | 409 «Otro usuario cambió el cargo; recargue.» con «Recargar» (si hay cambios, la confirmación) | — |
| V9 | Id 193: apagar «Activo» y guardar | Confirmación «El reporte dejará de mostrar el código Infor de este cargo.» → «Inactivo» (visible con «Incluir inactivos») | Id 193 inactivo |
| V10 | «Importar puestos desde el ERP» | Vista previa con «Lista de las HH:mm» y `nuevos` en 0 o pocos (personal nuevo en el ERP desde el 29/10/2026 12:53); «Importar» → insertados 0 o pocos; «Cerrar» | Si hay nuevos: filas nuevas sin código (no se revierten) |
| V11 | Editar un cargo, cambiar un campo y cerrar con Esc, la X o «Cancelar» | «¿Cerrar sin guardar?» | — |
| V12 | 375 px (F12 → dispositivo) | Una columna, botones y diálogos a todo el ancho, tabla desplazable | — |
| V13a | Regresión: Usuarios (listado, detalle) y Personal elegible | Igual que antes | — |
| V13b | `/admin/usuarios/999999`; luego dos pestañas en un usuario de prueba, cambiar el nombre y guardar en ambas | «El usuario no existe.»; el 409 de «Datos» con «Recargar» y su confirmación (`BarraErrores` extraída) | Sin cambios si se recarga sin guardar |

**Restauración:** el Id 193 debe quedar **sin código y activo**. Desde el diálogo, con «Incluir inactivos»: borrar el código, encender «Activo» y guardar. Comprobar con SQL de solo lectura en SSMS: `SELECT Id, CodigoInfor, Activo FROM dbo.CargoInfor WHERE Id = 193;` → `NULL`, `1`. En V13b no guarde cambios en el usuario de prueba, o deje el nombre como estaba.

## Verificación del usuario
29/10/2026: V1–V13 y V13b OK.
- **V10 (importación):** puestosApi 498, insertados 0, yaExistentes 498, existentesInactivos 0.
- **SSMS después de restaurar:**
  - `CargoInfor` con 499 filas;
  - Id 193 sin código y activo;
  - Usuario Id 7 «PRUEBA UI» inactivo;
  - Id 4 y 5 sin cambios.

## 6. Pendientes
- Pendiente 74 (componentes repetidos de administración), 2 (Excel, opcional) y 73 (join sensible a tildes y espacios).
