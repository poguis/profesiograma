# TAREA-25-2 — Reporte: usuarios y departamentos, frontend

**Fecha:** 29/10/2026 · **Fase:** B (implementación del plan de la TAREA-25) · **Estado:** ✅ Verificada por el usuario (29/10/2026)

Rutas, textos, invalidación y errores: `docs/fases/FASE_5_Usuarios_Departamentos.md` §9.

## 1. Qué se hizo
- **Menú y centro `/admin`:** «Administración» abre `/admin`, con dos tarjetas («Personal elegible» y «Usuarios y departamentos»). Lo ve solo un Admin y queda marcado en todas las `/admin/*`. Las páginas nuevas pasan por `AdminRequerido`: un gestor ve «No tienes permiso…» y la página no se monta, así que no llama al backend.
- **`/admin/usuarios`:**
  - lista con los filtros en la URL y una tabla que se desplaza a lo ancho;
  - insignias Activo/Inactivo y «Aún no ha ingresado»;
  - paginación de 25 (la de la API);
  - diálogo «Nuevo usuario», que normaliza y valida antes de enviar y ofrece «Abrir usuario» en el 409.
- **`/admin/usuarios/:id`:** las secciones «Datos» y «Departamentos» son independientes:
  - confirmación antes de una baja; el interruptor está deshabilitado para el usuario actual;
  - «Se guardará: A → B» y «No hay cambios para registrar.»;
  - la advertencia «Sin departamentos…» (pendiente 72);
  - avisos de éxito;
  - errores por sección, con «Recargar» (confirma si hay cambios) y «Reintentar»;
  - `useBlocker` + `beforeunload`.
- **Pendiente 70:** `esAdmin` está ahora en `src/auth/roles.ts`; el menú, personal elegible y novedades lo importan de allí. Efecto secundario: el menú ya no arrastra módulos de novedades al paquete inicial.
- **DevAuth:** `gestor2` está en `USUARIOS_DEV` y en el selector.
- **Excepción de backend:** el bloqueo por usuario responde 503 con «Hay otra modificación de este usuario en curso; intente de nuevo.». Se hizo con `RegistroOcupadoException.MensajeUsuario`; si no se indica, el detail sigue siendo el de asignaciones, que no cambia.
- **Control de configuración (Grep):** `appsettings.json` tiene `Usuarios:DominiosPermitidos = [ "sedemi.com" ]` y `profesiograma.local` aparece solo en `appsettings.Development.json`. Correcto; no se cambió.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/features/admin/usuarios/usuariosAdmin.ts` (+test) | Nuevo: tipos, filtros de la URL, alta, estado de las secciones, cuerpos y errores |
| `…/usuarios/api.ts`, `PaginaUsuarios.tsx`, `DialogoNuevoUsuario.tsx`, `PaginaUsuario.tsx` | Nuevos |
| `frontend/src/features/admin/PaginaAdmin.tsx`, `AdminRequerido.tsx` | Nuevos: centro y guarda |
| `frontend/src/auth/roles.ts` (+test) | Nuevo: `esAdmin` (pendiente 70) |
| `frontend/src/features/novedades/sincronizacion.ts` (+test), `pages/ListadoNovedades.tsx`, `features/admin/personal-elegible/PaginaPersonalElegible.tsx`, `components/MenuLateral.tsx` | Imports de `esAdmin`; el menú apunta a `/admin` |
| `frontend/src/app/App.tsx`, `auth/devAuth.ts`, `auth/SelectorUsuarioDev.tsx` | Rutas; `gestor2` |
| `backend/src/App.Application/Proyectos/Crear/CrearProyectoContratos.cs`, `App.Api/Configuracion/RegistroOcupadoExceptionHandler.cs`, `App.Infrastructure/Persistencia/Seguridad/UsuariosAdminRepositorio.cs` | Mensaje propio del 503 por usuario |
| `backend/tests/App.Api.Tests/BaseDatosNoDisponibleTests.cs`, `App.Infrastructure.Tests/Consultas/UsuariosAdminSqlTests.cs` | +2 pruebas |
| `docs/fases/FASE_5_Usuarios_Departamentos.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` (H14), `docs/tareas/TAREA-25-1-reporte.md` | Documentación |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `npm run build` (frontend) | OK |
| `npm run lint` | Sin hallazgos (exit 0) |
| `npm test` | **378/378** (362 + 16 de `usuariosAdmin`; la prueba de `esAdmin` se movió a `auth/roles.test.ts`) |
| `dotnet build Profesiograma.slnx -c Release` | Compilación correcta, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1123/1123** (1121 + 2). La prueba existente del 503 de asignaciones sigue igual y pasa |
| `00_ESTADO_ACTUAL.md` | **24 938 B**; lo reemplazado va textual a `HISTORIAL.md` H14 (0 líneas perdidas frente a `HEAD`) |

## 4. Desviaciones y decisiones
- **Única prueba existente ajustada:** el `describe('esAdmin')` salió de `sincronizacion.test.ts` hacia `auth/roles.test.ts`, sin cambios en su contenido. `sincronizacion.ts` ya no exporta `esAdmin` (no hacía falta reexportarlo).
- **Tamaño de página fijo (25):** el pedido solo lista `texto`, `incluirInactivos` y `pagina`. La paginación ofrece solo 25.
- **Búsqueda por texto:** se aplica con «Buscar» o Enter, no en cada tecla.
- **«Sin departamentos»:** la advertencia se basa en el conjunto **guardado**, no en las casillas marcadas. Desaparece al guardar.
- **Invalidación:** guardar departamentos también invalida `['proyectos','opciones-formulario']` (P5), además de `['empleados']`.
- **Pendiente 72:** no existía en el estado vigente. Se registró como creado y resuelto en `HISTORIAL.md` H14.
- **Guarda de personal elegible:** se conserva la suya (no se migró a `AdminRequerido`) para no tocar la 62-2.

## 5. Verificación para el usuario
Requisitos: la API en **Simulado** y **reiniciada**; `npm run dev`; el selector «Usuario (dev)» arriba a la derecha.

| # | Pasos | Esperado |
|---|---|---|
| U1 | Selector → gestor y luego gestor2: mire el menú; escriba `/admin`, `/admin/usuarios` y `/admin/usuarios/4` en la barra | Sin «Administración»; «No tienes permiso para ver esta página.»; en Red, ninguna llamada a `/api/admin/…` |
| U2 | admin → «Administración» | `/admin` con las tarjetas «Personal elegible» y «Usuarios y departamentos»; el menú queda marcado |
| U3 | Tarjeta «Usuarios y departamentos». Marque y desmarque «Incluir inactivos». Escriba `gestor` y luego «Buscar». Recargue con F5 | Sin inactivos: admin, gestor y gestor2 (y otros activos). Con inactivos: además, el usuario de prueba de `prueba.tarea25` («Inactivo»). La URL lleva `?texto=gestor&incluirInactivos=true` y F5 los conserva |
| U4 | «Nuevo usuario»: (a) `prueba.tarea25@profesiograma.local` + un nombre; (b) `x@otro.com`; (c) `no-es-correo`; (d) `prueba.ui@profesiograma.local`, nombre `PRUEBA UI` | (a) «Ya existe un usuario con ese correo.» y «Abrir usuario», que abre su detalle; (b) bajo «Correo»: «El correo debe ser de un dominio permitido: sedemi.com, profesiograma.local.»; (c) «El correo no tiene un formato válido.» sin llamar a la API; (d) detalle con «Usuario creado.» y «Aún no ha ingresado» |
| U5 | Abra gestor2. En «Departamentos» marque «UNIDAD SISTEMA INTEGRADO DE GESTION» y pulse «Guardar departamentos» | Antes: la advertencia «Sin departamentos…». Luego: «Se guardará: (sin departamentos) → SIG», «Departamentos actualizados.» y la advertencia desaparece |
| U6 | Selector → gestor2 → «Proyectos» → «Nuevo proyecto» → buscador de personal, con «Ver otros departamentos» **sin marcar**: busque `SIM006` y luego `SIM011`. Hágalo antes de U5 y repítalo después | Antes de U5 (sin departamentos): aparecen los dos. Después de U5 (SIG): `SIM006` no aparece (es de Infraestructura) y `SIM011` sí. Si marca «Ver otros departamentos», vuelve `SIM006`. En el formulario, el departamento SIG queda automático (P5) |
| U7 | Ventana A (admin): detalle de gestor2 → «Activo» apagado → «Guardar datos» → confirmar. Ventana B (otra pestaña con gestor2): F5. Luego, en A: «Activo» encendido → «Guardar datos» | Confirmación «El usuario dejará de poder entrar de inmediato…» y «Datos actualizados.». En B, error 403 «Usuario no habilitado». Tras reactivar, B vuelve a entrar con F5 |
| U8 | admin → su propio detalle | Interruptor «Activo» deshabilitado con «No puedes darte de baja a ti mismo.»; el nombre sí se puede editar |
| U9 | Dos pestañas con el detalle de gestor2: en A cambie y guarde los departamentos; en B cambie otra casilla y guarde | B: «Otro usuario cambió los departamentos; recargue.» con «Recargar», que confirma porque hay cambios; al recargar se ve el conjunto de A |
| U10 | Cambie el nombre o una casilla y vaya a otra entrada del menú; luego pulse F5 | «¿Salir sin guardar?» / aviso del navegador. Tras guardar, sin aviso |
| U11 | DevTools a 375 px: menú, `/admin`, lista, diálogo y detalle | Menú en fila con «Administración»; tarjetas en una columna; tabla desplazable; botones y diálogos a todo el ancho |
| U12 | `/admin` → «Personal elegible» (A2 de la 62-2) | Igual que antes: ADMINISTRATIVO marcado y los dos textos fijos |

**Al terminar (restaurar):** (1) detalle de gestor2: desmarque SIG → «Guardar departamentos»; debe quedar **activo y sin departamentos**. (2) Detalle de `prueba.ui`: «Activo» apagado → «Guardar datos» → confirmar; queda **inactivo** (las filas no se borran). Comprobación de solo lectura: `SELECT Id, Activo FROM dbo.Usuario WHERE Id IN (<gestor2>, <prueba.ui>);` y `SELECT UsuarioId, DepartamentoId, Activo, Notificado FROM dbo.UsuarioDepartamento WHERE UsuarioId = <gestor2>;` (todas con `Activo = 0`).

## Verificación del usuario
29/10/2026: U1–U12 OK. Estado final comprobado en SSMS: Id 5 gestor2 activo y sin departamentos; Id 7 prueba.ui inactivo; Id 4 prueba.tarea25 inactivo (se conservan; no se borran filas).

## 6. Pendientes
- Verificación visual (§5) y restauración.
- 25-3 (CargoInfor, pendiente 2) con su Fase A corta; pendiente 71 (aviso por Graph, Fase 7).
