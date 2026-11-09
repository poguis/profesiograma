# TAREA-22c / 22c-b — verificación manual: 503 rápido con la base caída

`bd-caida.cmd` lo ejecuta **el usuario** (doble clic o desde `cmd`). Claude Code no lo ejecuta. Tarda unos 3–4 minutos.

## Qué hace
- Muestra el banner «MODO REAL - base FICTICIA por variable de entorno; no toca PROFESIOGRAMA_DEV».
- Aborta si el puerto 5181 está ocupado.
- Compila la API en Release (la API que ya está corriendo usa `bin\Debug` y no se toca).
- Por cada caso, abre una ventana minimizada con el título `PROFESIOGRAMA-22C-5181` y levanta ahí una instancia aparte de la API (`--no-launch-profile --urls http://localhost:5181`, Development). Solo para ese proceso define:
  - `ConnectionStrings__Profesiograma` con una base ficticia (`BD_FICTICIA_22C`, `usuario_ficticio`, `clave_ficticia`). Las variables de entorno ganan a los user-secrets, que no se leen ni se modifican.
  - `BaseDatos__ForzarTcp` (`true` o `false`, según el caso).
  - `DatosPrueba__SembrarAlIniciar=false` (con la base caída, el sembrador impediría arrancar: pendiente 60).
- Al terminar cada caso cierra **solo esa ventana** (por su título). Nunca mata procesos `dotnet` por nombre.

## Casos (en este orden: primero los «después»)
| # | Servidor ficticio | ForzarTcp | Primero | Criterio |
|---|---|---|---|---|
| 1 | `127.0.0.1,1` (nadie escucha) | true | EF | estricto |
| 2 | `192.0.2.1` (RFC 5737, no enrutable) | true | EF | estricto (antes: 27 s) |
| 3 | `tcp:192.0.2.1` (prefijo en la cadena) | false | EF | estricto |
| 4 | `192.0.2.1\SSDEV` (instancia con nombre: SQL Browser) | true | EF | instancia |
| 5 | `203.0.113.1` | false | EF | informativo: reproduce el defecto |
| 6 | `203.0.113.2\SSDEV` | false | EF | informativo |
| 7 | `198.51.100.1` | false | `/api/health/db` | informativo: diagnóstico de la Fase A, punto 1 |

Los casos 5–7 usan IP nuevas: el sistema puede recordar un fallo de Named Pipes hacia una IP y acortar el siguiente intento.

En cada caso: `GET /` (anónimo), `GET /api/proyectos` dos veces (gestor; «EF 1.ª» y «EF 2.ª»), `POST /api/proyectos/previsualizar` (admin) y `GET /api/health/db` («diagnóstico»; en el caso 7 va primero).

**Criterios** (el script imprime OK, FALLA o INFORMATIVO y los guarda en `resultado-criterios.txt`):
- estricto (1–3): EF 1.ª y diagnóstico con 503 en ≤ 7000 ms; EF 2.ª en ≤ 500 ms.
- instancia (4): EF 1.ª con 503 en ≤ 7000 ms.
- informativo (5–7): solo tiempos.

## Salidas (en esta carpeta)
- `resultado-criterios.txt`: veredicto de cada caso.
- `resultado-<caso>-resumen.txt`: código HTTP y milisegundos de cada petición, y el control del log.
- `resultado-<caso>-<petición>.txt`: encabezados y cuerpo de cada respuesta.
- `resultado-<caso>-api.txt`: log de la instancia.

## Qué esperar
| Petición | Esperado |
|---|---|
| `GET /` | 200 |
| EF 1.ª | 503, `title` «Base de datos no disponible», `detail` «No se pudo consultar la base de datos. Intente nuevamente en un minuto.»; casos 1–4: ~5–6,5 s |
| EF 2.ª | 503 igual, casi inmediato (bloqueo del pool de SqlClient tras un fallo) |
| `POST …/previsualizar` | 503, `detail` «La base de datos no respondió y no se guardó ningún cambio. Intente nuevamente en un minuto.» |
| Diagnóstico | 503 «No se pudo conectar a SQL Server» (informa el fallo, no lo enmascara). Usa otro pool que EF (EF cambia `Application Name`), por eso no falla al instante |
| Todas las 503 | Encabezado `Cache-Control: no-cache,no-store` |
| Log | «OK, no aparece ningún dato de la cadena ficticia»; un aviso «Base de datos no disponible: error SQL …» como máximo cada 30 s; sin `fail: Microsoft.EntityFrameworkCore.Query[10100]` |
| Caso 5 | EF 1.ª ~27 s (el defecto sin `tcp:`) |
| Caso 7 | Si el diagnóstico tarda ~27 s y EF ~5 s, se confirma que la diferencia de la primera prueba era el orden, no la forma de conectarse |

Referencia de las pruebas automáticas (conexión directa, Connect Timeout = 5): `tcp:192.0.2.1` falló en ~5,0 s (error 258) y `tcp:192.0.2.1\SSDEV` en ~5,0 s (error -1). Por EF, la primera petición suma ~1 s (arranque).

## Prueba opcional posterior
Desde casa, con Sophos conectado y desconectado, contra la API normal (pendiente 61). La decide el usuario. Si con la base real la conexión falla con `ForzarTcp=true` (servidor solo con Named Pipes), usar `BaseDatos:ForzarTcp=false`; si aparecen 503 falsos con la VPN, subir `BaseDatos:TiempoEsperaConexionSegundos`.
