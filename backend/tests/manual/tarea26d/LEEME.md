# TAREA-26d-1 — Backend: empleados desde la API (verificación con scripts)

> **No use las pantallas de "Nuevo proyecto", "Actualizar personal" ni "Reactivar" hasta la TAREA-26d-2.** El buscador del frontend todavía espera el `id` numérico y las solicitudes con `empleadoId`. La verificación de esta subtarea es **solo con scripts**.
>
> **Datos sensibles.** Pegue en el chat **solo** los `resultado-*.txt` y los conteos de `conteos.sql`. Los scripts muestran códigos EKON y conteos, **nunca nombres**.

## Requisitos
- API reiniciada con este código.
- Para V1, V3, V9 y V10: modo **Http** (user-secrets de la 26a; ver `..\tarea26a\LEEME.md`).
- Para V8: modo **Simulado**.

## Archivos
| Archivo | Caso | ¿Escribe? |
|---|---|---|
| `buscar.cmd` | V1: GET `/api/empleados` como gestor. Muestra el total, la página, si hay `avisoErp` y los códigos de los primeros 10. Con `set TEXTO=<apellido>` prueba también el texto con y sin tildes | No |
| `crear-proyecto.cmd` | V3: vista previa y, **solo si confirma** (escribiendo `REGISTRAR` o con `set REGISTRAR=SI`), creación del proyecto con empleados reales por `codigoEkon` | **Sí, al confirmar** |
| `transicion.cmd` | V10: vista previa con solo `empleadoId`, solo `codigoEkon`, ambos (400) y ninguno (400) | No |
| `conteos.sql` | V9: conteos de `Empleado` y `CargoInfor`, en SSMS | No (solo lectura) |
| `crear-proyecto.ejemplo.json` | Plantilla del cuerpo de creación | — |

Las salidas `resultado-*.txt` y los `*.json` que no sean `*.ejemplo.json` están en `.gitignore`.

## Orden
1. **V1** — `buscar.cmd` (opcional: `set TEXTO=<un apellido con tilde>`).
2. **V3** — Copie `crear-proyecto.ejemplo.json` a `crear-proyecto-1.json` y complete los datos:
   - `companiaId`, `proyectoErpId`, `actividadId` y `horarioCodigo` reales (los de "Nuevo proyecto" en modo Http, o los del Id 14):
     - `companiaId` y `horarioCodigo`: números sin comillas (`Id` de `/api/erp/companias`, `codigo` de `/api/erp/horarios`);
     - `proyectoErpId`: texto entre comillas, `id` de `/api/erp/companias/{companiaId}/proyectos`;
     - `actividadId`: **texto entre comillas** (p. ej. `"20"`), `id` de `/api/erp/companias/{companiaId}/proyectos/{proyectoErpId}/actividades` del proyecto elegido. Un número sin comillas da 400 en `actividadId` ("Debe ser texto entre comillas"; antes de la corrección del 08/10/2026 daba 500);
     - `principalRelacionado` del back: número (posición 1..n del principal en `principales`), como en `CrearProyectoSolicitud`;
   - `codigoEkon` reales del principal y del back (por ejemplo, de `buscar.cmd`);
   - fechas futuras.

   JSON válido, sin comentarios. Ejecute `crear-proyecto.cmd`: primero muestra la vista previa (los `empleadoId` negativos son personas sin fila, que se darán de alta al registrar) y después pide confirmación.
3. **V9** — `conteos.sql` en SSMS. Deben aparecer los empleados del paso 2, con su `UltimaCopiaApiUtc`, asignados a 1 proyecto, y con `NoDevConCedula = 0`.
4. **V10** — `set EMPLEADO_ID=<Id de la consulta 3 de conteos.sql>`, `set CODIGO_EKON=<un código activo>` y `transicion.cmd`.
5. **V8** (modo Simulado: `dotnet user-secrets remove "ServiciosExternos:Modo" --project src/App.Api` y reiniciar la API):
   - `buscar.cmd` → DEV001–DEV008 y SIM001…;
   - `crear-proyecto.cmd` con códigos `SIM…` (sin cruces) y la compañía 9001 / DEV-ERP-001 / DEV.01 / horario 1 del simulado.

   Vuelva después a Http (user-secrets de la 26a).

## Modo simulación (sin red)
`set SIMULAR=1` antes de ejecutar un `.cmd`. Las respuestas se leen de `%SIMULACION%` (por defecto `.\simulacion`):
- `buscar_1.json` … `buscar_4.json`;
- `crear_previsualizar.json` y `crear_registrar.json`;
- `transicion_a.json` … `transicion_d.json`.

El código HTTP va en un archivo con el mismo nombre y extensión `.codigo`. Ninguna función abre conexiones (ver `..\tarea26a\comun.ps1`).
