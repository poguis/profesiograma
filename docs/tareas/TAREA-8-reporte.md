# TAREA-8 — Reporte: inventario de pendientes sin TI y lote 1

**Fecha:** 09/11/2026 · **Fase:** B (lote 1 de la Fase A aprobado) · **Estado:** implementada; pendiente de la revisión del usuario (incluye el `git add -f` de K6)

## 1. Qué se hizo
- **Inventario (Fase A):** 51 pendientes abiertos clasificados.

  | Categoría | Pendientes |
  |---|---|
  | Ejecutables ya | 15, 8, 84 (3) |
  | Para cerrar | 7, 12, 16, 19, 21, 24, 65, 82, y el 50 a §7.1 (9) |
  | TI | 10, 63, 71, 75 (4) |
  | Usuario | 1, 5, 9, 47, 61, 66 (6) |
  | Externo | 2, 4, 13, 49, 53, 54 (6) |
  | Fecha u observación | 48, 58, 81, 85 (4) |
  | Fase 3 | 27, 45, 68 (3) |
  | Después de producción o futuro | 36, 37, 38, 43, 44, 46, 59, 60, 69, 74, 76 (11) |
  | Lote 2 (antes de PRO) | 73, 77, 83 (3) |
  | Pendiente 7 | Parte de su texto pasó al 63 |
- **Pendiente 15:**
  - 6 consultas EF de `DatosPruebaSembrador` expuestas como `internal static IQueryable<…>`, sin cambiar el comportamiento: proyectos `PRY-DEV-*`, compañía 9001, departamento de prueba, `UsuarioDepartamento`, usuario por correo y empleado por código;
  - 6 pruebas con `ToQueryString()`.
- **Pendiente 8:** FASE_2 §8 dice ahora `profesiograma_dev`.
- **Cierres:** 7, 12, 16, 19, 21, 24, 65 y 82, con su texto completo y el motivo en HISTORIAL H33. Antes de quitar el 7, el 63 incorporó «Bloquea la TAREA-23-4 si se elige la opción A o B».
- **Pendiente 50:** pasó a §7.1 como regla 5 (turnos nocturnos).
- **Pendiente 84 (K6):** «lista para `git add -f` entregada en la Fase A de la TAREA-8; se cierra cuando el usuario confirme el commit».
- **00_ESTADO:** la siguiente tarea es «Ajustes del usuario…; después: Lote 2 (83, 77) antes de PRO y Fase 3». Tamaño: 23 098 B.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `backend/src/App.Infrastructure/Persistencia/DatosPrueba/DatosPruebaSembrador.cs` | 6 consultas `internal static` (sección «consultas»); las llamadas usan esas consultas |
| `backend/tests/App.Infrastructure.Tests/Consultas/DatosPruebaSqlTests.cs` (nuevo) | 6 pruebas de traducción |
| `docs/fases/FASE_2_Modelo_Datos.md` | `:354` `profesiograma_app` → `profesiograma_dev` |
| `docs/00_ESTADO_ACTUAL.md` | Cierres, 63, 84, regla 5 de §7.1, siguiente tarea, cabecera |
| `docs/HISTORIAL.md` | H33 (y su fila en el índice) |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1227/1227** (1221 + 6 nuevas); 0 ajustadas |
| Frontend | Sin cambios; no se compiló |

## 4. Desviaciones, errores y decisiones
- **6 consultas, no 5:** además de las 5 de la Fase A (`:44, :61, :75, :179, :198`), expuse la del departamento de prueba (`:71`, `FirstAsync` por `Nombre`), que también filtra. La lectura completa de `Jornadas` (`:51`, sin filtro) quedó igual.
- **Cierre adicional de 8 y 15:** se implementaron en esta tarea, así que también los cerré, con su texto en H33. El alcance aprobado solo enumeraba los otros 8 cierres.
- **Error propio corregido:** la primera versión de la prueba de proyectos buscaba el literal `N'PRY-DEV-%'`, pero EF traduce `StartsWith` de otra forma. Ahora comprueba `[Codigo] LIKE`.
- **Pruebas del sembrador:** no había pruebas propias del sembrador (Grep). Las existentes que lo mencionan (`PersonalElegibleSqlTests`, `SincronizacionNovedadesTests`, `CacheEmpleadosErpTests`) siguen en verde.
- **K6:** no se ejecutó git. La lista va archivo por archivo para no forzar los `resultado-*.txt`. Revisa antes `tarea62/simulacion/empleados.json` [PENDIENTE DE CONFIRMAR que sea ficticio; no lo abrí].

## 5. Verificación para el usuario
| # | Caso | Esperado |
|---|---|---|
| 1 | `git add -f` con la lista de la Fase A, y `git status` | Los scripts, LEEME y `simulacion\` de las 7 carpetas, sin `resultado-*.txt` |
| 2 | Revisar 00_ESTADO §7 y §7.1 | Sin 7, 8, 12, 15, 16, 19, 21, 24, 50, 65 ni 82; regla 5 en §7.1; 63 con el bloqueo de la 23-4 |
| 3 | DEV con DevAuth y la base de prueba ya sembrada (opcional) | La API arranca igual (el sembrador detecta `PRY-DEV-*` y no hace nada) |

## 6. Pendientes
- 84: se cierra cuando confirmes el commit de K6.
- Siguiente: tus ajustes menores; luego el Lote 2 (83, 77) y la Fase 3.
