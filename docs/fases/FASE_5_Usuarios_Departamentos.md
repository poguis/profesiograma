# FASE 5 — Usuarios y departamentos (TAREA-25)

| Subtarea | Alcance | Estado |
|---|---|---|
| 25-1 | Backend: endpoints de administración, reglas, invalidación de la caché, DevAuth `gestor2`, script `tarea25/usuarios.cmd` | ✅ Verificada por el usuario (29/10/2026) |
| 25-2 | Frontend: centro `/admin` con tarjetas, lista y edición de usuarios (§9); mensaje propio del 503 por usuario | ✅ Verificada por el usuario (29/10/2026) |
| 25-3 | CargoInfor: «Importar puestos desde la API» y edición de `CodigoInfor` (pendiente 2) | Pendiente; su propia Fase A |

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
- **Menú:** «Administración» (solo `esAdmin`, de `src/auth/roles.ts`, pendiente 70) abre el centro **`/admin`** con dos tarjetas: «Personal elegible» (`/admin/personal-elegible`) y «Usuarios y departamentos» (`/admin/usuarios`); queda marcado en todas las `/admin/*`. Un gestor que entra por URL a cualquier `/admin/*` ve «No tienes permiso para ver esta página.» sin llamar al backend (`AdminRequerido`; personal elegible conserva su guarda).
- **`/admin/usuarios`:** filtros en la URL (`texto`, `incluirInactivos`, `pagina`; tamaño fijo 25), búsqueda con «Buscar» o Enter; tabla desplazable (correo, nombre, estado Activo/Inactivo y «Aún no ha ingresado», departamentos); cada fila abre el detalle.
- **Alta («Nuevo usuario»):** normaliza (correo en minúsculas) y valida forma y nombre antes de enviar (el dominio lo decide el backend). 201 → detalle con «Usuario creado.»; 400 bajo su campo; 409 duplicado → el title y «Abrir usuario» si viene `usuarioId`.
- **Texto fijo** (diálogo y detalle): «El rol (Gestor o Admin) no se administra aquí: lo asigna TI en Entra ID. Un usuario sin rol no puede entrar aunque esté dado de alta.».
- **`/admin/usuarios/:id`:** dos secciones con su botón. **Datos:** nombre e interruptor «Activo» (deshabilitado con «No puedes darte de baja a ti mismo.» si es el usuario actual); la baja confirma «El usuario dejará de poder entrar de inmediato. Sus datos se conservan.». **Departamentos:** una casilla por disponible (los inactivos asignados con «(inactivo)»); conjunto vacío admitido. Ambas: «No hay cambios para registrar.» o «Se guardará: A → B»; el PUT lleva los valores anteriores del GET que abrió la sección (detalle con `staleTime: Infinity`).
- **Advertencia (pendiente 72):** si el conjunto **guardado** está vacío: «Sin departamentos: este usuario ve a todo el personal en el buscador y sus proyectos nuevos no tendrán departamento.».
- **Invalidación:** alta → lista y detalle; Datos → lista y detalle (+ `/api/usuarios/me` si es el usuario actual); Departamentos → lista y detalle, `['empleados']` y `['proyectos','opciones-formulario']`. Tras guardar, la sección se vuelve a montar con el estado nuevo («Datos actualizados.» / «Departamentos actualizados.»).
- **Errores:** 400 por campo (`anterior`/`anteriores` ofrecen «Recargar»); 409 de datos o departamentos → el title con «Recargar» (confirma si la sección tiene cambios); 404 «El usuario no existe.»; 403 el title sin «Reintentar»; 503 «título: detalle» y red con «Reintentar». Mutaciones sin reintentos, `isPending` y un ref contra el doble clic; `useBlocker` + `beforeunload` mientras alguna sección tenga cambios.
- **Responsive:** una columna; botones a todo el ancho y diálogos a todo el ancho por debajo de 600 px. Los correos solo se muestran en estas pantallas (nunca en la consola, los avisos de error ni el título del documento).