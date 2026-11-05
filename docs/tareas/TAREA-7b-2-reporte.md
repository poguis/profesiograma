# TAREA-7b-2 — Reporte: UPN distinto del correo (P3)

**Fecha:** 05/11/2026 · **Fase:** B (implementación del plan de la Fase A corta aprobado) · **Estado:** implementada; pendiente de aplicar la migración, de los registros del portal y de la verificación del usuario (M0–M7, que **reemplazan** a las de `TAREA-7b-reporte.md`)

El diseño (orden de búsqueda, conflictos, Q1–Q4 y riesgos) está en `docs/fases/FASE_7_Entornos_Despliegue.md` §7.3.

## 1. Qué se hizo
- **Modelo:** `Usuario.Upn` (256, NULL, en minúsculas) con el índice único filtrado `UX_Usuario_Upn`. Migración `20261105140125_AgregarUpnUsuario`, **creada en Release y sin aplicar**.
- **Claims:** `ObtenerUpn` (`preferred_username`, si no `upn`) y `ObtenerCorreo` (`email`, si no el UPN). «Token sin correo» solo si faltan los dos. DevAuth emite `email`.
- **Primer ingreso:**
  - `ReglasProvisionamiento.Decidir` (Application, pura): busca por oid → UPN → correo, completa el oid y el UPN, y aplica tres conflictos con 403 (uno es Q1);
  - `UsuarioProvisionamiento` (Infrastructure) solo busca, con tres consultas expuestas, y guarda;
  - logs con solo el Id y el oid abreviado (Q3).
- **Administración:**
  - `Upn` en los DTO del listado y del detalle;
  - el filtro `texto` busca también en `Upn` (Q2);
  - el frontend muestra el UPN de solo lectura, o «Se completa en el primer ingreso».
- **Vite:** `server.port: 5173` con `strictPort: true`. Si el puerto está ocupado, falla con «Port 5173 is already in use».
- **Documentación:** FASE_7 §7.3, FASE_5 §6 y §7 (precarga del UPN en la Fase 3), `00_ESTADO` y HISTORIAL H25.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `backend/src/App.Domain/Seguridad/Usuario.cs`, `Configuraciones/UsuarioConfiguracion.cs` | `Upn` e índice |
| `backend/src/App.Infrastructure/Persistencia/Migraciones/20261105140125_AgregarUpnUsuario.cs` (+ Designer), `ProfesiogramaDbContextModelSnapshot.cs` | Migración nueva y snapshot |
| `backend/src/App.Application/Seguridad/ReglasProvisionamiento.cs` (nuevo), `IUsuarioProvisionamiento.cs`, `UsuariosAdminContratos.cs`, `UsuariosAdminServicio.cs` | Reglas, firma `ResolverAsync(oid, upn, correo, …)`, DTO |
| `backend/src/App.Infrastructure/Seguridad/UsuarioProvisionamiento.cs`, `Persistencia/Seguridad/UsuariosAdminRepositorio.cs` | Búsquedas, guardado y logs; filtro y proyección |
| `backend/src/App.Api/Seguridad/ClaimsPrincipalExtensions.cs`, `UsuarioActualMiddleware.cs`, `DevAuth/DevAuthHandler.cs` | UPN y correo; claim `email` |
| `backend/tests/App.Application.Tests/Seguridad/ReglasProvisionamientoTests.cs` (nuevo), `UsuariosAdminServicioTests.cs` | 9 pruebas nuevas; el doble y el DTO |
| `backend/tests/App.Infrastructure.Tests/Consultas/UsuariosAdminSqlTests.cs` | 2 pruebas nuevas (ToQueryString); 1 llamada ajustada |
| `backend/tests/App.Api.Tests/EntraIdAutenticacionTests.cs`, `UsuariosAdminEndpointsTests.cs` | 5 pruebas nuevas, la de claims ajustada; el doble y el DTO |
| `frontend/src/features/admin/usuarios/usuariosAdmin.ts` (+ `.test.ts`), `PaginaUsuarios.tsx`, `PaginaUsuario.tsx`, `frontend/vite.config.ts` | `upn`, `textoUpn`, la columna y el detalle; el puerto |
| `docs/…` | FASE_7, FASE_5, 00_ESTADO, HISTORIAL, este reporte; nota en `TAREA-7b-reporte.md` |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1196/1196** (1180 + 16 nuevas); **2 ajustadas** (la llamada a `ResolverAsync` en Infrastructure y la de claims en Api) |
| `dotnet ef migrations add AgregarUpnUsuario … --configuration Release` | Creada; **no** se ejecutó `database update`. `has-pending-model-changes`: sin cambios pendientes |
| `npm run build` / `npm run lint` / `npm test` | Correcto (se mantiene el aviso de más de 500 kB de la 7b) / 0 / **412/412** (411 + 1 nueva; el objeto de prueba del detalle suma `upn`) |
| Grep de `X-Dev-User` y `SelectorUsuarioDev` en `frontend/dist` | 0 coincidencias |
| `00_ESTADO_ACTUAL.md` | 24 986 B |

SQL de `dotnet ef migrations script 20261029201119_QuitarParametrosAlmuerzo 20261105140125_AgregarUpnUsuario` (generado sin conectarse a la base):
```sql
BEGIN TRANSACTION;
ALTER TABLE [dbo].[Usuario] ADD [Upn] nvarchar(256) NULL;

CREATE UNIQUE INDEX [UX_Usuario_Upn] ON [dbo].[Usuario] ([Upn]) WHERE [Upn] IS NOT NULL;

INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105140125_AgregarUpnUsuario', N'10.0.12');

COMMIT;
GO
```
El `Down` borra el índice y la columna; el aviso de EF «may result in the loss of data» se refiere a eso.

## 4. Desviaciones, errores y decisiones
- **Migración editada a mano:** EF generó un `UpdateData` de la semilla SISTEMA (`Upn = NULL`). Lo quité porque no hace nada (la columna nace NULL) y porque, en el script sin `GO`, un `UPDATE` de la columna recién agregada en el mismo lote falla en SSMS («Invalid column name»). Después de quitarlo, `has-pending-model-changes` sigue sin cambios.
- **Prueba Api «precargado por correo»:** el doble de la capa Api no puede representarla, así que el caso está en Application (`PrecargadoPorCorreo_UpnDistinto_CompletaOidYUpn`). Los casos de la Api comprueban que el UPN y el correo lleguen separados al alta.
- **Conflicto adicional:** si la fila encontrada por oid no tiene UPN y otra fila ya lo tiene, se responde 403 «El UPN está asociado a otra identidad…» (el índice único no permitiría guardarlo).
- **Error propio corregido:** el primer `migrations script` se ejecutó con `--no-build` sobre binarios viejos («migration not found»); al recompilar se generó.
- **DTO:** `Upn` va después de `Email` en los dos records (el JSON incluye `upn`), lo que obligó a cambiar tres construcciones en las pruebas.

## 5. Verificación para el usuario (reemplaza las M de la 7b)
Requisitos:
- **M0** hecho;
- los registros del portal (FASE_7 §7.2) **y** el claim opcional `email` en el access token de la API (§7.3);
- user-secrets (`Autenticacion:Modo=EntraId`, `AzureAd:TenantId`, `AzureAd:ClientId`) y `frontend/.env.development.local`;
- reiniciar la API y `npm run dev`.

| # | Caso | Esperado |
|---|---|---|
| M0 | Respaldo COPY_ONLY verificado de DEV. Generar el script (§3), revisarlo y ejecutarlo **completo**. Comprobar `SELECT @@TRANCOUNT;` y `SELECT TOP 1 MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;` | `@@TRANCOUNT` = 0; `20261105140125_AgregarUpnUsuario`; `dbo.Usuario` con la columna `Upn` |
| M1 | DevAuth (sin el `.local` ni `Autenticacion:Modo`) | Igual que antes. En `/admin/usuarios`, la columna «Inicio de sesión (UPN)»: el UPN de prueba para admin y gestor tras su siguiente ingreso; «Se completa en el primer ingreso» para los demás |
| M2 | Modo Entra: `http://localhost:5173` | «Verificando la sesión…», el botón y el inicio de sesión. La barra muestra tu nombre; en F12, `Authorization: Bearer` y ningún `X-Dev-User` |
| M3 | Primer ingreso. En SSMS, sin mostrar los valores: `SELECT TOP 3 Id, CASE WHEN EntraObjectId IS NULL THEN 0 ELSE 1 END AS ConOid, CASE WHEN Upn IS NULL THEN 0 ELSE 1 END AS ConUpn, CASE WHEN Email <> Upn THEN 1 ELSE 0 END AS CorreoDistintoDelUpn FROM dbo.Usuario ORDER BY Id DESC;` | Tu fila con `ConOid = 1`, `ConUpn = 1` y `CorreoDistintoDelUpn = 1`. Si da 0, el token no trae `email`: revisar el claim opcional |
| M4 | Cuenta sin rol | «No tienes acceso a PROFESIOGRAMA.»; ninguna fila nueva |
| M5 | «Cerrar sesión» y volver a entrar | El botón; un solo `ssoSilent`, sin bucles |
| M6 | Segunda cuenta Gestor dada de baja | 403 «Usuario no habilitado» en su siguiente solicitud |
| M7 | Volver a DevAuth | Igual que M1 |

## 6. Pendientes
- M0–M7 y el cierre documental de la 7b y la 7b-2.
- Q4: actualizar el correo, para la Fase 7c.
- Precarga del UPN en la Fase 3.
