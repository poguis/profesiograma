# TAREA-26a — ERP real en Development: scripts de sondeo y verificación

Los ejecuta el **usuario**. Los scripts solo leen, a excepción del caso A6 de la verificación visual (crear un proyecto), que se hace desde la pantalla.

> **Datos sensibles.** Pegue en el chat **solo** los archivos `resultado-*.txt`. Contienen solo agregados: conteos, tipos, largos y los valores de la lista permitida.
> **No comparta respuestas crudas del ERP**, ni capturas de Postman o del navegador con datos de empleados.

## Archivos
| Archivo | Caso | Llama a | Qué muestra |
|---|---|---|---|
| `conectividad.cmd` | A1 | ERP real (`backstack.sedemi.com` 7048/7055) | TCP 7048 y 7055; GET `list_company`: solo código, tiempo y bytes |
| `sondeo-empleados.cmd` | A2 | ERP real: `POST :7048/api/EvolutionEmployee/EmployeesEvolution` | Forma de la respuesta y solo agregados (ver el encabezado de `sondeo-empleados.ps1`, con la **lista permitida**) |
| `erp-horarios.cmd` | A5 (pendiente 13) | **Nuestra API** (`GET /api/erp/horarios`, `X-Dev-User: gestor`) | Horarios activos y resumen por `tipo` (M/D) |
| `erp-largos.cmd` | A5 (pendiente 17) | **Nuestra API** (compañías, proyectos, dimensiones y actividades de los primeros N proyectos) | Largo máximo de los Id frente a las columnas (30/30/20) |
| `cuerpo-empleados.ejemplo.json` | A2 | — | Plantilla del cuerpo confirmado (todos los activos de todas las empresas) |

Salidas: `resultado-conectividad.txt`, `resultado-sondeo.txt`, `resultado-horarios.txt` y `resultado-largos.txt`. Están en `.gitignore`, igual que los `cuerpo-empleados-*.json`.

## Orden de ejecución
1. **A1** — `conectividad.cmd` (no requiere la API).
2. **A2** — Copie `cuerpo-empleados.ejemplo.json` a `cuerpo-empleados-1.json` (sin cambios: `parameter` vacío y `estado` "A") y ejecute `sondeo-empleados.cmd`.
   - Opcional: más cuerpos (`cuerpo-empleados-2.json`, …), por ejemplo con un `codEmpresa`. Cada uno se analiza por separado.
   - Si `parameter` lleva un valor (código o cédula), el script **no lo muestra**.
   - El tiempo de espera es de 180 s (`set TIMEOUT_SEGUNDOS=300` para ampliarlo).
3. **A3** — Active el modo Http (abajo), reinicie la API y confirme en la consola de la API la línea `ERP: modo Http`.
4. **A5** — Con la API en modo Http: `erp-horarios.cmd` y `erp-largos.cmd`.
   - `set ERP_N_ACTIVIDADES=40` cambia cuántos proyectos se consultan para actividades (por defecto 20).
   - `set API_BASE=…` si la API no está en `https://localhost:7180`.
5. Casos de pantalla A4, A6–A10 (ver `docs/tareas/TAREA-26a-reporte.md`) y, al final, **A11**: volver a Simulado.

## Activar y desactivar el ERP real (desde `backend/`)
Los user-secrets tienen prioridad sobre `appsettings.Development.json` (que fija `Simulado`). Las URL base vienen de `appsettings.json`. **Después de cada cambio hay que reiniciar la API.**

Activar el modo Http (A3):
```
dotnet user-secrets set "ServiciosExternos:Modo" "Http" --project src/App.Api
```

Volver a Simulado (A11):
```
dotnet user-secrets remove "ServiciosExternos:Modo" --project src/App.Api
```

Provocar el 503 de forma controlada (A9, con la API en modo Http): URL base inválida.
```
dotnet user-secrets set "ServiciosExternos:ErpBase7048" "https://localhost:1" --project src/App.Api
dotnet user-secrets set "ServiciosExternos:ErpBase7055" "https://localhost:1" --project src/App.Api
```

Restaurar las URL reales después de A9:
```
dotnet user-secrets remove "ServiciosExternos:ErpBase7048" --project src/App.Api
dotnet user-secrets remove "ServiciosExternos:ErpBase7055" --project src/App.Api
```

> `dotnet user-secrets list --project src/App.Api` muestra también la cadena de conexión: **no pegue su salida en el chat**.
> No cambie el `UserSecretsId` del proyecto.

## Logs del ERP (A3 y A10)
- **Al arrancar** (Information): `ERP: modo Http` o `ERP: modo Simulado`. No muestra URL ni secretos.
- **Por cada llamada real al ERP** (Debug, categoría `App.Infrastructure.Erp.CatalogoErpHttp`): `ERP horarios: HTTP 200 en 153 ms`. Nunca muestra el cuerpo ni la URL.
  - Las respuestas en caché (compañías y horarios 10 min; proyectos, dimensiones y actividades 5 min) **no** generan línea. Así se comprueba la caché en A10.
  - En Development ya está activo: `appsettings.Development.json` tiene `"App": "Debug"`. Si no aparece, súbalo solo para esa categoría:
    ```
    dotnet user-secrets set "Logging:LogLevel:App.Infrastructure.Erp.CatalogoErpHttp" "Debug" --project src/App.Api
    ```

## Modo simulación (sin red)
`set SIMULAR=1` antes de ejecutar un `.cmd`. Ninguna función abre conexiones:
- `Invocar-Http` (la única que usa la red, en `comun.ps1`) se detiene si se llama en simulación;
- las URL base se fuerzan a `https://localhost:1`.

Las respuestas se leen de `%SIMULACION%` (por defecto `.\simulacion`). El nombre del archivo es la ruta con todo lo que no sea letra o número cambiado por `_`, más `.json`; el código HTTP opcional va en un archivo con el mismo nombre y extensión `.codigo`. Ejemplos:
- `api_erp_horarios.json`;
- `empleados_cuerpo_empleados_1.json`, que es la respuesta para `cuerpo-empleados-1.json`.
