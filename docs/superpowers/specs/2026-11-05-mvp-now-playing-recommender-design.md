# GamePlanner MVP — Now Playing por dispositivo, sesiones, recomendador unificado y solidez

Fecha: 2026-11-05 · Origen: brainstorming sobre `docs/Game_Backlog_Assistant_MVP_Plan(1).md` + revisión de todas las pantallas (código y app corriendo con el usuario de prueba).

## 1. Intención

GamePlanner deja de ser "mi tracker de Steam" y pasa a ser una app donde **cualquier persona con cuenta maneja sus juegos, tenga o no Steam** (Steam, Epic, GOG, Switch, físico…). El dueño la usa todos los días con este flujo:

> Entro → voy a lo que estoy jugando → pongo juegos, saco los que terminé o dejé → registro cuánto jugué y cómo me fue.

Nada está roto, pero ese flujo hoy cuesta varios pasos y dos pantallas que no se hablan (Now Playing y Journal). Cuando no sabe qué jugar, usa el recomendador; quiere que considere **género, tiempo de sesión, duración del juego y dispositivo**, y que explique por qué recomienda cada juego.

Éxito = el flujo diario se hace desde una sola pantalla en pocos clics, el estado de cada juego siempre es coherente en toda la app, y el recomendador da resultados creíbles y explicados, incluso para un usuario sin Steam.

## 2. Objetivos

1. **Now Playing por dispositivo**, unificado con el estado del Journal.
2. **Registro de sesiones** (horas + nota) desde Now Playing.
3. **Recomendador único "What to Play"** que reemplaza Session Planner y Backlog Prioritizer.
4. **Dispositivos y preferencias por usuario** en el perfil.
5. **Biblioteca independiente de Steam**: tienda, edición de juegos, filtros por estado/tienda/dispositivo/duración/favoritos.
6. **Menú simplificado** y sección de administración protegida.
7. **Correcciones de robustez** detectadas en la revisión, con tests de los escenarios críticos.

## 3. No-objetivos

- Integraciones nuevas con Epic, GOG, Xbox, PlayStation (la tienda es solo un dato cargado a mano).
- IA externa para recomendaciones.
- Traducir la interfaz: **la UI queda en inglés** (decisión del dueño, la usa para practicar).
- Estadísticas avanzadas nuevas (Stats solo se corrige y simplifica).
- App móvil, sincronización entre dispositivos, servicios cloud.

## 4. Anti-objetivos (fracaso aunque "funcione")

- Que un juego aparezca como "Playing" en un lado y "Finished" en otro.
- Que el recomendador sugiera algo terminado, abandonado o que ya se está jugando.
- Que las horas registradas a mano se pierdan con una sincronización de Steam, o que se cuenten dos veces.
- Perder datos existentes del dueño (journal, now playing, horas) en la migración.
- Que un usuario pueda leer o modificar juegos de otro, o ejecutar workers.
- Reglas o preferencias personales del dueño fijas en el código.

## 5. Restricciones (del proyecto)

- Backend ASP.NET Core 8 + EF Core para **lecturas (LINQ)**; **stored procedures (Dapper) solo para insert/update/delete**.
- Lógica en **Managers**; Services solo para APIs externas.
- Migraciones = **scripts SQL numerados** en `Database/`, aplicados a mano en SSMS. El 39 ya está reservado por la Phase 6 (script de Portal); usar los siguientes libres.
- Frontend Angular (módulos, no standalone), estilos con las variables de `styles.css` (tema oscuro, acento violeta `#7c3aed`).
- Sin dependencias nuevas salvo justificación.
- Hay trabajo en curso sin commitear (Phase 6: `GameImageManager.cs`, `game-detail.*`). No pisarlo.

### 5.1 Relación con el milestone v1.1 (decidido por el dueño)

- **Primero se termina la Phase 6** (Game Images & Metadata): tiene trabajo sin commitear en `game-detail` y reserva el script SQL 39. Este MVP arranca después y puede usar el arnés `GamePlannerDB_Test` que crea esa fase.
- **Este MVP reemplaza a la Phase 8 (Manual Playtime).** PLAY-01 queda cubierto por el registro de sesiones (6.4), con otro enfoque: las horas van aparte y no se escriben en `PlaytimeHours`. PLAY-02 ("Steam nunca baja un valor manual") deja de aplicar, porque Steam ya no comparte campo con las horas manuales. Hay que actualizar el ROADMAP y REQUIREMENTS en ese sentido.
- **Este MVP absorbe la Phase 9 (UI Copy & Type Scale)**: los estados vacíos y de error de las pantallas que toca deben indicar el próximo paso, y la escala tipográfica se consolida en el mismo trabajo visual.
- **La Phase 7 (Steam Login Hardening) es independiente**; puede ir antes o después. Si va antes, la numeración de scripts SQL de este MVP sigue a la suya.

## 6. Diseño acordado

### 6.1 Dispositivos del usuario

- Cada usuario elige en **Profile → My devices** qué dispositivos tiene, de una lista fija: `PC`, `Steam Deck`, `Switch`, `PlayStation`, `Xbox`, `Mobile`, `Other`. Cada uno con un ícono.
- Se puede elegir más de uno, sin repetir. El orden de la lista define el orden de los lugares en Now Playing.
- Si el usuario no configuró ninguno, Now Playing muestra un único lugar **General**.
- Quitar un dispositivo que tiene un juego en Now Playing pide antes resolver ese juego (el mismo diálogo de "Finish / remove").

### 6.2 Now Playing (maqueta aprobada)

- **Un lugar por dispositivo, un juego activo por dispositivo** (garantizado en la base de datos con un índice único). Un mismo juego no puede estar en dos dispositivos a la vez.
- Cada lugar lleno muestra: portada, nombre, género, tienda, "Playing since <fecha>", horas (Steam y/o registradas), las últimas 2–3 sesiones con su nota, y dos botones: **Log session** y **Finish / remove**.
- Un lugar vacío muestra "Pick a game" y un acceso a **What to Play** con ese dispositivo preseleccionado.
- El buscador para elegir juego excluye los terminados y abandonados por defecto (con opción de verlos, por ejemplo para rejugar).
- Se reemplaza el estante de madera actual por tarjetas oscuras, consistentes con el resto de la app.

**Diálogo "Finish / remove"** (aprobado):
- ✓ **Finished**: estado Finished, `FinishedAt` = hoy, puntaje opcional con el mismo selector de estrellas del Journal (1–5 con medias estrellas, guardado como 1–10 en `UserRating`).
- ✕ **Dropped**: estado Dropped.
- ⏸ **Paused**: estado Paused.
- **Just remove**: libera el lugar sin cambiar el estado del Journal.

**Cambiar el juego de un lugar ocupado** (desde el buscador o desde "Start on <device>" del recomendador) abre primero ese diálogo para el juego que sale.

### 6.3 Estado único del juego (Journal como fuente de verdad)

Los estados existentes del Journal se mantienen: `Backlog`, `Playing`, `Replaying`, `Paused`, `Finished`, `Dropped`. Un juego sin entrada en el Journal se considera `Backlog`.

Reglas, que se aplican en el servidor y en una sola transacción:
- **Poner un juego en Now Playing**: el estado pasa a `Playing`. Si estaba `Finished` pasa a `Replaying`. `StartedAt` = hoy si estaba vacío. Se crea la entrada del Journal si no existía.
- **Sacarlo con un resultado**: se actualiza el estado como se describe en 6.2.
- **Cambiar el estado desde el Journal o desde el detalle del juego** a algo distinto de Playing o Replaying: el juego sale de Now Playing.
- **Eliminar un juego** (borrado lógico): sale de Now Playing.
- **Registrar una sesión** actualiza `LastPlayedAt`. No cambia el estado.

El Journal, el Dashboard, Stats y el recomendador leen siempre ese estado.

### 6.4 Registro de sesiones (aprobado)

- Nueva entidad **PlaySession**: usuario, juego, dispositivo, fecha (por defecto hoy), horas (decimal, mayor que 0 y como máximo 24), nota opcional (texto corto, hasta 1000 caracteres), fecha de creación.
- Se carga desde **Log session** en Now Playing. El dispositivo se completa solo según el lugar.
- El **detalle del juego** muestra el historial completo (fecha, dispositivo, horas, nota) y permite editar o borrar una sesión.
- **Las horas de las sesiones se guardan aparte de `PlaytimeHours` de Steam.** Nunca se suman a ese campo, y la sincronización de Steam nunca las toca.
- **Horas totales** (lo que usan el recomendador, Stats, el Dashboard y "played / not played"): las horas de Steam si son mayores a 0; si no, la suma de las sesiones. La interfaz muestra ambas cuando existen: "Steam: 42 h · Logged: 12 h in 8 sessions".

### 6.5 Biblioteca independiente de Steam

- Nuevo campo **Store** en el juego: `Steam`, `Epic`, `GOG`, `Nintendo eShop`, `PlayStation Store`, `Xbox / Microsoft Store`, `Physical`, `Other`. Los juegos importados de Steam quedan con `Steam`; los manuales existentes, con `Other` (editable). `Source` se mantiene como dato de procedencia (Manual, Steam, IGDB).
- **Dispositivos donde se puede jugar** (opcional por juego). Si no se cargan, se deducen de la tienda con una tabla fija de compatibilidad, no de preferencias. Por ejemplo: Steam → PC y Steam Deck; Epic y GOG → PC; Nintendo eShop → Switch; PlayStation Store → PlayStation; Xbox / Microsoft Store → Xbox y PC; Physical y Other → todos los dispositivos del usuario.
- **Favorito** (corazón) por juego.
- **Editar un juego**: nombre, portada (URL), tienda, dispositivos, duración estimada en horas (se usa cuando no hay datos de HLTB), géneros, favorito.
- **Borrar** pide confirmación siempre, y se puede borrar cualquier juego del usuario, incluidos los importados de Steam (hoy solo los manuales).
- **Tarjetas de la biblioteca**: portada, nombre, chip de estado, tienda, horas totales, géneros con texto (hoy son solo íconos), corazón de favorito.
- **Filtros**: búsqueda por nombre, estado, tienda, dispositivo, género, duración (Short < 10 h, Medium 10–30 h, Long 30 h+, Unknown) y favoritos. Las pestañas actuales "Played / Backlog", que dependen de las horas de Steam, se reemplazan por el filtro de estado.
- **Import from Steam** pasa a ser un botón en My Games. Deja de ser una sección del menú.

### 6.6 Preferencias del usuario (Profile → Preferences)

Todas opcionales y guardadas por usuario. Nada fijo en el código:
- Géneros favoritos y géneros a evitar (de los géneros existentes en la base).
- Duración máxima deseada del juego (horas).
- Single-player, narrativa y combate: cada uno con `Prefer`, `Neutral` o `Avoid`.
- Juegos que gustaron y que no gustaron se derivan de datos que ya existen: favoritos y rating ≥ 4 estrellas (`UserRating` ≥ 8) = gustaron; rating ≤ 2 estrellas (`UserRating` ≤ 4) o `Dropped` = no gustaron. Se usan para sumar o restar puntos a juegos de géneros parecidos.

### 6.7 Recomendador "What to Play" (maqueta aprobada)

Una pantalla y un endpoint. Reemplaza a `SessionPlannerManager`, `BacklogPrioritizerManager` y al endpoint viejo `POST /api/session/recommend` (`SessionManager`, elige al azar).

**Datos de entrada:**
- **Device** (uno de los dispositivos del usuario; preseleccionado el primero o el que venga desde Now Playing).
- **Session time**: `< 1 h`, `1–2 h`, `2 h+`, `Any`.
- **Game length**: `Short < 10 h`, `Medium 10–30 h`, `Long 30 h+`, `Any`.
- **Genre**: `Any` o uno o más géneros.
- **More options** (plegado): estado de ánimo, perspectiva, modo (solo, coop, multiplayer) y estrategia: `Default`, `Resume` (prioriza juegos empezados y abandonados poco) o `Discover` (prioriza géneros que casi no jugaste).

**Exclusiones duras:** juegos borrados; estado `Finished` o `Dropped`; los que están en Now Playing; los que no se pueden jugar en el dispositivo elegido; los de géneros a evitar (salvo que el usuario elija ese género a propósito).

**Todo lo demás suma o resta puntos**, nunca excluye: género pedido y géneros favoritos, duración del juego frente a lo pedido y frente a la duración máxima, encaje con el tiempo de sesión, preferencias de single-player, narrativa y combate, gustos derivados, **rating de la crítica (IGDB `AggregatedRating`, como hoy en el Backlog Prioritizer)**, estado de ánimo, perspectiva, modo y estrategia. Los modos Prestige y Hidden Gems del Prioritizer no se mantienen como estrategias separadas: el rating ya pesa en el orden por defecto. Así siempre hay resultados. Si lo pedido no se cumple, la explicación lo dice ("shorter than requested").
- **Duración del juego:** HLTB (main story) si existe; si no, la duración que cargó el usuario; si no, desconocida. Desconocida = neutral, y la explicación dice "no length data".
- **Encaje con el tiempo de sesión:** se deduce de géneros y modos con una tabla fija de conocimiento sobre juegos (por ejemplo, roguelike, arcade y puzzle → sesiones cortas; RPG y estrategia → sesiones largas). Es un dato sobre los juegos, no una preferencia personal. Se puede reutilizar lo que ya existe en `SessionPlannerManager`.
- **Determinista:** sin azar. Desempate por nombre. Lo mismo entra, lo mismo sale (necesario para los tests).

**Salida:** una recomendación principal y hasta 4 alternativas. Cada una trae:
- la lista de motivos (✓ a favor, • neutral o en contra),
- un nivel de coincidencia (alta, media o baja) con el puntaje numérico,
- tienda, duración y horas jugadas.

**Acciones:** **Start on <device>** pone el juego en Now Playing (y abre el diálogo de 6.2 si el lugar está ocupado) y **View details**.

El "Tonight's Pick" del Dashboard usa el mismo recomendador con los valores por defecto.

### 6.8 Navegación

Menú principal (de 10 a 7 secciones): **Home** · **Now Playing** · **What to Play** · **My Games** · **Journal** · **Calendar** · **Stats**.
- **Calendar** suma una pestaña o filtro **Tracked**, que reemplaza a la pantalla Tracked Games.
- **Administration** (solo administradores): **Workers**.
- Las rutas viejas (`/planner`, `/backlog-prioritizer`, `/tracked-games`, `/steam`) redirigen a sus reemplazos.
- Los botones del Dashboard ("Session Planner", "Backlog", "Add to Session") se apuntan a las pantallas nuevas.

### 6.9 Administración y seguridad

- Nuevo campo `IsAdmin` en el usuario, falso por defecto. Se activa a mano en la base para la cuenta del dueño.
- El rol viaja en el JWT. `WorkerController` y `SyncController` exigen administrador; el frontend oculta Administration a los demás.
- Todo endpoint que reciba un `gameId` (Now Playing, sesiones, journal, editar o borrar juego) valida que el juego sea del usuario. Hoy `PUT /api/now-playing/{slot}` no lo valida.

### 6.10 Correcciones detectadas en la revisión

- **Stats cuenta juegos borrados.** `StatsDashboardManager` no filtra `IsActive`: el usuario de prueba muestra 8 juegos cuando tiene 3, y en "Most played" aparece un juego eliminado.
- **Borrar desde la tarjeta no pide confirmación** (`game-card`).
- **No existe edición de juegos.**
- **Los recomendadores ignoran el estado del Journal** y pueden recomendar juegos terminados o abandonados.
- **"Played / not played" depende solo de Steam**, así que un usuario sin Steam ve todo como "Not played".
- **El Dashboard muestra "(in 0d)"** para lanzamientos de hoy; debe decir "Today".

### 6.11 Migración de datos existentes

- Los lugares actuales de Now Playing (`Slot` 0–2) pasan a dispositivos. Para cada usuario con filas existentes: si no tiene dispositivos configurados, se le crean `PC` y `Steam Deck` (aplica al dueño). El slot 0 va a PC y el slot 1 a Steam Deck. El juego del slot 2, si existe, no se pierde: queda con estado `Playing` en el Journal y sale de Now Playing, para reasignarlo desde la interfaz.
- Para todo juego que esté en Now Playing al migrar, se crea o actualiza su entrada del Journal a `Playing`, para que las dos fuentes queden coherentes desde el primer día.
- `Store` se completa a partir de `Source`/`SteamAppId`.
- El script debe ser idempotente o fallar sin dejar datos a medias (transacción), y antes de aplicarlo se hace un backup.

### 6.12 Tests

Priorizar los escenarios críticos, no la cantidad:
- **Recomendador:** exclusiones (Finished, Dropped, Now Playing, dispositivo, géneros a evitar), resultado determinista, explicaciones presentes, que siempre devuelva algo si hay candidatos, usuario sin Steam y con sesiones.
- **Now Playing y estado:** un juego por dispositivo, un juego en un solo dispositivo, las transiciones de estado de 6.3 (en las dos direcciones), validación de pertenencia del juego.
- **Sesiones:** validaciones (horas fuera de rango, juego ajeno) y cálculo de horas totales.
- **Administración:** un usuario que no es admin recibe 403 en workers y sync.
- **Stats:** no cuenta juegos borrados.
- Usar el arnés de base de datos de prueba que trae la Phase 6 (`GamePlannerDB_Test`) si ya está; si no, el patrón existente en `GamePlanner.Tests`.
- Un e2e de Playwright del flujo diario: poner un juego, registrar una sesión, terminarlo, verificar el Journal.

## 7. Decisiones tomadas por el diseñador (revisar)

- Un juego activo por dispositivo (lo dice el plan de MVP, el dueño no lo objetó).
- Lugar "General" cuando el usuario no tiene dispositivos.
- Las horas de las sesiones van aparte de las de Steam; horas totales = Steam si es mayor a 0, si no, la suma de las sesiones.
- Finished → Replaying al volver a ponerlo en Now Playing.
- El tiempo de sesión y la duración del juego suman o restan puntos, no excluyen; los géneros a evitar sí excluyen.
- Recomendador determinista.
- Backlog Prioritizer absorbido como estrategias "Resume" y "Discover".
- Tabla fija tienda → dispositivos compatibles.
- Gustos derivados de favoritos, rating y Dropped (sin lista manual aparte). Umbrales en estrellas: ≥ 4 gustó, ≤ 2 no gustó.
- El rating de la crítica (IGDB) sigue sumando puntos en What to Play; se eliminan los modos Prestige y Hidden Gems.
- `IsAdmin` manual en la base, sin pantalla de gestión de roles.
- Edición y borrado de sesiones desde el detalle del juego.

## 8. Lo que queda a criterio del implementador

- Nombres exactos de tablas, columnas, stored procedures, DTOs y endpoints, siguiendo las convenciones del repo.
- Pesos exactos del puntaje y umbrales de "alta, media o baja", mientras el orden sea explicable y los tests lo fijen.
- Detalle visual dentro del tema existente (las maquetas aprobadas están en `.superpowers/brainstorm/`: `now-playing.html`, `recommender.html`).
- Orden de los bloques de implementación, siempre que cada bloque deje la app funcionando y con sus tests pasando.
