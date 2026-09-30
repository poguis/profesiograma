# FASE 5 — Diseño: Crear proyecto (motor de cronograma, cruces y registro)

**Fecha:** 2026-09-30 · **Estado:** aprobado por el usuario (2026-09-30), decisiones en la sección 11
**Fuentes:** `FASE_1_Analisis_Funcional.md` §2.3 y §3 (RN01–RN18), `FASE_2_Modelo_Datos.md` §2.1–2.5,
fórmulas reales de `docs/origen/powerapps/GestionProyecto_pa.yaml` (botones **Generar** y **Registrar**).

---

## 1. Alcance

Crear un proyecto nuevo: cabecera, personal (principales y backs), generación del cronograma diario,
detección de cruces y registro atómico. **Fuera de este diseño:** editar proyecto, cambiar estado,
fecha de corte (RN11), novedades y reporte Excel.

## 2. Flujo del usuario (pantalla nueva)

| Paso | Qué hace | Llamada a la API |
|---|---|---|
| 1. Cabecera | Compañía → Grupo → (CAMPO: Proyecto ERP → Actividad) / (PLANTA u OFICINAS: Dimensión) → Fechas → Horario → Almuerzo | Catálogos ERP (sección 4) |
| 2. Personal | Agregar principales (con jornada) y backs (Jornada/Descanso + días de descanso posterior) | `GET /api/empleados` |
| 3. Vista previa | Ver tramos, calendario por persona y **cruces** | `POST /api/proyectos/previsualizar` |
| 4. Registrar | Solo si no hay cruces | `POST /api/proyectos` |

**Regla clave:** el servidor **recalcula todo** al registrar. La vista previa es solo informativa; nunca se confía en lo que envía el navegador.

## 3. Reglas de negocio (implementación)

| Id | Regla | Capa |
|---|---|---|
| RN01 | Código `PRY-yyyyMMdd-XXXXXX`: fecha de hoy en **Ecuador** + últimos 6 caracteres hex del `Uid` (sin guiones, en mayúsculas o minúsculas según el dato existente [ver P6]). Índice único; si colisiona, se regenera el `Uid` (máx. 3 intentos) | Domain (generador) + Infrastructure |
| RN02 | CAMPO exige proyecto ERP + actividad. PLANTA / OFICINAS ADMINISTRATIVAS exige dimensión. Se toma de los flags `RequiereProyectoErp` / `RequiereDimension` del catálogo `GrupoProyecto` (no se codifican nombres) | Application (validador) |
| RN03 | Jornadas desde el catálogo `Jornada` (TIPO_1 22/8, TIPO_2 11/4, TIPO_3 5/2, ESPECIAL 3/0) | BD |
| RN04–RN06 | Motor de cronograma (sección 5) | **Domain, lógica pura** |
| RN07 | Cruces internos y con otros proyectos (sección 6) | Domain (internos) + Infrastructure (externos) |
| RN08 | Fechas de todo el personal dentro del rango del proyecto; fin ≥ inicio | Application |
| RN09 | Almuerzo: salida 11:00–14:00, regreso 12:00–15:00 y regreso > salida (ya existe `CK_Proyecto_Almuerzo`) | Application + BD |
| RN12 | Etapa versión 1 `CREACION`, estado `ACTIVO`, `FechaCorte = FechaInicio`, snapshot JSON del personal | Application |
| RN13 | Actividad versión 1 `CREACION` (solo si hay actividad), vigencia = rango del proyecto | Application |
| RN18 | Máximo de principales y backs desde `Parametro` (20 / 20) | Application |
| P1 | Mínimo de principales = **0** (no obligatorio). La regla existe en el validador con la constante `MinimoPrincipales = 0` y un comentario que indica cómo exigir 1 o más | Application |
| — | Nombre visual: nombre del proyecto ERP; si no hay, `"PLANTA - " + descripción de la dimensión` (igual que la app original, también para OFICINAS) | Domain |
| — | Estado inicial `ACTIVO`; propietario = usuario actual; departamento = [P5] | Application |

## 4. Datos externos

Las APIs del ERP (`https://backstack.sedemi.com`, puertos 7048/7055, sin autenticación) alimentan la cabecera:

| Dato | Endpoint externo | Endpoint propio (proxy con caché corta) |
|---|---|---|
| Compañías | `GET :7048/api/Company/list_company` | `GET /api/erp/companias` |
| Proyectos ERP (solo `status = "Activo"`) | `GET :7048/api/Project/GetProject/{companyId}` | `GET /api/erp/companias/{id}/proyectos` |
| Dimensiones (UEGP) | `GET :7048/api/Uegp/GetUegpCompany/{companyId}` | `GET /api/erp/companias/{id}/dimensiones` |
| Actividades | `GET :7055/api/Activity/GetActivitiesProject/{projectId}/{companyId}` | `GET /api/erp/companias/{id}/proyectos/{projectId}/actividades` |
| Horarios (solo `status = "A"`) | `GET :7055/api/PayrollSchedule/ListPayrollSchedule` | `GET /api/erp/horarios` |

- Contrato en Application: `ICatalogoErp`. Dos implementaciones en Infrastructure, elegidas por configuración `ServiciosExternos:Modo`:
  - `Http` → llama a las APIs reales (HttpClient con timeout y manejo de error → 503 "Servicio ERP no disponible").
  - `Simulado` → datos fijos de prueba coherentes con la compañía 9001 y `DEV-ERP-001` (solo Development).
- Al registrar, la compañía elegida se guarda/actualiza en la tabla `Compania` (caché).
- **Empleados:** se buscan en la tabla local `Empleado` (activos, sin datos sensibles). La sincronización con `EvolutionEmployee` es una tarea aparte; en desarrollo se usan DEV001–DEV008.

## 5. Motor de cronograma (RN04–RN06) — replica exacta de la app original

Entrada: rango del proyecto, lista de principales y backs. Salida: **tramos** (para la vista previa) y **días** (`ProyectoAsignacionDia`).

### 5.1 Principal (rol PRINCIPAL, tipo AUTO)

```
ciclo   = DiasTrabajo + DiasDescanso
total   = (FechaFin - FechaInicio) + 1
bloques = techo(total / ciclo)
para b = 1..bloques:
    inicioBloque = FechaInicio + (b-1)·ciclo
    si inicioBloque > FechaFin → fin
    finBloque = min(inicioBloque + DiasTrabajo - 1, FechaFin)
    tramo PRINCIPAL/AUTO bloque b; un día por fecha
```

### 5.2 Back (tipo MANUAL)

```
rol = TipoRegistro == DESCANSO ? DESCANSO : BACK
tramo rol/MANUAL, bloque = número del back, de FechaInicio a FechaFin
si TipoRegistro == JORNADA y DiasDescanso > 0:
    tramo DESCANSO/MANUAL de FechaFin+1 a FechaFin+DiasDescanso   ← NO se recorta al rango del proyecto (P3);
                                                                     el REPORTE Excel sí lo recorta a la fecha fin del proyecto
```

### 5.3 Descansos automáticos del principal (se agregan al final, antes de guardar)

```
para cada tramo PRINCIPAL:
    siguiente = finBloque + 1
    si DiasDescanso > 0 y siguiente <= FechaFin del principal
       y la persona NO tiene ningún día (de cualquier rol) en `siguiente` dentro de este proyecto:
        agregar DESCANSO/AUTO (mismo bloque) para d = 1..DiasDescanso con finBloque+d <= FechaFin del principal
```

Nota: igual que la app original, solo se revisa que el **primer** día después del bloque esté libre, y los descansos se calculan automáticamente hasta la fecha fin de esa persona (P4).

### 5.4 Deduplicación

Clave única `(EmpleadoId, Fecha, Rol)` dentro del proyecto (coincide con el índice único de `ProyectoAsignacionDia`).
Si hay duplicados, se conserva el primero generado (orden: principales, backs, descansos automáticos).

### 5.5 Ejemplos (se convierten en pruebas unitarias)

| # | Caso | Resultado esperado |
|---|---|---|
| E1 | Principal TIPO_2 (11/4), 01/10/2026–31/10/2026 | Ciclo 15, 3 bloques: PRINCIPAL 01–11, 16–26, 31. DESCANSO auto 12–15 y 27–30. Tras el bloque 3 no hay (el 01/11 está fuera) |
| E2 | Principal TIPO_1 (22/8), 01/10–10/10 | 1 bloque 01–10 (recortado). Sin descanso |
| E3 | Principal ESPECIAL (3/0), 01/10–07/10 | Ciclo 3: bloques 01–03, 04–06, 07. Sin descansos (DiasDescanso = 0) |
| E4 | Back JORNADA 12/10–15/10, DiasDescanso 2 | BACK 12–15, DESCANSO 16–17 |
| E5 | Back DESCANSO 12/10–15/10 | DESCANSO/MANUAL 12–15, sin descanso posterior |
| E6 | Principal TIPO_2 01–31/10 + la **misma persona** como back 12/10 | El 12/10 está ocupado → no se genera el descanso automático del bloque 1 |
| E7 | Principal y back distintos, mismo día | Sin cruce |
| E8 | Misma persona PRINCIPAL y BACK el mismo día | Cruce interno |
| E9 | Misma persona PRINCIPAL y DESCANSO el mismo día | Sin cruce (DESCANSO se ignora) |
| E10 | Rango de 1 día | 1 bloque de 1 día |

## 6. Cruces (RN07)

- **Internos:** misma persona y fecha con más de un registro cuyo rol ≠ DESCANSO.
- **Con otros proyectos:** consulta SQL indexada sobre `ProyectoAsignacionDia` + `Proyecto`:
  mismos `EmpleadoId`, fechas en el rango del nuevo proyecto, rol ≠ DESCANSO en ambos lados,
  proyecto con estado `EsVigente` (ACTIVO / SUSPENDIDO) y `Eliminado = 0`.
  Reemplaza la lectura del JSON `ASIGNACIONES` (problema C6).
- Los cruces se calculan con los días **antes** de agregar descansos automáticos (igual que el original; los descansos no generan cruce).
- Resumen para la pantalla: agrupado por persona / rol / proyecto / mes, con la lista de días ("3, 4, 5").
- **Hay cruces → no se registra** (409 Conflict con la lista). Igual que la app original.

## 7. API

### 7.1 `POST /api/proyectos/previsualizar` (Gestor) y `POST /api/proyectos` (Gestor)

Mismo cuerpo:

```json
{
  "companiaId": 9001,
  "grupo": "CAMPO",
  "proyectoErpId": "DEV-ERP-001",
  "actividadId": "DEV.01",
  "dimensionUegpId": null,
  "fechaInicio": "2026-10-01",
  "fechaFin": "2026-10-31",
  "horarioCodigo": 1,
  "salidaAlmuerzo": "13:00",
  "regresoAlmuerzo": "14:00",
  "principales": [
    { "empleadoId": 1, "jornada": "TIPO_2", "fechaInicio": "2026-10-01", "fechaFin": "2026-10-31", "cargo": null }
  ],
  "backs": [
    { "empleadoId": 3, "tipoRegistro": "JORNADA", "diasDescanso": 2,
      "fechaInicio": "2026-10-12", "fechaFin": "2026-10-15", "principalRelacionado": 1, "observacion": null }
  ]
}
```

- Nombres, descripciones, RUC, datos del horario y de la actividad **no los envía el navegador**: el servidor los obtiene de `ICatalogoErp` por Id (evita datos manipulados).
- `previsualizar` → 200 `{ tramos[], dias[] (resumido por persona), cruces[], resumen }`. No guarda nada.
- `POST /api/proyectos` → 201 `{ id, codigo }` + `Location`; 400 validación; 409 cruces; 503 ERP no disponible.

### 7.2 Apoyo

- `GET /api/empleados?texto=&soloMisDepartamentos=true|false&pagina=&tamano=` (Gestor): Id, código EKON, nombre, cargo, departamento. Sin cédula ni correo.
  - `soloMisDepartamentos=true` (por defecto): solo empleados cuyo departamento/unidad coincide **por nombre** con los departamentos del usuario en `UsuarioDepartamento`.
  - `false`: toda la estructura (equivale al ícono "Ver otros departamentos" de la app original). No es control de seguridad, es un filtro de ayuda.
  - Admin sin departamentos asignados: ve todos.
- Endpoints ERP de la sección 4.

## 8. Registro (una sola transacción)

1. Validar (sección 3) y obtener datos ERP.
2. Generar cronograma + cruces internos. Consultar cruces externos.
3. Si hay cruces → 409.
4. `BEGIN TRANSACTION`: upsert `Compania` → `Proyecto` → `ProyectoPersonal` → `ProyectoAsignacionDia` (incluye descansos automáticos) → `ProyectoEtapa` v1 → `ProyectoActividad` v1.
5. **Repetir la consulta de cruces externos dentro de la transacción** antes de confirmar (otro usuario pudo registrar al mismo tiempo). Si aparecen → rollback + 409.
6. `COMMIT`. La auditoría la llena el interceptor.

La app original guardaba en 10 pasos sin transacción (podía dejar proyectos a medias); esto lo corrige.

## 9. Ubicación por capa

| Capa | Contenido |
|---|---|
| Domain | `Cronograma/MotorCronograma` (puro, sin EF ni fechas del sistema), `CruceDetector` (internos), `GeneradorCodigoProyecto`, value objects de entrada/salida |
| Application | `CrearProyecto` (caso de uso), validador, `ICatalogoErp`, `IConsultaCrucesExternos`, `IProyectoRepositorio`, DTOs |
| Infrastructure | `CatalogoErpHttp`, `CatalogoErpSimulado`, consulta de cruces (EF/SQL), repositorio con transacción |
| Api | `ProyectoEndpoints` (POST), `ErpEndpoints`, `EmpleadoEndpoints` |
| Tests | `tests/App.Domain.Tests` (xUnit): E1–E10 + bordes. Luego `App.Application.Tests` para validaciones |

## 10. Plan de tareas

| Tarea | Contenido | Toca BD |
|---|---|---|
| **10** | Motor de cronograma + cruces internos en Domain + proyecto de pruebas xUnit (E1–E10 y bordes) | No |
| **11** | `ICatalogoErp` (Http + Simulado), endpoints ERP y `GET /api/empleados` | Solo lectura |
| **12** | Caso de uso `CrearProyecto`, cruces externos, `previsualizar` y `POST /api/proyectos` con transacción | Sí (escribe) |
| **13** | Pantalla "Nuevo proyecto" en React (cabecera, personal, vista previa, registrar) | — |

## 11. Decisiones del usuario (2026-09-30)

| # | Tema | Decisión |
|---|---|---|
| P1 | Mínimo de principales | **No obligatorio** (mínimo 0). Queda la regla preparada con la constante `MinimoPrincipales` y comentario para exigir 1 o más en el futuro |
| P2 | Primer principal = inicio del proyecto | **Se sugiere** la fecha, no se exige |
| P3 | Descanso posterior del back fuera del rango | **Se guarda completo** (no se recorta). El **reporte Excel** lo recorta a la fecha fin del proyecto (anotar en RN15) |
| P4 | Descanso automático del principal | **Igual que el original**: automático por jornada, hasta la fecha fin de esa persona; solo se revisa el primer día libre |
| P5 | Departamento | `UsuarioDepartamento` indica a qué departamento(s) pertenece el usuario. Sirve para: (a) filtrar por defecto la búsqueda de empleados, con opción "ver todos" (ícono de la app original); (b) asignar `Proyecto.DepartamentoId`: si el usuario tiene uno, ese; si tiene varios, lo elige; opcional |
| P6 | Código | Sufijo en **minúsculas**, igual que los existentes |