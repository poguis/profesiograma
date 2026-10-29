# FASE 5 — Usuarios y departamentos (TAREA-25)

| Subtarea | Alcance | Estado |
|---|---|---|
| 25-1 | Backend: endpoints de administración, reglas, invalidación de la caché, DevAuth `gestor2`, script `tarea25/usuarios.cmd` | ✅ Verificada por el usuario (29/10/2026) |
| 25-2 | Frontend: centro `/admin` con tarjetas, lista y edición de usuarios (§9); mensaje propio del 503 por usuario | ✅ Verificada por el usuario (29/10/2026) |
| 25-3-1 | CargoInfor, backend: «Importar puestos desde la API» y edición de `CodigoInfor` (pendiente 2; §10) | ✅ Verificada por el usuario (29/10/2026) |
| 25-3-2 | CargoInfor, frontend: tarjeta «Cargos Infor» en `/admin`, listado, edición e importación (§10.6) | ✅ Verificada por el usuario (29/10/2026) |

Reemplaza el popup de SuperAdmin y el JSON `PermisosGenerales` de la fila 666 (FASE_1 §2.2 y §4; J4 de FASE_2). **Sin migración:** usa las tablas `Usuario`, `Departamento` y `UsuarioDepartamento` de la Fase 2.

## 1. Qué decide cada cosa
- **Acceso:** los roles (App Roles de Entra ID `Admin` y `Gestor`; DevAuth en desarrollo). La app **no** guarda ni administra roles. Dar de alta a alguien aquí no le da acceso: sin el App Role recibe 403.
- **Visibilidad de proyectos:** el propietario (R1); el Admin ve todo. El departamento **no** restringe la visibilidad.
- **Departamentos del usuario:** solo sugieren personas en el buscador y fijan el departamento de un proyecto nuevo (P5). Ver §5.
- **Baja (`Usuario.Activo = 0`):** bloquea el acceso en la **siguiente** solicitud (403 «Usuario no habilitado», detail «El usuario está inactivo en la aplicación.»).

## 2. Contratos (todos con `Politica.Admin`; el gestor recibe 403)
| Endpoint | Entrada | Respuestas |
|---|---|---|
| `GET /api/admin/usuarios` | `texto` (correo o nombre, contiene), `incluirInactivos` (false), `pagina`, `tamano` (25; ≤ 100) | 200 `PaginaResultado<{ id, email, nombreMostrar, activo, conIdentidad, departamentos: [{ id, nombreCorto }] }>`, orden por correo, sin SISTEMA, una sola consulta; 400 en los parámetros |
| `GET /api/admin/usuarios/{id}` | — | 200 lo anterior + `departamentosDisponibles: [{ id, nombre, nombreCorto, activo, asignado }]` (activos + inactivos ya asignados, por `Orden`) y `esUsuarioActual`; 404 si no existe o es SISTEMA |
| `POST /api/admin/usuarios` | `{ email, nombreMostrar }` | 201 `{ id }` + Location; 400 en `email` / `nombreMostrar`; 409 «Ya existe un usuario con ese correo.» con `usuarioId` (sin él si el correo es el de SISTEMA) |
| `PUT /api/admin/usuarios/{id}` | `{ nombreMostrar, activo, nombreAnterior, activoAnterior }` | 200 con el usuario; 400 en `nombreMostrar` / `activo` / `anterior`; 404 (inexistente o SISTEMA); 409 «Otro usuario cambió el registro; recargue.» |
| `PUT /api/admin/usuarios/{id}/departamentos` | `{ departamentoIds: [], anteriores: [] }` | 200 `{ departamentos: [{ id, nombreCorto }] }`; 400 en `departamentoIds` / `anteriores`; 404; 409 «Otro usuario cambió los departamentos; recargue.»; 503 si el bloqueo no llega en 15 s |

## 3. Reglas y mensajes
- **Correo (alta):** se recorta y pasa a minúsculas. «El correo es obligatorio.»; «El correo supera los 256 caracteres.»; «El correo no tiene un formato válido.» (`MailAddress` debe leer **exactamente** ese texto: «Nombre <x@y>» se rechaza); «El correo debe ser de un dominio permitido: {lista}.». Dominios: `Usuarios:DominiosPermitidos` (`appsettings.json`: `sedemi.com`; Development agrega `profesiograma.local`, el de DevAuth; sin valor → `sedemi.com`).
- **Nombre:** obligatorio, recortado, ≤ 200 («El nombre es obligatorio.», «El nombre supera los 200 caracteres.»). Es obligatorio porque la columna es NOT NULL y el provisionamiento **no** lo completa para un usuario precargado (§6).
- **Edición (compare-and-set por valores):** `ExecuteUpdate … WHERE Id AND NombreMostrar = nombreAnterior AND Activo = activoAnterior AND EsSistema = 0`, con `ModificadoPorId` y `FechaModificacion` explícitos (no pasa por el `AuditoriaInterceptor`, como la TAREA-62). 0 filas → 409. La comparación del nombre sigue la collation (`Modern_Spanish_CI_AS`: sin distinguir mayúsculas). «Indique si el usuario está activo.»; «Faltan los valores anteriores del usuario; recargue.»; «No puedes darte de baja a ti mismo.» (el Admin sí puede cambiar su propio nombre).
- **Departamentos (compare-and-set por conjunto):** transacción con `sp_getapplock 'profesiograma:usuario:{id}'` (exclusivo, dueño = transacción, 15 s → 503 «Registro ocupado» con detail «Hay otra modificación de este usuario en curso; intente de nuevo.», TAREA-25-2; el bloqueo de asignaciones conserva el suyo); dentro se **releen** las asignaciones y, si el conjunto activo no es `anteriores`, 409. Validación previa: «Envíe la lista de departamentos (puede estar vacía).»; «Falta el conjunto anterior de departamentos; recargue.»; «El departamento {id} está repetido.»; «El departamento {id} no existe o está inactivo.» (un inactivo **ya asignado** se puede conservar). Se admite el conjunto vacío.
- **Efecto:** los retirados pasan a `Activo = 0`; los nuevos se insertan y los que vuelven reactivan **la misma fila** (`UQ_UsuarioDepartamento`), ambos con `Notificado = 0` y `FechaNotificacion` NULL; los que no cambian no se tocan (ni su `Notificado`). **Nunca se borran filas.** Auditoría por el `AuditoriaInterceptor`.
- **Caché:** `IUsuarioProvisionamiento.Invalidar(email, oid)` quita las entradas «usuario:{oid}» y «usuario:{correo}» tras el alta y la edición (nombre, baja, reactivación). Caché en memoria del proceso: con varias instancias de la API haría falta una caché distribuida (Fase 7).
- **Logs:** un Information por escritura con los Id (usuario, departamentos agregados y retirados, Admin); nunca correos ni nombres.

## 4. Decisiones
| # | Decisión |
|---|---|
| P1 | Alta por correo (precarga), como el original: prepara el acceso antes del primer ingreso |
| P2 | Roles solo en Entra ID (Fase 7); la app no los guarda |
| P3 | Sin correo de aviso por ahora: las asignaciones nuevas quedan con `Notificado = 0` (pendiente 71, Fase 7 con Graph) |
| P4 | CargoInfor aparte (25-3) |
| P5 | Menú: centro `/admin` con tarjetas en la 25-2; se conserva `/admin/personal-elegible` |
| P6 | La baja bloquea de inmediato (invalidación de la caché) |
| P7 | Dominios permitidos configurables (`Usuarios:DominiosPermitidos`). **Resuelta (29/10/2026):** solo `@sedemi.com` en producción (no hay contratistas con otro dominio); `profesiograma.local` solo en Development |
| P8 | La novedad GENERAL sigue solo para Admin (J1); no hay bandera por usuario |
| O3 | Departamentos: serialización por usuario (`sp_getapplock`), relectura y comparación de conjuntos dentro de la transacción |
| O6 | Provisionamiento de un precargado documentado (§6), sin cambios |
| O7 | Efecto de los departamentos en P5 y en el buscador documentado (§5), sin cambios |
| O8 | Prueba manual de la baja inmediata con `gestor2` (que queda activo al terminar) |

[PENDIENTE DE CONFIRMAR] El texto de los ajustes O1, O2, O4 y O5 no está en el pedido de la 25-1: si contienen algo distinto de lo descrito aquí, indicarlo.

## 5. Efecto de los departamentos (O7, verificado en el código, sin cambios)
- Ambas lecturas (`EmpleadoConsultas.ConsultaDepartamentosDeUsuario` y `DatosReferenciaProyecto.ConsultaDepartamentosDeUsuario`) toman las asignaciones **activas** de departamentos **activos**, sin caché: un cambio rige en la siguiente solicitud.
- **Buscador** (`GET /api/empleados`, `soloMisDepartamentos` = true por defecto): filtra por **nombre** del departamento o la unidad del empleado. Un usuario **sin departamentos** ve **a todos** (sin filtro), igual que con `soloMisDepartamentos=false`.
- **Proyecto nuevo (P5):** 0 departamentos → el formulario no muestra el selector y el proyecto se crea **sin departamento** (`DepartamentoId` NULL), **sin mensaje ni error**; 1 → ese automáticamente (otro enviado: «El departamento no está entre los asignados al usuario.»); varios → obligatorio («Debe elegir el departamento del proyecto.»).
- Consecuencia: un usuario sin departamentos **sí puede crear proyectos** (con el rol Gestor o Admin). Si se quiere exigir departamento, sería un cambio de regla aparte.

## 6. Primer ingreso de un usuario precargado (O6, `UsuarioProvisionamiento`, sin cambios)
Busca por `oid` (no lo encuentra) y luego por correo (normalizado): lo encuentra sin `EntraObjectId` y **completa el oid** (escritura auditada). `NombreMostrar` **no** se actualiza desde los claims (queda el que puso el Admin). Si está inactivo → 403. Si el correo ya tiene **otro** oid → 403 «El correo está asociado a otra identidad de Entra ID.». El resultado se guarda 10 minutos en la caché.

## 7. Fase 3 (carga de la fila 666) — nota de diseño
Se hará con **SQL por staging**, no con estos servicios. Para que sea idempotente: correos recortados y en minúsculas, con formato válido (pendiente 45); `INSERT INTO Usuario` solo si no existe ese correo (`UQ_Usuario_Email`, `Activo = 1`, `CreadoPorId = 1`); departamento por **nombre** (las 7 semillas); `INSERT INTO UsuarioDepartamento` solo si no existe el par (`UQ_UsuarioDepartamento`), con `Notificado = 1` (ya recibieron el correo en el original) y `Activo = 1`; si existe inactivo, decidir en esa fase si se reactiva. Repetir la carga no debe duplicar ni cambiar filas existentes.

## 8. Desarrollo y pruebas
- DevAuth `gestor2` (Gestor, `gestor2.dev@profesiograma.local`, sin departamentos: el sembrador solo asigna a `admin` y `gestor`). Requiere reiniciar la API. En el frontend está en `USUARIOS_DEV` y en el selector (TAREA-25-2).
- Filas conservadas en PROFESIOGRAMA_DEV (no se pueden borrar; comprobadas por el usuario en SSMS el 29/10/2026): Id 4 prueba.tarea25 y Id 7 prueba.ui (usuarios de prueba, inactivos); Id 5 gestor2 (usuario DevAuth creado en su primer ingreso; activo, sin departamentos).
- Pruebas automáticas: reglas (Application), traducción SQL y plan de asignación (Infrastructure), 403/404/409/201 (Api). El `ExecuteUpdate` y el `sp_getapplock` se verifican con `backend/tests/manual/tarea25/usuarios.cmd`.

## 9. Frontend (TAREA-25-2)
- **Menú:** «Administración» (solo `esAdmin`, de `src/auth/roles.ts`, pendiente 70) abre el centro **`/admin`** con tres tarjetas: «Personal elegible» (`/admin/personal-elegible`), «Usuarios y departamentos» (`/admin/usuarios`) y «Cargos Infor» (`/admin/cargos-infor`, TAREA-25-3-2, §10.6); queda marcado en todas las `/admin/*`. Un gestor que entra por URL a cualquier `/admin/*` ve «No tienes permiso para ver esta página.» sin llamar al backend (`AdminRequerido`; personal elegible conserva su guarda).
- **`/admin/usuarios`:** filtros en la URL (`texto`, `incluirInactivos`, `pagina`; tamaño fijo 25), búsqueda con «Buscar» o Enter; tabla desplazable (correo, nombre, estado Activo/Inactivo y «Aún no ha ingresado», departamentos); cada fila abre el detalle.
- **Alta («Nuevo usuario»):** normaliza (correo en minúsculas) y valida forma y nombre antes de enviar (el dominio lo decide el backend). 201 → detalle con «Usuario creado.»; 400 bajo su campo; 409 duplicado → el title y «Abrir usuario» si viene `usuarioId`.
- **Texto fijo** (diálogo y detalle): «El rol (Gestor o Admin) no se administra aquí: lo asigna TI en Entra ID. Un usuario sin rol no puede entrar aunque esté dado de alta.».
- **`/admin/usuarios/:id`:** dos secciones con su botón. **Datos:** nombre e interruptor «Activo» (deshabilitado con «No puedes darte de baja a ti mismo.» si es el usuario actual); la baja confirma «El usuario dejará de poder entrar de inmediato. Sus datos se conservan.». **Departamentos:** una casilla por disponible (los inactivos asignados con «(inactivo)»); conjunto vacío admitido. Ambas: «No hay cambios para registrar.» o «Se guardará: A → B»; el PUT lleva los valores anteriores del GET que abrió la sección (detalle con `staleTime: Infinity`).
- **Advertencia (pendiente 72):** si el conjunto **guardado** está vacío: «Sin departamentos: este usuario ve a todo el personal en el buscador y sus proyectos nuevos no tendrán departamento.».
- **Invalidación:** alta → lista y detalle; Datos → lista y detalle (+ `/api/usuarios/me` si es el usuario actual); Departamentos → lista y detalle, `['empleados']` y `['proyectos','opciones-formulario']`. Tras guardar, la sección se vuelve a montar con el estado nuevo («Datos actualizados.» / «Departamentos actualizados.»).
- **Errores:** 400 por campo (`anterior`/`anteriores` ofrecen «Recargar»); 409 de datos o departamentos → el title con «Recargar» (confirma si la sección tiene cambios); 404 «El usuario no existe.»; 403 el title sin «Reintentar»; 503 «título: detalle» y red con «Reintentar». Mutaciones sin reintentos, `isPending` y un ref contra el doble clic; `useBlocker` + `beforeunload` mientras alguna sección tenga cambios.
- **Responsive:** una columna; botones a todo el ancho y diálogos a todo el ancho por debajo de 600 px. Los correos solo se muestran en estas pantallas (nunca en la consola, los avisos de error ni el título del documento).
## 10. Cargos Infor (TAREA-25-3)
`CargoInfor` traduce el puesto del empleado al código Infor de la columna CARGO INFOR del reporte (D14, `FASE_5_Cronograma.md` §8). **Sin migración**; la tabla no tiene `RowVer`.

**Datos en DEV (29/10/2026):** `CargoInfor` con 499 filas, de las cuales 491 se importaron ese día desde los 498 puestos de los activos del ERP. No había variantes (tildes o espacios internos) en el ERP a esa fecha.

### 10.1 Contratos (`Politica.Admin`; el gestor recibe 403)
| Endpoint | Entrada | Respuestas |
|---|---|---|
| `GET /api/admin/cargos-infor` | `texto` (cargo o código, contiene), `filtro` (`todos` · `sinCodigo` · `conCodigo`), `incluirInactivos` (false), `pagina`, `tamano` (25; ≤ 100) | 200 `PaginaResultado<{ id, cargo, codigoInfor, activo, personasGuardadas, activosErp }>` por `Cargo`; `personasGuardadas` = filas de `Empleado` con ese puesto (subconsulta, una sola consulta); `activosErp` desde la caché, **null** si no hay lista utilizable (nunca 503); 400 en los parámetros |
| `GET /api/admin/cargos-infor/importacion` | — | 200 `{ puestosApi, nuevos, yaExistentes, existentesInactivos, fusionados, gruposVariantes, variantes (≤ 20 grupos), obtenidaUtc, esAnterior }` con la lista **en caché**; sin lista utilizable → 503 «Servicio de empleados no disponible». No escribe |
| `POST /api/admin/cargos-infor/importacion` | — | 200 `{ puestosApi, insertados, yaExistentes, existentesInactivos }`; 503 (ERP o «Registro ocupado»); 409 (índice único) |
| `PUT /api/admin/cargos-infor/{id}` | `{ codigoInfor, activo, codigoInforAnterior, activoAnterior }` | 200 `{ id, codigoInfor, activo }`; 400 en `codigoInfor` / `activo` / `anterior`; 404 «El cargo no existe.»; 409 «Otro usuario cambió el cargo; recargue.» |

### 10.2 Reglas y normalización
- **Cargo:** `MapeoEmpleado.NormalizarCargo` (la misma de la alta puntual): sin espacios extremos, `ToUpperInvariant`, recorte a 200. **No** colapsa espacios internos ni quita tildes. `Cargo` no se edita (es la clave contra `Empleado.Puesto`); sin alta manual (F1); nunca se borra.
- **Comparador en memoria** (`ReglasCargoInfor.Collation`): imita `Modern_Spanish_CI_AS` con `CompareInfo` es-EC e `IgnoreCase | IgnoreKanaType | IgnoreWidth`: distingue tildes y no distingue mayúsculas ni ancho. Los espacios internos cuentan.
- **Dependencia de la collation (E3):** `Empleado.Puesto` conserva las mayúsculas de la API y `Cargo` va en mayúsculas. El join del reporte (`ConsultaEmpleadosReporte`, sin `UPPER`) coincide **solo** porque la collation no distingue mayúsculas.
- **Conteos de la importación:** `puestosApi` (cargos distintos según la collation); `nuevos` / `insertados`; `yaExistentes` (activos); `existentesInactivos` (no se reactivan, K6); `fusionados` (textos de la API que terminan en un cargo ya contado: mayúsculas, ancho, recorte); `variantes` (grupos de cargos que solo difieren en tildes o espacios internos según `ComparacionEmpleadosErp.Iguales`, entre los de la API y los guardados, con al menos uno de la API). Cada variante es una fila distinta y necesita su propio código (pendiente 73).
- **Código Infor (F2):** recortado y en mayúsculas; vacío = null (quitar el código); ≤ 20; `^[A-Z0-9._-]+$`; sin unicidad. Mensajes: «El código Infor supera los 20 caracteres.», «El código Infor solo admite letras, números, punto, guion y guion bajo.», «Indique si el cargo está activo.», «Faltan los valores anteriores del cargo; recargue.». Un `codigoInforAnterior` vacío equivale a null.
- **Efecto (K10):** el reporte lee `CargoInfor` sin caché; el cambio rige en la siguiente descarga. Un cargo inactivo deja vacío el código en el reporte.

### 10.3 Concurrencia y logs
- **Importación:** lectura **fresca** fuera de la transacción (ERP caído → 503 sin abrirla). Dentro de `ITransaccionAsignaciones` (el applock de la alta puntual, que también inserta cargos) se relee la tabla completa y se insertan solo los faltantes. Es idempotente. Si aun así salta `UQ_CargoInfor_Cargo` → 409 «No se pudo importar: otro registro se guardó al mismo tiempo o hay puestos que la base considera iguales. Vuelva a intentarlo; si se repite, avise a TI.», sin guardar ninguna fila, con un log Warning que da la cantidad de filas (sin nombres de puestos).
- **Edición:** `ExecuteUpdate … WHERE Id AND CodigoInfor = anterior (NULL → IS NULL) AND Activo = activoAnterior`, con `ModificadoPorId` y `FechaModificacion` explícitos. Con 0 filas se relee por Id: no existe → 404; existe → 409.
- **Logs (K14):** importación → los cuatro conteos y el Id del Admin; PUT → Id del cargo, si quedó con código (sí/no), activo anterior → nuevo y el Id del Admin. Nunca nombres de puestos ni códigos.

### 10.4 Decisiones
| # | Decisión |
|---|---|
| F1 | Sin alta manual de cargos |
| F2 | Código recortado y en mayúsculas; vacío = null; ≤ 20; `^[A-Z0-9._-]+$`; sin unicidad |
| F3 | Puestos de **todos** los activos (sin filtro de familia) |
| F4 | Vista previa con la caché (`obtenidaUtc`, `esAnterior`); importación con lectura fresca fuera de la transacción |
| F5 | El modo Simulado **no** se modifica (los SIM están asignados en DEV; un cambio de puesto generaría `CAMBIO_CARGO`). Las variantes se prueban en Application |
| K1 | Lectura fresca al importar; 503 sin abrir la transacción |
| K2 | Todos los activos (= F3) |
| K3 | `NormalizarCargo` compartida; comparador de la collation; conteos de fusionados y variantes |
| K4 | Rutas de vista previa e importación bajo `/api/admin/cargos-infor/importacion` |
| K5 | `ITransaccionAsignaciones` con relectura de la tabla completa; 409 propio por el índice único |
| K6 | No reactiva inactivos; solo los cuenta |
| K7 | Nunca se borra; `Cargo` no es editable; sin alta manual (= F1) |
| K8 | Compare-and-set por valores con auditoría explícita; 0 filas → 404 o 409 |
| K9 | Listado con filtros, `personasGuardadas` (una consulta) y `activosErp` (caché; null sin lista) |
| K10 | Sin caché en el reporte: rige en la siguiente descarga |
| K11 | Importación desde Excel fuera (pendiente 2, opcional) |
| K12 | La semilla `PPOOPP` se corrige desde la pantalla, no por código |
| K13 | 25-3-1 backend + `tarea25-3/cargos.cmd`; 25-3-2 frontend |
| K14 | Logs solo con conteos e Id |
| K15 | Simulado: DEV y SIM usan 5 puestos (SUPERVISOR SSA, PARAMEDICO, TECNICO SSA, INSPECTOR SSA, ASISTENTE SSA), probablemente ya guardados; el script corre en el modo actual (Http) |

### 10.5 Pruebas
- **Application:** comparador (tildes → distintos; mayúsculas → iguales; ancho completo → iguales; espacios internos dobles → distintos), plan de importación (nuevos, existentes, inactivos, fusionados, variantes, recorte a 200), código Infor y servicio (404/409 con 0 filas, idempotencia, no reactivar, 409 por el índice único, ERP caído sin transacción, `activosErp` null, filtros).
- **Infrastructure:** `ToQueryString` del listado (filtros y subconsulta), de todos, por Id y de `ConsultaCargoConValores` con anterior null y no null.
- **Api:** 403 en los 4 endpoints, 404, 409 del PUT y de la importación, 503 con el ERP caído sin tocar la base, 400 y vista previa.
- **Manual:** `backend/tests/manual/tarea25-3/cargos.cmd` (ver su `LEEME.md`).

### 10.6 Frontend (TAREA-25-3-2)
- **Ruta:** tercera tarjeta «Cargos Infor» del centro `/admin` → **`/admin/cargos-infor`** (carga diferida, `AdminRequerido`: un gestor ve «No tienes permiso…» sin llamar al backend). Código en `frontend/src/features/admin/cargos-infor/`.
- **Texto fijo** (pantalla y diálogo de edición): «El código Infor se usa en la columna CARGO INFOR del reporte Excel. Se compara con el puesto del empleado tal como viene del ERP: las tildes y los espacios cuentan.».
- **Filtros en la URL:** `texto` (cargo o código; «Buscar» o Enter), `filtro` (selector «Sin código» · «Con código» · «Todos»; por defecto **«Sin código»**, que se omite en la URL), `incluirInactivos`, `pagina`. A la API se envían siempre `filtro`, `pagina` y `tamano=25` (la API, sin `filtro`, usa `todos`).
- **Columnas:** Cargo, Código Infor («—» sin código), Estado (Activo/Inactivo + insignia), Personas guardadas, Activos ERP («—» si es null). Tabla desplazable; cada fila (clic o Enter) abre la edición.
- **Insignia «Afecta al reporte» (P1):** `personasGuardadas > 0 && (sin código || inactivo)`, porque en ambos casos el reporte deja vacía la columna. **Aproximación:** `personasGuardadas` cuenta filas de `Empleado` de **cualquier** proyecto (también cerrados), no solo las que saldrán en un reporte.
- **Diálogo de edición:** el cargo (solo lectura), personas guardadas y activos ERP; campo «Código Infor» con vista previa normalizada («Se guardará como: P-25.3»; vacío = sin código) y validación local con los mensajes del backend; interruptor «Activo»; «No hay cambios para registrar.» o «Se guardará: (sin código) (Activo) → P-25.3 (Activo)». La baja (activo → inactivo) confirma «El reporte dejará de mostrar el código Infor de este cargo.». Los anteriores del PUT salen de la fila que abrió el diálogo. Cerrar con cambios pide confirmación. Éxito → «Cargo actualizado.».
- **Diálogo de importación** («Importar puestos desde el ERP»): la vista previa se pide **solo al abrir** (P2: `staleTime: 0`, `gcTime: 0`, sin reintentos) y muestra «Lista de las HH:mm» (hora de Ecuador, `formatearHoraUtc`), el aviso si `esAnterior`, los conteos y hasta 20 grupos de variantes con «y N más». Nota fija: «La importación lee el ERP en este momento; los conteos pueden variar.». «Importar» se deshabilita mientras corre (`isPending` + ref); el resultado (insertados, ya existentes, inactivos) reemplaza la vista previa y queda «Cerrar».
- **Invalidación:** PUT e importación → los listados `['admin','cargos-infor','listado',…]`, la vista previa (marcada vencida sin volver a pedirse) y `['proyectos','reporte-resumen']` (prefijo, como `novedades/invalidacion.ts`).
- **Errores:** PUT: 400 bajo su campo (`anterior` ofrece «Recargar»); **409 → «Recargar»** (cierra el diálogo y vuelve a leer la lista; confirma si hay cambios); 404 «El cargo no existe.» con «Recargar»; 403 sin «Reintentar»; 503 y red con «Reintentar». Vista previa e importación: **409 → «Reintentar»** (no es un cambio de datos); 503 (ERP o registro ocupado) y red con «Reintentar». `BarraErrores` es el componente común extraído de la 25-2 (P3).
- **Responsive:** por debajo de 600 px, filtros y botones a todo el ancho, diálogos a todo el ancho con sus botones apilados; la tabla se desplaza en horizontal.
- **Repetición conocida (pendiente 74):** `useFiltrosCargos` copia a `useFiltrosUsuarios`, y el cuerpo del listado se repite en `PaginaUsuarios` y `PaginaCargosInfor`. `MOVIL` y los estilos `superficie`/`acciones` se repiten en `DialogoNuevoUsuario`, `PaginaUsuario`, `BarraErrores`, `DialogoEditarCargo` y `DialogoImportarCargos`.

| # | Decisión |
|---|---|
| P1 | La insignia cuenta también los cargos inactivos con personas guardadas |
| P2 | La vista previa se pide solo al abrir el diálogo (`staleTime: 0`, `retry: false`) |
| P3 | `BarraErrores` se extrae a `features/admin/BarraErrores.tsx` (lo usan solo pantallas de administración y depende de `ErroresDialogo` de `features/proyectos`; `components/` no importa tipos de features). Sin cambios de comportamiento ni de textos; `PaginaUsuario.tsx` lo importa |
| W1 | Tercera tarjeta y ruta diferida con `AdminRequerido` |
| W2 | Listado con filtros en la URL y «Sin código» por defecto; `activosErp` null → «—» |
| W3 | Insignia con la condición de P1 |
| W4 | Edición en diálogo: vista previa normalizada, «Activo», «Se guardará», confirmación de baja |
| W5 | Diálogo de importación: vista previa, «Lista de las HH:mm», aviso `esAnterior`, variantes, nota y resultado |
| W6 | Texto fijo del código Infor |
| W7 | Invalidación de listados, vista previa y resumen del reporte |
| W8 | Errores como la 25-2; el 409 de la importación ofrece «Reintentar» y el del PUT «Recargar» |
| W9 | Responsive como la 25-2 |