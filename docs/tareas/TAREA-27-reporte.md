# TAREA-27 — Reporte: pendientes 18 (parámetros de almuerzo) y 67 (`tipoConflicto`)

**Fecha:** 29/10/2026 · **Fase:** B (implementación del plan de la Fase A aprobado) · **Estado:** ✅ verificada por el usuario el 29/10/2026 (M1–M4; backend 1163/1163, frontend 404/404)

El detalle está en dos documentos:
- `docs/fases/FASE_5_Novedades.md` §1: tabla de `tipoConflicto`.
- `docs/fases/FASE_2_Modelo_Datos.md` §2.7: nota de la eliminación.

## 1. Qué se hizo
- **Pendiente 18:**
  - se quitaron del `HasData` de `ParametroConfiguracion` los Id 6 y 7, que quedan como huecos y no se reutilizan;
  - la migración `20261029201119_QuitarParametrosAlmuerzo` se **creó en Release y no se aplicó**. Su `Up` borra las filas 6 y 7; su `Down` las vuelve a insertar con los valores de la semilla;
  - el snapshot solo pierde esas dos entradas: los Id 1–5 y 8–11 no cambian.
- **Pendiente 67, backend:**
  - en Application, `TipoConflictoNovedad` (`Duplicado`, `Cambio`, `Anulada`, `RegistroConcurrente`) y las fábricas `ResultadoModificacionNovedad.Anulada(mensaje)` y `Cambio()`;
  - en App.Api, el helper `Endpoints/ProblemasConflicto.cs`, que devuelve un 409 con `tipoConflicto` y extensiones adicionales;
  - lo usa **solo** `NovedadEndpoints`, en crear, editar y anular.
- **Pendiente 67, frontend:** `erroresNovedad.ts` decide **solo** por `tipoConflicto`:
  - `duplicado` → sin «Recargar» y con «Ver {codigo}»;
  - `registroConcurrente` → «Reintentar» (P3);
  - `cambio`, `anulada`, un valor desconocido o sin tipo → 409 genérico con «Recargar» (P4).

  Se quitaron `MENSAJE_DUPLICADO` y la comparación por texto.
- **Documentación:**
  - `FASE_5_Novedades.md` y `FASE_2_Modelo_Datos.md`;
  - `00_ESTADO_ACTUAL.md`: 18 y 67 resueltos, nuevo pendiente 76, siguiente paso y hoja de ruta;
  - `HISTORIAL.md` H21.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `backend/src/App.Infrastructure/Persistencia/Configuraciones/ParametroConfiguracion.cs` | Sin los Id 6 y 7 (con un comentario) |
| `backend/src/App.Infrastructure/Persistencia/Migraciones/20261029201119_QuitarParametrosAlmuerzo.cs` (+ `.Designer.cs`), `ProfesiogramaDbContextModelSnapshot.cs` | Migración nueva y snapshot |
| `backend/src/App.Application/Proyectos/Crear/ReglasAlmuerzo.cs` | Comentario |
| `backend/src/App.Application/Novedades/NovedadModificacionContratos.cs`, `NovedadModificacionServicio.cs` | `TipoConflictoNovedad`, `Anulada()` y `Cambio()` |
| `backend/src/App.Api/Endpoints/ProblemasConflicto.cs` (nuevo), `NovedadEndpoints.cs` | Helper y su uso |
| `backend/tests/App.Api.Tests/NovedadEndpointsTests.cs`, `backend/tests/App.Application.Tests/Novedades/NovedadModificacionTests.cs` | 5 pruebas nuevas; dobles ampliados (novedad 8 anulada, duplicados y alta concurrente por fecha) |
| `frontend/src/features/novedades/erroresNovedad.ts` (+ `.test.ts`) | Decisión por `tipoConflicto` |
| `docs/fases/FASE_5_Novedades.md`, `docs/fases/FASE_2_Modelo_Datos.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` | Documentación |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1163/1163** (1158 + 5); ninguna prueba existente se ajustó |
| `dotnet ef migrations add QuitarParametrosAlmuerzo … --configuration Release` | Creada; **no** se ejecutó `database update` |
| `npm run build` / `npm run lint` / `npm test` | Correcto / 0 / **404/404** (399 + 5 nuevas; **3 ajustadas**) |
| `00_ESTADO_ACTUAL.md` | 24 560 B. Lo quitado va textual a H21. Al comparar con `HEAD` solo difiere la línea «Última actualización» |

SQL de `dotnet ef migrations script 20261016164745_ParametroHorariosNoLaborales 20261029201119_QuitarParametrosAlmuerzo` (generado sin conectarse a la base):
```sql
BEGIN TRANSACTION;
DELETE FROM [dbo].[Parametro] WHERE [Id] = 6;  SELECT @@ROWCOUNT;
DELETE FROM [dbo].[Parametro] WHERE [Id] = 7;  SELECT @@ROWCOUNT;
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029201119_QuitarParametrosAlmuerzo', N'10.0.12');
COMMIT;
```
Con `--idempotent`, cada una de las tres sentencias va dentro de `IF NOT EXISTS (SELECT * FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261029201119_QuitarParametrosAlmuerzo') BEGIN … END`; los `DELETE` van como `EXEC(N'DELETE FROM [dbo].[Parametro] WHERE [Id] = 6; SELECT @@ROWCOUNT')`. La reversa (`… QuitarParametrosAlmuerzo ParametroHorariosNoLaborales`) reinserta los Id 6 y 7 con `IDENTITY_INSERT ON/OFF` y borra la fila del historial.

**Para aplicarla (lo hace el usuario, desde `backend/`):**
`dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --configuration Release`

## 4. Desviaciones, errores y decisiones
- **⚠ Git:** `.gitignore:51` ignora `backend/src/App.Infrastructure/Persistencia/Migraciones/`. Las migraciones anteriores están versionadas porque se agregaron antes de esa regla, pero los dos archivos nuevos **no aparecen en `git status`**. Para el commit: `git add -f backend/src/App.Infrastructure/Persistencia/Migraciones/20261029201119_QuitarParametrosAlmuerzo*.cs`. El snapshot sí aparece como modificado. No cambié el `.gitignore` porque está fuera del alcance. **Corregido en el cierre** (§6).
- **3 pruebas de Vitest ajustadas** (P4), como anticipó la Fase A: los dos casos de duplicado de la 24b y el de la 24c ahora envían `tipoConflicto: 'duplicado'`. `MENSAJE_DUPLICADO` dejó de exportarse; el test usa el texto como constante local. Las demás pruebas de 409 sin tipo («cambió», «anulada») siguen igual, porque el resultado es el mismo («Recargar»).
- **Error propio corregido:** usé `AAAAAAAAB9F=` como «otra versión» y daba 200. Esa cadena se decodifica en los mismos bytes que `AAAAAAAAB9E=`: solo difieren los bits de relleno de Base64. Ahora uso `AAAAAAAACQk=`.
- **Diseño:** `ResultadoModificacionNovedad.Conflicto(string)` se reemplazó por `Anulada(mensaje)` y `Cambio()`, que fijan el tipo. `MensajeConflicto` se conserva, así que las pruebas de Application existentes no cambiaron. `EstadoCrearNovedad.Cambiado` se mapea en la API a `registroConcurrente`.
- **Ubicación del helper:** `Endpoints/ProblemasConflicto.cs` (público, junto a los endpoints, que son quienes lo usan). Sus constantes `Tipos.*` sirven para reutilizarlo en el pendiente 76.
- **Pendiente 76** agregado con el texto pedido.

## 5. Verificación para el usuario
Requisitos: aplicar la migración (§3), **reiniciar la API** (endpoints cambiados) y `npm run dev`.

| # | Pasos | Esperado | Estado de la base |
|---|---|---|---|
| M1 | SSMS antes y después de `database update`: `SELECT Id, Clave, Valor FROM dbo.Parametro ORDER BY Id;` y `SELECT TOP 3 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;` | Antes 11 filas (1–11); después 9, sin el 6 ni el 7; los Id 1–5 y 8–11 sin cambios; `20261029201119_QuitarParametrosAlmuerzo` en el historial | −2 filas en `Parametro` |
| M2 | «Nuevo proyecto» y «Editar cabecera»: desplegables de almuerzo | Las mismas opciones que antes | — |
| M3 | Registrar una novedad igual a una existente (mismo tipo, sujeto y fecha) | «Ya existe una novedad de ese tipo…» y «Ver NOV-…», sin «Recargar»; en F12 → Red, el 409 con `"tipoConflicto": "duplicado"` | Sin filas nuevas |
| M4 | Dos pestañas con la misma novedad de prueba: anular en una y luego guardar una edición en la otra | 409 «La novedad está anulada.» con «Recargar»; `"tipoConflicto": "anulada"` | 1 novedad de prueba anulada (no se revierte) |

## 6. Verificación del usuario (29/10/2026)
| Punto | Resultado |
|---|---|
| Respaldo previo | COPY_ONLY verificado (`RESTORE VERIFYONLY` válido): `D:\BACKUP\PROFESIOGRAMA_DEV_20261029_antes_TAREA27.bak`, en el servidor NIQUEL\SSDEV |
| Recuperación | `PROFESIOGRAMA_DEV` pasó de FULL a SIMPLE: no tenía respaldos del log y en FULL el log puede crecer sin límite |
| Migración | El usuario la aplicó en SSMS con el script generado (no idempotente). **Desviación:** el `BEGIN TRANSACTION` no se ejecutó (selección parcial), así que las sentencias corrieron en autocommit. El resultado es equivalente: `@@TRANCOUNT = 0` y EF sin migraciones pendientes |
| M1 | OK: 9 parámetros; el Id 8 se conserva |
| M2 | OK |
| M3 / M4 | OK: `tipoConflicto` «duplicado» y «anulada» comprobados en F12 → Red. Queda una novedad de prueba anulada |

**Cierre documental:**
- `.gitignore`: la regla de la línea 51 (`backend/src/App.Infrastructure/Persistencia/Migraciones/`, sección «Ruido generado (opcional)») se reemplazó por la excepción explícita `!backend/src/App.Infrastructure/Persistencia/Migraciones/`. La regla la agregó el commit `5cb3b2e` (21/10/2026), cuando las migraciones anteriores ya estaban versionadas: git sigue los archivos ya versionados aunque estén ignorados, por eso solo afectó a las nuevas. Comprobado: `git check-ignore -v` no da salida para los dos archivos nuevos, `git status` los muestra como «untracked», y `bin/` y `obj/` dentro de la carpeta siguen ignorados (`**/bin/`, `**/obj/`).
- `00_ESTADO_ACTUAL.md`: entorno (§4), pendientes 7, 65 y 75, y siguiente tarea Fase 7a; `HISTORIAL.md` H22.

## 7. Pendientes
- Pendiente 76 (alta de usuarios con `tipoConflicto`, después de producción).
