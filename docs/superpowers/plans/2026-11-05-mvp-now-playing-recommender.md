# GamePlanner MVP — Now Playing por dispositivo, sesiones y What to Play — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Now Playing con un lugar por dispositivo unificado con el estado del Journal, registro de sesiones, recomendador único "What to Play" con explicaciones, biblioteca independiente de Steam, preferencias y dispositivos por usuario, administración protegida y las correcciones de la revisión.

**Architecture:** El Journal (`GameJournalEntries.Status`) es la única fuente del estado del juego. Toda transición (poner o sacar de Now Playing, cambiar estado, borrar juego) ocurre dentro de un stored procedure transaccional. El recomendador es una clase pura (`WhatToPlayScorer`) que recibe los candidatos y el perfil ya cargados por un Manager, así se testea sin base de datos. El frontend Angular reemplaza dos pantallas (Session Planner, Backlog Prioritizer) por una (What to Play) y reconstruye Now Playing según la maqueta aprobada.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (lecturas LINQ), Dapper + stored procedures (escrituras), SQL Server (scripts numerados aplicados a mano), xUnit, Angular 13+ (módulos), Karma, Playwright (`e2e/`).

**Spec:** `docs/superpowers/specs/2026-11-05-mvp-now-playing-recommender-design.md`. Leelo antes de cualquier tarea; este plan argumenta desde ahí.

**Maquetas aprobadas:** `.superpowers/brainstorm/5496-1793894781/content/now-playing.html` y `recommender.html`. Las etiquetas de las maquetas están en español; **la UI se implementa en inglés** (spec §3).

## Global Constraints

- **Precondición:** la Phase 6 (`.planning/phases/06-game-images-metadata/`) está terminada y commiteada antes de la Task 1. Esta fase crea `scripts/build-test-db.ps1`, `GamePlannerDB_Test`, `TestDatabase.ConnectionString` y la colección xUnit `TestDatabase.CollectionName`. Las tasks con tests de base de datos los usan.
- Lecturas con EF LINQ. Insert, update y delete **solo** con stored procedures llamados por Dapper. Lógica en Managers; Services solo para APIs externas (CLAUDE.md).
- Migraciones: scripts SQL numerados en `Database/`. Este plan los llama `40_…` a `42_…`. Si la Phase 7 ya agregó scripts, usá los siguientes números libres manteniendo el orden. **Nunca los apliques sobre `GamePlannerDB`.** Se aplican solo sobre `GamePlannerDB_Test` (con `build-test-db.ps1`); el dueño los aplica en SSMS en el checkpoint de la Task 1.
- La UI queda en inglés. Las etiquetas visibles al usuario de este plan son textos exactos.
- Estados del Journal: `Backlog`, `Playing`, `Replaying`, `Paused`, `Finished`, `Dropped`. Un juego sin entrada en el Journal es `Backlog`.
- Dispositivos (códigos guardados → etiqueta visible): `PC`→"PC", `SteamDeck`→"Steam Deck", `Switch`→"Switch", `PlayStation`→"PlayStation", `Xbox`→"Xbox", `Mobile`→"Mobile", `Other`→"Other". Hay un código más, `General`→"General", que no se puede elegir: es el lugar único cuando el usuario no tiene dispositivos.
- Tiendas (códigos guardados → etiqueta visible): `Steam`→"Steam", `Epic`→"Epic", `GOG`→"GOG", `NintendoEShop`→"Nintendo eShop", `PlayStationStore`→"PlayStation Store", `XboxStore`→"Xbox / Microsoft Store", `Physical`→"Physical", `Other`→"Other".
- Dispositivos compatibles por defecto según la tienda: Steam → PC y SteamDeck; Epic → PC; GOG → PC; NintendoEShop → Switch; PlayStationStore → PlayStation; XboxStore → Xbox y PC; Physical y Other → cualquiera (lista vacía = cualquier dispositivo).
- `UserRating` se guarda de 1 a 10 (medias estrellas). Gustó = ≥ 8 o favorito. No gustó = ≤ 4 o `Dropped`.
- Horas de una sesión: decimal mayor que 0 y como máximo 24. Nota: hasta 1000 caracteres.
- Horas totales = `PlaytimeHours` (Steam) si es mayor que 0; si no, la suma de las sesiones. Las sesiones nunca escriben en `PlaytimeHours`.
- Sin dependencias nuevas (NuGet ni npm).
- Commits chicos por task, con el trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Doble clic o dos pestañas en "Start on <device>"**: dos inicios simultáneos en el mismo dispositivo deben terminar con uno aplicado y el otro con 409, nunca con 500 ni con dos filas. Test en la Task 2.
2. **Juego borrado (soft delete) que estaba en Now Playing o tenía sesiones**: desaparece del lugar, no aparece en What to Play, y sus sesiones no rompen el detalle ni los totales. Test en la Task 2 (`DeleteGame_ClearsNowPlaying`) y en la Task 4.
3. **Usuario sin dispositivos configurados** (todo usuario nuevo): Now Playing muestra "General" y What to Play trata todos los juegos como jugables. Test en la Task 3 y en la Task 9.
4. **Horas con coma decimal** ("1,5", escrito con teclado en español): el formulario la acepta como 1.5. Si no es un número válido, muestra "Enter hours between 0.1 and 24." y no envía nada. Test en la Task 12.
5. **Juego sin géneros, sin datos de HLTB, sin rating ni horas** (lo típico al cargar uno a mano): el scorer no falla, lo puntúa neutral y la explicación incluye "No length data". Test en la Task 8.

---

## Block A — Datos

### Task 1: Esquema, migración de datos y entidades

**Files:**
- Create: `Database/40_MvpSchema.sql`, `Database/41_MvpDataMigration.sql`
- Create: `GamePlanner.API/Entities/UserDevice.cs`, `PlaySession.cs`, `UserPreference.cs`, `UserPreferenceGenre.cs`, `GamePlayableDevice.cs`
- Create: `GamePlanner.API/Domain/MvpConstants.cs` (clases `DeviceTypes`, `Stores`, `JournalStatuses`)
- Modify: `GamePlanner.API/Entities/Game.cs`, `User.cs`, `NowPlayingGame.cs`, `GamePlanner.API/Context/GamePlannerDbContext.cs`
- Test: `GamePlanner.Tests/MvpSchemaTests.cs`, `GamePlanner.Tests/MvpConstantsTests.cs`

**Interfaces:**
- Produces (tablas y columnas):
  - `UserDevices(UserId INT FK, Device NVARCHAR(20), SortOrder INT, PK(UserId, Device))`
  - `Games.Store NVARCHAR(30) NOT NULL DEFAULT 'Other'` y `Games.IsFavorite BIT NOT NULL DEFAULT 0`
  - `GamePlayableDevices(GameId INT FK, Device NVARCHAR(20), PK(GameId, Device))`
  - `PlaySessions(Id INT IDENTITY PK, UserId INT FK, GameId INT FK, Device NVARCHAR(20), PlayedOn DATE, Hours DECIMAL(4,2) CHECK (Hours > 0 AND Hours <= 24), Note NVARCHAR(1000) NULL, CreatedAt DATETIME2 DEFAULT GETUTCDATE(), ModifiedAt DATETIME2 NULL)`, con un índice en `(UserId, GameId, PlayedOn DESC)`
  - `UserPreferences(UserId INT PK FK, MaxGameHours INT NULL, SinglePlayer / Narrative / Combat NVARCHAR(10) NOT NULL DEFAULT 'Neutral' CHECK IN ('Prefer','Neutral','Avoid'))`
  - `UserPreferenceGenres(UserId, GenreId, Kind NVARCHAR(10) CHECK IN ('Favorite','Avoid'), PK(UserId, GenreId))`: un género no puede estar en las dos listas
  - `Users.IsAdmin BIT NOT NULL DEFAULT 0`
  - `NowPlayingGames.Device NVARCHAR(20) NOT NULL` en lugar de `Slot`. Restricciones únicas: `UQ_NowPlaying_UserDevice (UserId, Device)` y `UQ_NowPlaying_UserGame (UserId, GameId)`. Se borran `Slot` y `UQ_NowPlaying_UserSlot`.
- Produces (C#):
  - `DeviceTypes`: constantes de los códigos, `IReadOnlyList<string> Selectable` (sin General) y `bool IsSelectable(string)`.
  - `Stores`: constantes, `IReadOnlyList<string> All` y `IReadOnlyList<string> DefaultDevices(string store)` (lista vacía = cualquier dispositivo).
  - `JournalStatuses`: constantes y `bool IsActive(string status)` (true para Playing y Replaying).
  - Las entidades nuevas como `DbSet`: `UserDevices`, `PlaySessions`, `UserPreferences`, `UserPreferenceGenres`, `GamePlayableDevices`. `Game.Store`, `Game.IsFavorite` y `Game.PlayableDevices` (navegación). `User.IsAdmin`. `NowPlayingGame.Device: string`.

- [ ] **Step 1: Escribir los tests que fallan.** `MvpConstantsTests`:
  - `DefaultDevices("Steam")` es `["PC","SteamDeck"]`.
  - `DefaultDevices("Physical")` está vacía.
  - `DefaultDevices("NintendoEShop")` es `["Switch"]`.
  - `IsSelectable("General")` es false.

  `MvpSchemaTests`, en la colección `TestDatabase.CollectionName`:
  - `Schema_HasMvpTables`: con EF, `UserDevices`, `PlaySessions`, `UserPreferences`, `UserPreferenceGenres` y `GamePlayableDevices` se pueden consultar (`AnyAsync()` no lanza excepción).
  - `NowPlaying_RejectsSameGameOnTwoDevices`: insertar dos filas con el mismo `(UserId, GameId)` lanza `SqlException` con número 2627.
  - `PlaySessions_RejectsZeroHours`: insertar `Hours = 0` lanza `SqlException` con número 547.
- [ ] **Step 2:** `dotnet test GamePlanner.Tests --filter "FullyQualifiedName~MvpSchemaTests|FullyQualifiedName~MvpConstantsTests"`. Esperado: FAIL (las clases y tablas no existen).
- [ ] **Step 3: Escribir `40_MvpSchema.sql`** (solo DDL, idempotente con `IF COL_LENGTH(...) IS NULL` y `IF OBJECT_ID(...) IS NULL`). Escribir también las constantes, las entidades y el mapeo en el DbContext (claves compuestas con `HasKey`; `NowPlayingGame.Device`).
- [ ] **Step 4: Escribir `41_MvpDataMigration.sql`**: todo dentro de `BEGIN TRAN` con `SET XACT_ABORT ON`. Debe poder correrse dos veces sin cambiar nada la segunda. Las reglas salen de la spec §6.11:
  1. `Games.Store = 'Steam'` donde `SteamAppId IS NOT NULL OR Source = 'Steam'`. El resto queda `'Other'`.
  2. Para cada usuario con filas en `NowPlayingGames` y sin filas en `UserDevices`: insertar `('PC',0)` y `('SteamDeck',1)`.
  3. Slot 0 → Device `'PC'` y slot 1 → `'SteamDeck'`. La fila del slot 2 se borra, pero antes su juego queda con estado `Playing` en el Journal.
  4. Todo juego que quede en Now Playing tiene su entrada del Journal en `Playing` (se crea si no existe; si estaba `Finished` pasa a `Replaying`; `StartedAt` = `AddedAt` si estaba vacío).

  La migración de `Slot` a `Device` necesita dos pasos: el script 40 agrega `Device` como nullable; el 41 lo completa, lo hace `NOT NULL`, crea las restricciones únicas y borra `Slot`. Al final imprime `PRINT` con la cantidad de filas de cada paso.
- [ ] **Step 5:** `powershell -File scripts/build-test-db.ps1`. Esperado: `TEST_DB_READY=GamePlannerDB_Test`. Después correr los tests del Step 2. Esperado: PASS.
- [ ] **Step 6: Prueba en seco sobre los datos reales (solo lectura).** Correr el cuerpo de `41_MvpDataMigration.sql` contra `GamePlannerDB` envuelto en `BEGIN TRAN … ROLLBACK`, con los `PRINT` y un `SELECT` final de `NowPlayingGames` + Journal del usuario 2. Guardar la salida en el resumen de la task. No se commitea ningún dato.
- [ ] **Step 7: Commit.** `git add Database/40_MvpSchema.sql Database/41_MvpDataMigration.sql GamePlanner.API GamePlanner.Tests && git commit -m "feat(db): MVP schema, data migration and entities"`
- [ ] **Step 8: CHECKPOINT DEL DUEÑO.** Mostrarle la salida de la prueba en seco. Pedirle que haga un backup de `GamePlannerDB` y aplique 40 y 41 en SSMS. Esperar su confirmación antes de la Task 2: la API corriendo contra su base falla hasta que estén aplicados.

### Task 2: Transiciones de estado y Now Playing por dispositivo (backend)

**Files:**
- Create: `Database/42_MvpProcedures.sql` (todos los SPs del MVP; las tasks 3–6 le agregan los suyos)
- Modify: `GamePlanner.API/Managers/NowPlayingManager.cs`, `Controllers/NowPlayingController.cs`, `Domain/` (DTOs de Now Playing)
- Create: `GamePlanner.Tests/Support/MvpTestData.cs`
- Test: `GamePlanner.Tests/NowPlayingTransitionTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces (SQL):
  - `sp_StartNowPlaying @UserId, @Device, @GameId, @OutgoingOutcome NVARCHAR(10) NULL, @OutgoingRating TINYINT NULL`. Devuelve `INT`: 0 = ok, 1 = el juego no es del usuario o está borrado, 2 = lugar ocupado sin `@OutgoingOutcome`, 3 = el juego ya está en otro dispositivo, 4 = `@OutgoingOutcome` inválido.
  - `sp_RemoveNowPlaying @UserId, @Device, @Outcome NVARCHAR(10), @Rating TINYINT NULL`. Devuelve 0 = ok, 1 = lugar vacío, 4 = outcome inválido.
  - Outcomes válidos: `Finished`, `Dropped`, `Paused`, `None`. Al aplicar uno: `Finished` → `FinishedAt` = hoy y `UserRating` = `@Rating` si no es NULL; `None` deja el Journal como está.
  - Al iniciar, el juego entrante pasa a `Playing` (o a `Replaying` si estaba `Finished`, y en ese caso `ReplayCount += 1`). `StartedAt` = hoy si estaba vacío. La entrada del Journal se crea si no existe.
  - Se modifica `sp_UpsertGameJournalEntry`: si el estado nuevo no es Playing ni Replaying, borra la fila de `NowPlayingGames` del mismo `(UserId, GameId)`. Además valida que el juego sea del usuario y esté activo: devuelve 0 = ok y 1 = juego ajeno o borrado. `GameJournalManager.UpsertAsync` pasa a devolver `bool` y `GameJournalController` responde 404 cuando es false (hoy no valida la pertenencia).
  - Se modifica `sp_DeleteGame`: borra la fila de Now Playing del juego.
  - Ambos SPs nuevos usan `SET XACT_ABORT ON; BEGIN TRAN` y `WITH (UPDLOCK, HOLDLOCK)` al leer el lugar. Una violación de la restricción única por carrera devuelve 2 o 3, no un error.
- Produces (C#):
  - `NowPlayingSlotDto { string Device; GameDto? Game; DateTime? PlayingSince; List<PlaySessionDto> RecentSessions }`. `PlaySessionDto` lo define la Task 4; hasta entonces la lista va vacía.
  - `StartNowPlayingRequest { int GameId; string? OutgoingOutcome; byte? OutgoingRating }`
  - `RemoveNowPlayingRequest { string Outcome; byte? Rating }`
  - `NowPlayingManager.GetSlotsAsync(int userId) → List<NowPlayingSlotDto>`: un lugar por cada dispositivo del usuario, en orden de `SortOrder`, o solo `General` si no tiene ninguno.
  - `NowPlayingManager.StartAsync(int userId, string device, StartNowPlayingRequest r) → NowPlayingResult`
  - `NowPlayingManager.RemoveAsync(int userId, string device, RemoveNowPlayingRequest r) → NowPlayingResult`
  - `enum NowPlayingResult { Ok, GameNotFound, SlotOccupied, GameOnOtherDevice, InvalidOutcome, InvalidDevice, SlotEmpty }`. `InvalidDevice` = el dispositivo no está en la lista del usuario (o no es `General` cuando no tiene ninguno). Lo valida el manager antes de llamar al SP.
  - HTTP:
    - `GET /api/now-playing`
    - `PUT /api/now-playing/{device}` con body `StartNowPlayingRequest`
    - `POST /api/now-playing/{device}/remove` con body `RemoveNowPlayingRequest`
    - Mapeo de resultados: Ok→204, GameNotFound→404, SlotOccupied→409 `{ code: "slot_occupied" }`, GameOnOtherDevice→409 `{ code: "game_on_other_device" }`, InvalidOutcome o InvalidDevice→400, SlotEmpty→404.
  - `MvpTestData` (helper de tests): `CreateUserAsync(string prefix) → int`, `CreateGameAsync(int userId, string name, string store = "Other", string[]? genres = null, double steamHours = 0, int estimatedMinutes = 0) → int`, `SetDevicesAsync(int userId, params string[] devices)` y `CleanupAsync(string prefix)`. Las filas llevan el prefijo `mvp-probe-` y se insertan con SQL directo sobre `TestDatabase.ConnectionString`.

- [ ] **Step 1: Escribir los tests que fallan** (colección de base de prueba, cada uno con try/finally de limpieza):
  - `Start_SetsJournalPlaying_AndStartedAt`
  - `Start_FinishedGame_BecomesReplaying_AndIncrementsReplayCount`
  - `Start_OccupiedSlotWithoutOutcome_ReturnsSlotOccupied_AndChangesNothing`
  - `Start_OccupiedSlotWithFinished_FinishesOutgoing_SetsFinishedAtAndRating` (rating 8)
  - `Start_GameOnOtherDevice_ReturnsGameOnOtherDevice`
  - `Start_OtherUsersGame_ReturnsGameNotFound`
  - `Start_DeviceNotInUserList_ReturnsInvalidDevice`
  - `Start_ConcurrentOnSameDevice_OneOkOneConflict`: dos `StartAsync` con `Task.WhenAll` y juegos distintos. Exactamente uno da `Ok`, el otro `SlotOccupied`, y queda una sola fila.
  - `Remove_Dropped_SetsStatus_AndFreesSlot`
  - `Remove_None_KeepsJournalStatus`
  - `JournalUpsert_ToFinished_RemovesFromNowPlaying`
  - `JournalUpsert_OtherUsersGame_ReturnsFalse_AndWritesNothing`
  - `DeleteGame_ClearsNowPlaying`
  - `GetSlots_NoDevices_ReturnsSingleGeneralSlot`
- [ ] **Step 2:** `dotnet test GamePlanner.Tests --filter FullyQualifiedName~NowPlayingTransitionTests`. Esperado: FAIL.
- [ ] **Step 3:** Escribir los SPs en `42_MvpProcedures.sql`, el manager, los DTOs y el controller. El mapeo a HTTP va en el controller.
- [ ] **Step 4:** Reconstruir la base de prueba y correr el Step 2. Esperado: PASS. Correr también la suite completa `dotnet test GamePlanner.Tests`. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(now-playing): device slots with atomic journal transitions`

### Task 3: Dispositivos y preferencias del usuario (backend)

**Files:**
- Create: `GamePlanner.API/Managers/UserSettingsManager.cs`, `Domain/UserSettingsDtos.cs`
- Modify: `Controllers/ProfileController.cs`, `Program.cs` (DI), `Database/42_MvpProcedures.sql`
- Test: `GamePlanner.Tests/UserSettingsTests.cs`

**Interfaces:**
- Produces:
  - `sp_SaveUserDevices @UserId, @Devices NVARCHAR(400)`: códigos separados por coma, en orden. Devuelve 0 = ok y 1 = un dispositivo que se quita tiene un juego en Now Playing (en ese caso no cambia nada).
  - `sp_SaveUserPreferences @UserId, @MaxGameHours, @SinglePlayer, @Narrative, @Combat, @FavoriteGenres NVARCHAR(MAX), @AvoidGenres NVARCHAR(MAX)`. Los géneros van por nombre, separados por `|` (hay géneros con coma); se ignoran los nombres que no existen en `Genres`.
  - `UserPreferencesDto { List<string> FavoriteGenres; List<string> AvoidGenres; int? MaxGameHours; string SinglePlayer; string Narrative; string Combat }`
  - `UserSettingsManager`:
    - `GetDevicesAsync(int userId) → List<string>`: la lista real, vacía si no hay ninguno.
    - `SaveDevicesAsync(int userId, List<string> devices) → SaveDevicesResult { Ok, Invalid, DeviceInUse }`. Invalid = un código no seleccionable o repetido.
    - `GetPreferencesAsync(int userId) → UserPreferencesDto`: valores por defecto si no hay fila (Neutral y listas vacías).
    - `SavePreferencesAsync(int userId, UserPreferencesDto p) → bool`. false = un género está en las dos listas, `MaxGameHours` está fuera de 1..500, o un valor de preferencia no es válido.
  - HTTP:
    - `GET` y `PUT /api/profile/devices` con body `{ devices: string[] }`. Respuestas: 400 o 409 `{ code: "device_in_use" }`.
    - `GET` y `PUT /api/profile/preferences` con body `UserPreferencesDto`. Respuesta: 400.
    - `GET /api/genres` → `string[]` con los géneros de los juegos activos del usuario, ordenados (alimenta los selectores).

- [ ] **Step 1: Escribir los tests que fallan:**
  - `SaveDevices_KeepsOrder`
  - `SaveDevices_RejectsGeneralAndDuplicates`
  - `SaveDevices_RemovingDeviceWithGame_ReturnsDeviceInUse`
  - `Preferences_DefaultWhenMissing`
  - `Preferences_RoundTrip`
  - `Preferences_GenreInBothLists_Rejected`
  - `Preferences_GenreWithComma_RoundTrips` (usar `"Hack and slash/Beat 'em up"` y un género creado con coma en el nombre)
- [ ] **Step 2:** correr el filtro `UserSettingsTests`. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Reconstruir la base de prueba y correr los tests. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(profile): user devices and preferences`

### Task 4: Sesiones de juego (backend)

**Files:**
- Create: `GamePlanner.API/Managers/PlaySessionManager.cs`, `Controllers/PlaySessionsController.cs`, `Domain/PlaySessionDtos.cs`, `Domain/PlaytimeCalculator.cs`
- Modify: `Database/42_MvpProcedures.sql`, `Program.cs`, `NowPlayingManager.cs` (cargar `RecentSessions`: las últimas 3)
- Test: `GamePlanner.Tests/PlaySessionTests.cs`, `GamePlanner.Tests/PlaytimeCalculatorTests.cs`

**Interfaces:**
- Produces:
  - `PlaySessionDto { int Id; int GameId; string Device; DateTime PlayedOn; decimal Hours; string? Note }`
  - `LogSessionRequest { string Device; DateTime? PlayedOn; decimal Hours; string? Note }`. `PlayedOn` NULL = hoy (UTC); una fecha futura → 400.
  - `PlaytimeCalculator.TotalHours(double steamHours, double loggedHours) → double`, según la regla de Global Constraints.
  - SPs:
    - `sp_InsertPlaySession` devuelve el Id nuevo, o -1 si el juego no es del usuario. Además pone `LastPlayedAt` del Journal en `PlayedOn`, creando la entrada en `Backlog` si no existía, sin cambiar el estado.
    - `sp_UpdatePlaySession @Id, @UserId, …` devuelve las filas afectadas.
    - `sp_DeletePlaySession @Id, @UserId` devuelve las filas afectadas.
  - `PlaySessionManager`:
    - `GetForGameAsync(userId, gameId) → List<PlaySessionDto>`, más reciente primero, solo de juegos activos.
    - `LogAsync(userId, gameId, LogSessionRequest) → int?` (null = el juego no es del usuario)
    - `UpdateAsync(userId, id, LogSessionRequest) → bool`
    - `DeleteAsync(userId, id) → bool`
  - HTTP:
    - `GET` y `POST /api/games/{gameId}/sessions`
    - `PUT` y `DELETE /api/sessions/{id}`
    - Validaciones → 400 con mensajes exactos: `"Hours must be between 0.1 and 24."`, `"Note must be 1000 characters or less."`, `"Date cannot be in the future."`, `"Unknown device."` (el dispositivo debe ser seleccionable o `General`).

- [ ] **Step 1: Escribir los tests que fallan:**
  - `PlaytimeCalculatorTests`: `(42, 12) → 42`, `(0, 12.5) → 12.5`, `(0, 0) → 0`.
  - `Log_UpdatesLastPlayedAt_WithoutChangingStatus` (el estado sigue `Playing`)
  - `Log_OtherUsersGame_ReturnsNull`
  - `Log_HoursOutOfRange_Rejected` (0 y 24.5, a nivel manager)
  - `Log_NeverTouchesSteamPlaytime` (`PlaytimeHours` sigue en 42)
  - `Delete_OtherUsersSession_ReturnsFalse`
  - `Sessions_OfDeletedGame_NotReturned`
- [ ] **Step 2:** correr los filtros. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Reconstruir la base de prueba y correr. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(sessions): log play sessions per device`

### Task 5: Biblioteca independiente de Steam (backend)

**Files:**
- Modify: `Domain/GameDto.cs`, `Mapping/GameMapping.cs`, `Managers/GameManager.cs`, `Controllers/GamesController.cs`, `Database/42_MvpProcedures.sql` (`sp_UpdateGame` amplía sus parámetros; nuevos `sp_SetGameFavorite` y `sp_SetGamePlayableDevices`)
- Test: `GamePlanner.Tests/GameLibraryTests.cs`

**Interfaces:**
- Produces:
  - Campos nuevos en `GameDto`: `string Store; bool IsFavorite; List<string> PlayableDevices`. `PlayableDevices` son los efectivos: los explícitos o `Stores.DefaultDevices`; vacía = cualquier dispositivo. También `string Status; double LoggedHours; int SessionCount; double TotalHours`.
  - Firma de mapeo: `GameMapping.ToDto(this Game game, string status, double loggedHours, int sessionCount)`. Se mantiene una sobrecarga `ToDto(this Game game)` que equivale a `("Backlog", 0, 0)` para los demás llamadores.
  - `GameManager.GetAllAsync` y `GetDetailAsync` completan estado, horas registradas y sesiones con un join agrupado. Nada de una consulta por juego.
  - `UpdateGameRequest` suma `string Store; List<string> PlayableDevices; bool IsFavorite; string? CoverImageUrl`. `PlayableDevices` vacía = usar el valor por defecto de la tienda. Tienda o dispositivo inválido → 400. `CreateGameRequest` y `CreateGameFromIgdbRequest` suman `string Store = "Other"`.
  - `PUT /api/games/{id}/favorite` con body `{ isFavorite: bool }` → 204 o 404.
  - `DELETE /api/games/{id}` sirve para cualquier juego del usuario, también los de Steam. La restricción de `Source` estaba solo en el frontend.

- [ ] **Step 1: Escribir los tests que fallan:**
  - `GetAll_ReturnsStatusFromJournal_DefaultBacklog`
  - `GetAll_TotalHours_UsesSessionsWhenNoSteam`
  - `Update_SetsStoreDevicesFavoriteCover`
  - `Update_EmptyDevices_UsesStoreDefaults` (Steam → `["PC","SteamDeck"]`)
  - `Update_InvalidStore_Rejected`
  - `Favorite_OtherUsersGame_NotFound`
- [ ] **Step 2:** correr el filtro. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Reconstruir la base de prueba, correr el filtro y la suite completa. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(library): store, playable devices, favorite, status and total hours`

### Task 6: Administración, Stats y correcciones del Dashboard (backend)

**Files:**
- Modify: `Services/AuthService.cs` (`GenerateToken`), `Controllers/WorkerController.cs`, `Controllers/SyncController.cs`, `Managers/StatsDashboardManager.cs`
- Test: `GamePlanner.Tests/AdminAuthorizationTests.cs` (con `ApiFactory`, sin base), `GamePlanner.Tests/StatsTests.cs`

**Interfaces:**
- Produces:
  - Si `user.IsAdmin`, el token lleva el claim `ClaimTypes.Role = "Admin"`.
  - `WorkerController` y `SyncController` usan `[Authorize(Roles = "Admin")]`.
  - Stats filtra `g.IsActive` y usa `PlaytimeCalculator.TotalHours` para horas, "más jugado" y "nunca jugado". "Nunca jugado" = horas totales 0 y estado `Backlog`.

- [ ] **Step 1: Escribir los tests que fallan:**
  - `Workers_NonAdminToken_Returns403`
  - `Workers_AdminToken_Returns200` (token firmado con `ApiFactory.Secret` y el rol Admin; si tocar la base hace fallar el host, alcanza con un `GET /api/worker` que no dé 403)
  - `GenerateToken_AdminUser_HasRoleClaim`
  - `Stats_ExcludesDeletedGames` (base de prueba: 3 juegos activos y 1 borrado → total 3; "más jugado" nunca es el borrado)
- [ ] **Step 2:** correr los filtros. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** correr. Esperado: PASS.
- [ ] **Step 5: Commit.** `fix: admin-only workers and sync, stats ignore deleted games`

## Block B — Recomendador

### Task 7: Tablas de conocimiento de juegos

**Files:**
- Create: `GamePlanner.API/Managers/WhatToPlay/GameKnowledge.cs`
- Test: `GamePlanner.Tests/GameKnowledgeTests.cs`

**Interfaces:**
- Produces: `static class GameKnowledge`. Todo se compara sin distinguir mayúsculas.
  - `SessionFit Fit(IEnumerable<string> genres, string sessionTime) → enum SessionFit { Good, Neutral, Poor }`. Reutiliza `ShortGenres` y `LongGenres` de `SessionPlannerManager`. Con `Short`: género corto → Good, género largo → Poor. Con `Long`: largo → Good, corto → Neutral. `Medium` y `Any` → Neutral.
  - `bool IsNarrative(genres, themes)`: Adventure, Role-playing (RPG), RPG, Visual Novel, Point-and-click, o el tema Drama.
  - `bool IsCombat(genres, tags)`: Shooter, Fighting, Hack and slash/Beat 'em up, o el tag action.
  - `bool IsSinglePlayer(modes)`: "Single player".
  - `string[] MoodGenres(string mood)`: el mapa `MoodGenres` de `SessionPlannerManager`, movido acá.
  - `string? LengthBucket(double? hours) → "Short" | "Medium" | "Long" | null`. Menos de 10, de 10 a 30 inclusive, más de 30.

- [ ] **Step 1:** Escribir tests de tabla para cada función, con los nombres exactos de arriba (incluido el límite: 10 → Medium y 30 → Medium).
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** correr. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(what-to-play): game knowledge tables`

### Task 8: `WhatToPlayScorer` (puro, determinista)

**Files:**
- Create: `GamePlanner.API/Managers/WhatToPlay/WhatToPlayScorer.cs`, `WhatToPlayModels.cs`, `Domain/WhatToPlayDtos.cs`
- Test: `GamePlanner.Tests/WhatToPlayScorerTests.cs` (sin base de datos)

**Interfaces:**
- Produces:
  - `WhatToPlayRequest { string? Device; string SessionTime = "Any"; string GameLength = "Any"; List<string> Genres = []; string? Mood; string? Perspective; List<string> Modes = []; string Strategy = "Default" }`. Valores: SessionTime/GameLength `Short | Medium | Long | Any`; Strategy `Default | Resume | Discover`.
  - `record WhatToPlayCandidate(int GameId, string Name, string? CoverImageUrl, string Store, IReadOnlyList<string> PlayableDevices, IReadOnlyList<string> Genres, IReadOnlyList<string> Themes, IReadOnlyList<string> Perspectives, IReadOnlyList<string> Modes, IReadOnlyList<string> Tags, string Status, double TotalHours, double? LengthHours, double? CriticRating, bool IsFavorite, bool InNowPlaying)`
  - `record WhatToPlayProfile(ISet<string> FavoriteGenres, ISet<string> AvoidGenres, int? MaxGameHours, string SinglePlayer, string Narrative, string Combat, ISet<string> LikedGenres, ISet<string> DislikedGenres, IReadOnlyDictionary<string,int> GenrePlayCounts)`
  - `WhatToPlayReasonDto { string Kind; string Text }` (Kind = `Positive | Neutral | Negative`)
  - `WhatToPlayResultDto { int GameId; string Name; string? CoverImageUrl; string Store; double? LengthHours; double TotalHours; int Score; string Match; List<WhatToPlayReasonDto> Reasons }`
  - `static List<WhatToPlayResultDto> WhatToPlayScorer.Rank(IEnumerable<WhatToPlayCandidate> candidates, WhatToPlayRequest request, WhatToPlayProfile profile, int take = 5)`

**Exclusiones duras**, en este orden:
- estado `Finished` o `Dropped`;
- `InNowPlaying`;
- el dispositivo pedido no está en `PlayableDevices` (lista vacía = compatible; `Device` null o `General` = compatible);
- algún género en `AvoidGenres`, salvo que ese género esté en `request.Genres`.

**Puntaje inicial** (spec §8: los pesos los decide el implementador, pero estos valores son el punto de partida y los tests fijan el orden relativo). Cada regla agrega a lo sumo una razón con el texto exacto indicado:

| Regla | Puntos | Razón |
|---|---|---|
| Algún género pedido coincide | +30 | Positive `"{Genre}, as requested"` |
| Algún género favorito (una sola vez) | +20 | Positive `"{Genre}, one of your favorite genres"` |
| Género en LikedGenres (si no aplicó favorito) | +10 | Positive `"Similar to games you liked"` |
| Género en DislikedGenres | −10 | Negative `"Similar to games you didn't enjoy"` |
| Duración dentro del rango pedido | +20 | Positive `"~{h} h to beat, {bucket} as requested"` |
| Duración fuera del rango pedido | −15 | Negative `"~{h} h, shorter than requested"` / `"longer than requested"` |
| Duración desconocida | 0 | Neutral `"No length data"` (siempre que `LengthHours` sea null) |
| Por encima de MaxGameHours | −20 | Negative `"~{h} h, above your {max} h limit"` |
| SessionFit Good / Poor | +15 / −10 | Positive `"Good fit for a {label} session"` / Negative `"Better for longer sessions"` o `"Better for short sessions"` |
| Preferencia Prefer que coincide (single, narrative, combat) | +10 c/u | Positive `"Single-player"` / `"Story-driven"` / `"Combat-focused"` |
| Preferencia Avoid que coincide | −15 c/u | Negative, mismos textos con el prefijo `"Not your preference: "` |
| Rating de la crítica | `clamp((r − 70) / 2, −10, +15)`, redondeado | Positive si ≥ 80: `"Critics: {r}"` |
| Favorito | +15 | Positive `"In your favorites"` |
| Estado Paused | +5 | Neutral `"Paused, pick up where you left off"` |
| Nunca jugado (TotalHours 0) | 0 | Neutral `"Never played"` |
| Mood, perspectiva o modo que coinciden | +10 c/u | Positive `"Matches your {mood} mood"` / `"{Perspective}"` / `"{Mode}"` |
| Strategy Resume y TotalHours > 0 | +25 | Positive `"Already started ({h} h)"` |
| Strategy Discover y el género principal tiene ≤ 1 juego jugado | +25 | Positive `"A genre you rarely play"` |
| Strategy Discover y el género es favorito | −10 | sin razón |

- `Match`: puntaje ≥ 60 → `"High"`, ≥ 30 → `"Medium"`, menos → `"Low"`.
- Orden: puntaje descendente; si empatan, `Name` ascendente sin distinguir mayúsculas; si vuelven a empatar, `GameId` ascendente.
- Las horas se muestran redondeadas a entero en las razones.

- [ ] **Step 1: Escribir los tests que fallan:**
  - `Excludes_FinishedDroppedAndNowPlaying`
  - `Excludes_NotPlayableOnDevice_ButEmptyListIsAny`
  - `Excludes_AvoidedGenre_UnlessRequested`
  - `Device_General_TreatsAllPlayable`
  - `RequestedGenre_RanksFirst`
  - `LengthOutsideBucket_StillReturned_WithShorterThanRequestedReason`
  - `UnknownEverything_NoThrow_NeutralWithNoLengthDataReason` (sin géneros, longitud, rating ni horas)
  - `Deterministic_SameInputSameOrder` (dos corridas, mismo orden; con empate, gana el nombre alfabético)
  - `Resume_PrefersStartedGames`
  - `Discover_PrefersRarelyPlayedGenre`
  - `AboveMaxHours_Penalized`
  - `EveryResult_HasAtLeastOneReason`
  - `Take_LimitsResults`
  - `EmptyCandidates_ReturnsEmpty`
- [ ] **Step 2:** `dotnet test GamePlanner.Tests --filter FullyQualifiedName~WhatToPlayScorerTests`. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** correr. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(what-to-play): deterministic explainable scorer`

### Task 9: `WhatToPlayManager`, endpoint y retiro de los recomendadores viejos

**Files:**
- Create: `GamePlanner.API/Managers/WhatToPlay/WhatToPlayManager.cs`, `Controllers/WhatToPlayController.cs`
- Modify: `Managers/DashboardManager.cs` (Tonight's Pick), `Program.cs`, `Controllers/SessionController.cs`
- Delete: `Managers/SessionPlannerManager.cs`, `Managers/SessionManager.cs`, `Managers/BacklogPrioritizerManager.cs`, `Controllers/BacklogPrioritizerController.cs` y sus DTOs (antes de borrarlos, `grep` de cada tipo para no dejar referencias). `SessionController` se borra completo si queda vacío.
- Test: `GamePlanner.Tests/WhatToPlayManagerTests.cs`

**Interfaces:**
- Consumes: Tasks 3, 5, 7, 8.
- Produces:
  - `WhatToPlayManager.RecommendAsync(int userId, WhatToPlayRequest request, int take, CancellationToken ct) → WhatToPlayResponseDto { string Device; List<WhatToPlayResultDto> Results }`.
  - El dispositivo por defecto, si es null, es el primero del usuario o `General`. Un dispositivo que no es del usuario → `ArgumentException`, que el controller convierte en 400.
  - Carga los candidatos en pocas consultas: juegos activos con géneros, temas, perspectivas, modos y tags; Journal; sesiones agrupadas; HLTB (por SteamAppId y después por nombre, como hacía el Prioritizer); Now Playing.
  - `LengthHours` = HLTB main story, si no `EstimatedMinutes / 60` (cuando es mayor a 0), si no null.
  - Arma el perfil: preferencias, gustos y disgustos según la regla de Global Constraints (los géneros de esos juegos), y `GenrePlayCounts` (juegos con horas totales > 0 por género).
  - `POST /api/what-to-play` con body `WhatToPlayRequest` y query `take` (por defecto 5, máximo 10) → 200.
  - Dashboard: `TonightRecommendationDto` se llena con `RecommendAsync(userId, new WhatToPlayRequest(), 1)`. `Reason` = el texto de la primera razón Positive (o de la primera razón).

- [ ] **Step 1: Escribir los tests que fallan** (base de prueba):
  - `Recommend_ExcludesFinishedAndNowPlaying_FromRealData`
  - `Recommend_UserWithoutDevices_UsesGeneral`
  - `Recommend_DeviceNotOwned_Throws`
  - `Recommend_DeletedGame_NeverReturned`
  - `Recommend_NoSteamUser_UsesLoggedHoursForResume`
  - `Profile_LikedFromRatingAndFavorite_DislikedFromDropped`
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar y borrar el código viejo.
- [ ] **Step 4:** `dotnet build GamePlanner.sln` (0 errores) y `dotnet test GamePlanner.Tests`. Esperado: PASS.
- [ ] **Step 5: Commit.** `feat(what-to-play): manager and endpoint; remove session planner and backlog prioritizer`

## Block C — Frontend

Comando de tests del frontend para todas las tasks: `cd game-planner-ui && npx ng test --watch=false --browsers=ChromeHeadless` (karma-chrome-launcher ya trae ChromeHeadless). Verificación visual: levantar API y UI y recorrer la pantalla con Playwright usando el usuario de prueba `uat-test@gameplanner.local` (UserId 17). Nunca usar los datos del UserId 2.

### Task 10: Servicios y modelos del frontend

**Files:**
- Create: `game-planner-ui/src/app/shared/mvp-constants.ts`, `services/what-to-play.service.ts`, `services/play-session.service.ts`, `services/user-settings.service.ts`, `shared/hours.ts`
- Modify: `services/now-playing.service.ts`, `services/game.service.ts` (interfaz `Game` y métodos nuevos), `services/auth.service.ts` (`isAdmin()`)
- Delete: `services/backlog-prioritizer.service.ts`, `recommend()` y `planSession()` de `game.service.ts` (cuando la Task 15 ya no los use, ver la nota de esa task)
- Test: `shared/hours.spec.ts`, `services/auth.service.spec.ts` (caso isAdmin)

**Interfaces:**
- Produces:
  - `DEVICE_LABELS: Record<string,string>`, `DEVICE_ICONS: Record<string,string>` (PC 🖥️, SteamDeck 🎮, Switch 🕹️, PlayStation 🎮, Xbox 🎮, Mobile 📱, Other ❓, General ⭐), `SELECTABLE_DEVICES: string[]`, `STORE_LABELS: Record<string,string>`, `JOURNAL_STATUSES: string[]`.
  - `parseHours(input: string): number | null`: acepta "1.5", "1,5" y " 2 "; devuelve null si está fuera de 0.1..24 o no es un número.
  - Interfaces TS que reflejan los DTOs de las Tasks 2–5 y 8–9, con los mismos nombres en camelCase.
  - `NowPlayingService.getSlots()`, `start(device, req)`, `remove(device, req)`.
  - `PlaySessionService.list(gameId)`, `log(gameId, req)`, `update(id, req)`, `delete(id)`.
  - `UserSettingsService.getDevices()`, `saveDevices(devices)`, `getPreferences()`, `savePreferences(p)`, `getGenres()`.
  - `WhatToPlayService.recommend(req, take = 5)`.
  - `GameService.updateGame(id, req)`, `setFavorite(id, isFavorite)`.
  - `AuthService.isAdmin(): boolean`: lee el claim `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` del payload. Sirve si el valor es `"Admin"` o si es un array que lo incluye.
- [ ] **Step 1:** Tests: `parseHours('1,5') === 1.5`, `parseHours('0') === null`, `parseHours('24.5') === null`, `parseHours('abc') === null`, `parseHours(' 2 ') === 2`. Para `isAdmin`: un token falso con el rol da true y uno sin rol da false.
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** correr. Esperado: PASS. Además `npx ng build` sin errores.
- [ ] **Step 5: Commit.** `feat(ui): services and models for MVP`

### Task 11: Perfil — dispositivos y preferencias

**Files:** Modify `components/profile/profile.component.{ts,html,css}`.

- Dos tarjetas nuevas debajo de "Account details":
  - **"My devices"**: chips para alternar dispositivos (los seleccionables) y flechas ↑/↓ para ordenarlos. Al guardar, si la API devuelve 409 `device_in_use`: `"Finish or remove the game on {Device} in Now Playing first."`
  - **"Preferences"**:
    - multiselects "Favorite genres" y "Genres to avoid" (con datos de `getGenres()`; un género elegido en una lista se deshabilita en la otra);
    - campo numérico "Max game length (hours)";
    - tres grupos de opciones "Single-player", "Story" y "Combat", cada uno con Prefer / Neutral / Avoid.
  - Cada tarjeta tiene su botón "Save" y muestra "Saved" al confirmar o el mensaje de error de la API.
- [ ] **Step 1:** Implementar.
- [ ] **Step 2:** `npx ng build`: sin errores.
- [ ] **Step 3:** Verificación visual con Playwright: elegir PC y Steam Deck, guardar, recargar y ver que siguen ahí. Guardar preferencias, recargar y ver que persisten. Restaurar después el estado del usuario de prueba.
- [ ] **Step 4: Commit.** `feat(ui): profile devices and preferences`

### Task 12: Now Playing (maqueta aprobada) y diálogos

**Files:**
- Modify: `components/now-playing/now-playing.component.{ts,html,css}` (reescritos; desaparecen el estante y los 3 lugares fijos)
- Create: `components/finish-game-dialog/` (reutilizable) y `components/log-session-dialog/`
- Modify: `app.module.ts`
- Test: `components/log-session-dialog/log-session-dialog.component.spec.ts`

**Interfaces:**
- Produces:
  - `<app-finish-game-dialog [gameName] (chosen)="…" (cancelled)="…">`, que emite `{ outcome: 'Finished'|'Dropped'|'Paused'|'None', rating: number | null }`. Textos exactos:
    - título `"How did it go with {gameName}?"`;
    - opciones `"✓ I finished it"` (con estrellas opcionales), `"✕ I dropped it"`, `"⏸ Pause it, I'll come back later"`, `"Just remove, keep status"`.
  - `<app-log-session-dialog [gameName] [device] (saved)="…" (cancelled)="…">`:
    - campos "Hours played", "How did it go? (optional)" y "Date" (por defecto hoy, no permite fechas futuras);
    - botón "Save session";
    - valida con `parseHours`; si falla, muestra `"Enter hours between 0.1 and 24."` y deshabilita el guardado.
- Comportamiento:
  - Encabezado "Now Playing" con el subtítulo `"One active game per device"`.
  - Un lugar por dispositivo, como en la maqueta: portada, nombre, primer género, tienda, `"Playing since {date}"`, horas (`"Steam: {x} h · Logged: {y} h"`, mostrando solo las que existan) y hasta 3 sesiones recientes (`"{date} · {h} h — "{note}""`). Botones "+ Log session" y "Finish / remove".
  - Lugar vacío: botón `"Pick a game"` y el link `"or get a recommendation"` → `/what-to-play?device={device}`.
  - Si el usuario no tiene dispositivos, aparece una línea debajo del encabezado: `"Using a single General slot. Add your devices in Profile."` con link a `/profile`.
  - El buscador de juegos oculta Finished y Dropped salvo que se marque `"Show finished and dropped"`.
  - Elegir un juego para un lugar ocupado abre primero el diálogo de terminar y después llama a `start` con `outgoingOutcome`.
  - Errores de la API: 409 `game_on_other_device` → `"That game is already on another device."`; los demás → `"Could not save. Try again."`.
- [ ] **Step 1:** Tests del diálogo de sesión: con "1,5" emite `saved` con `hours: 1.5`; con "0" muestra el error y no emite; una fecha futura deshabilita el guardado.
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar los diálogos y la pantalla. Los estilos usan las variables de `styles.css`, tomando como referencia el CSS de `now-playing.html` de la maqueta.
- [ ] **Step 4:** correr los tests. Esperado: PASS. `npx ng build` sin errores.
- [ ] **Step 5:** Verificación visual con Playwright y captura comparada con la maqueta: poner un juego, registrar 1,5 h con nota, terminarlo con 4 estrellas y comprobar en `/journal` que figura Finished con ★ 4.
- [ ] **Step 6: Commit.** `feat(ui): device-based Now Playing with finish and log-session dialogs`

### Task 13: My Games — tarjetas, filtros, edición y borrado

**Files:**
- Modify: `components/game-list/*`, `components/game-card/*`
- Create: `components/game-edit-dialog/`, `components/confirm-dialog/` (genérico: `[title] [message] [confirmLabel] (confirmed) (cancelled)`)
- Test: `components/game-list/game-list.component.spec.ts` (ampliar el existente)

- Tarjeta:
  - portada, nombre, chip de estado (los colores del Journal: Playing verde, Replaying azul, Finished violeta, Paused amarillo, Backlog gris, Dropped rojo);
  - etiqueta de la tienda y horas totales (`"Not played"` si son 0);
  - hasta 3 géneros con ícono y texto, y `"+N"` si hay más;
  - corazón de favorito que alterna con `setFavorite`;
  - menú "⋯" con "Edit" y "Delete".
- Borrar abre el diálogo de confirmación: `"Delete {name}?"` / `"This removes it from your library, Now Playing and recommendations."` / `"Delete"`. Vale para todos los juegos, también los de Steam.
- El diálogo de edición tiene los campos de `UpdateGameRequest`: nombre, URL de portada, tienda, dispositivos (chips; ninguno = `"Store default"`), duración estimada en horas (se convierte a minutos), géneros (texto separado por comas, como hoy) y favorito.
- Filtros: búsqueda; Status, Store y Device (multiselección); Genre (los chips actuales); Length (Short, Medium, Long, Unknown, con los mismos rangos que `LengthBucket`, calculados sobre `estimatedMinutes`); y "Favorites only". Las pestañas Played/Backlog se reemplazan por chips de estado con su contador.
- El botón "Import from Steam" junto a "+ Add Game" navega a `/steam`.
- [ ] **Step 1:** Tests:
  - `filters by status`
  - `filters by store and device (empty playableDevices matches any device)`
  - `length Unknown matches games with estimatedMinutes 0`
  - `favorites only`
  - `clear all resets every filter`
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** correr. Esperado: PASS. `npx ng build`.
- [ ] **Step 5:** Verificación visual: editar la tienda y los dispositivos de un juego de prueba, recargar y ver que persisten. Borrar pide confirmación.
- [ ] **Step 6: Commit.** `feat(ui): library status/store/device filters, edit and confirmed delete`

### Task 14: Detalle del juego — estado e historial de sesiones

**Files:** Modify `components/game-detail/*`. La Phase 6 ya tiene que estar commiteada (Global Constraints).

- La tarjeta "My Journal" sigue igual. Después de guardar, si el estado quedó distinto de Playing o Replaying, se avisa `"Removed from Now Playing."` cuando el juego estaba ahí (comparando con `GET /api/now-playing` antes y después).
- Tarjeta nueva **"Play sessions"**:
  - total `"Logged: {h} h in {n} sessions"` y, si hay horas de Steam, `"Steam: {x} h"`;
  - lista de sesiones (fecha, ícono y etiqueta del dispositivo, horas, nota) con "Edit", que reutiliza `<app-log-session-dialog>` precargado, y "Delete", con `<app-confirm-dialog>`;
  - botón "+ Log session": si el juego está en Now Playing usa ese dispositivo; si no, abre el diálogo con un selector de dispositivos del usuario (o General).
  - Estado vacío: `"No sessions yet. Log one from Now Playing or here."`
- [ ] **Step 1:** Implementar. Si el diálogo de sesión necesita un selector de dispositivo opcional, agregar el input `[devices]: string[] | null` y un test que confirme que el selector aparece solo cuando `devices` no es null.
- [ ] **Step 2:** `npx ng test` y `npx ng build`. Esperado: PASS.
- [ ] **Step 3:** Verificación visual: editar y borrar una sesión.
- [ ] **Step 4: Commit.** `feat(ui): play session history on game detail`

### Task 15: What to Play (maqueta aprobada)

**Files:**
- Create: `components/what-to-play/*`
- Delete: `components/session-planner/*`, `components/backlog-prioritizer/*` y sus declaraciones en `app.module.ts`
- Modify: `app-routing.module.ts`
- Test: `components/what-to-play/what-to-play.component.spec.ts`

- Encabezado "What to Play" con el subtítulo `"Based on your library and preferences. Finished, dropped and now-playing games are left out."`
- Panel con 4 grupos, como en la maqueta:
  - **Device**: dispositivos del usuario, o "General". Preseleccionado el del parámetro `?device=` o el primero.
  - **Session time**: `"< 1 h"`, `"1–2 h"`, `"2 h+"`, `"Any"`.
  - **Game length**: `"Short < 10 h"`, `"Medium 10–30 h"`, `"Long 30 h+"`, `"Any"`.
  - **Genre**: `"Any"` más los géneros de `getGenres()`, multiselección.
  - Un desplegable "▸ More options" con: Mood (las opciones de mood del planner actual), Perspective, Mode y Strategy (`"Default"`, `"Resume started games"`, `"Discover new genres"`).
- Recomienda al cargar la página con los valores por defecto, y otra vez cada vez que cambia una opción (con un debounce de 300 ms).
- Resultado principal:
  - portada, `"Top pick"`, nombre;
  - las razones (✓ verde para Positive, • amarillo para Neutral, • rojo para Negative);
  - `"Match: {High|Medium|Low} ({score})"`;
  - botones `"▶ Start on {Device label}"` y "View details".
- Alternativas: hasta 4 filas, cada una con nombre, una línea con las primeras 2 razones y la coincidencia.
- "Start on …" usa `NowPlayingService.start`. Si responde 409 `slot_occupied`, abre `<app-finish-game-dialog>` para el juego actual del lugar y reintenta con el resultado. Al terminar navega a `/now-playing`.
- Estado vacío: `"Nothing to recommend with these options. Try another device or Any length."` Si la biblioteca está vacía: `"Your library is empty. Add games or import from Steam."` con links.
- [ ] **Step 1:** Tests con el servicio simulado:
  - `requests with defaults on init`
  - `uses ?device= query param`
  - `renders reasons with kind classes`
  - `start on 409 opens finish dialog`
  - `shows empty state when results empty`
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar y borrar las pantallas viejas. Ya podés quitar `recommend()` y `planSession()` de `GameService` (Task 10).
- [ ] **Step 4:** correr. Esperado: PASS. `npx ng build`.
- [ ] **Step 5:** Verificación visual comparada con `recommender.html`.
- [ ] **Step 6: Commit.** `feat(ui): What to Play replaces session planner and backlog prioritizer`

### Task 16: Navegación, Dashboard, Calendar Tracked y Administration

**Files:** Modify `components/sidebar/*`, `app-routing.module.ts`, `components/dashboard/*`, `components/calendar/*`. Además `guards/` (crear `admin.guard.ts`).

- Menú principal en este orden:
  - Home (`/dashboard`)
  - Now Playing
  - What to Play (`/what-to-play`)
  - My Games
  - Journal
  - Calendar
  - Stats
- Sección "Administration" con Workers, visible solo si `isAdmin()`. La ruta `/workers` usa `AdminGuard`, que redirige a `/dashboard`.
- Redirecciones:
  - `/planner` y `/backlog-prioritizer` → `/what-to-play`
  - `/tracked-games` → `/calendar?tab=tracked`
  - `/steam` sigue existiendo (se entra desde My Games) pero no aparece en el menú.
- Calendar: pestañas `"Releases"` y `"Tracked"`. "Tracked" muestra `<app-tracked-games>` embebido. El componente se reutiliza; si queda anidado, se oculta su encabezado con `@Input() embedded = false`.
- Dashboard:
  - los botones del encabezado pasan a ser "What to Play", "Now Playing" e "Import Steam";
  - "Continue Playing" muestra el dispositivo de cada juego;
  - el botón "Add to Session" de Tonight's Pick pasa a ser `"▶ Start on {first device}"`, con el mismo flujo de la Task 15;
  - `"(in 0d)"` pasa a mostrarse como `"Today"` y `1` como `"Tomorrow"`.
- [ ] **Step 1:** Tests: `admin.guard.spec.ts` (sin rol redirige, con rol deja pasar); spec del sidebar (sin admin no aparece "Workers").
- [ ] **Step 2:** correr. Esperado: FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** correr. Esperado: PASS. `npx ng build`.
- [ ] **Step 5:** Verificación visual: las redirecciones funcionan y el usuario de prueba (no admin) no ve Workers.
- [ ] **Step 6: Commit.** `feat(ui): simplified navigation, admin section, tracked tab, dashboard actions`

### Task 17: Textos y escala tipográfica (absorbe la Phase 9)

**Files:** `game-planner-ui/src/styles.css` y el CSS y los textos de las pantallas tocadas en las Tasks 11–16, más Journal y Stats.

- Una sola escala tipográfica en `styles.css`, como variables: `--fs-xs: 0.75rem; --fs-sm: 0.875rem; --fs-md: 1rem; --fs-lg: 1.25rem; --fs-xl: 1.5rem; --fs-2xl: 2rem`. Los `font-size` fijos de las pantallas tocadas se reemplazan por la variable más cercana. Los `h1` de página usan `--fs-2xl` y la misma fuente; esto quita el degradado que hoy solo tiene el título del Journal.
- Todo estado vacío y todo error de las pantallas tocadas dice el próximo paso, con un link o botón (por ejemplo, Journal: `"No entries yet. Start a game in Now Playing and it shows up here."` con un link a `/now-playing`). Los errores de carga dicen `"Could not load {thing}. Check that the API is running, then reload."`
- [ ] **Step 1:** Implementar.
- [ ] **Step 2:** `npx ng build` y `npx ng test`. Esperado: PASS.
- [ ] **Step 3:** Capturas con Playwright de las 8 pantallas principales (las rutas del menú más el detalle de un juego). Revisarlas comparando tamaños de títulos y estados vacíos; adjuntarlas al resumen.
- [ ] **Step 4: Commit.** `style(ui): single type scale and actionable empty/error states`

## Block D — Cierre

### Task 18: E2E del flujo diario, documentación y planificación

**Files:**
- Modify: `e2e/tests/uat.spec.ts` (o crear `e2e/tests/daily-flow.spec.ts` con el mismo setup de autenticación), `README.md`, `.planning/ROADMAP.md`, `.planning/REQUIREMENTS.md`
- Create: `docs/MVP_RELEASE_CHECKLIST.md`

- E2E `daily flow`, con el usuario de prueba y juegos con el prefijo `uat-{ts}`:
  1. configura los dispositivos PC y SteamDeck por API;
  2. desde What to Play elige el top pick en Steam Deck;
  3. registra una sesión de 1,5 h con nota;
  4. lo termina con 4 estrellas;
  5. verifica en `/journal` que figura Finished, en el detalle que figura la sesión, y que What to Play ya no lo ofrece.
  
  Al final limpia lo que creó y restaura los dispositivos.
- E2E `non-admin cannot see workers`.
- README: instalación (orden de los scripts, incluidos 40–42), configuración (`IsAdmin` se activa con `UPDATE Users SET IsAdmin = 1 WHERE Id = …`; hay que volver a iniciar sesión para que el token lleve el rol), funcionalidades y limitaciones conocidas (la tienda se carga a mano; solo Steam importa automáticamente).
- ROADMAP/REQUIREMENTS: Phase 8 → "Superseded by MVP (play sessions)", explicando por qué PLAY-02 deja de aplicar; Phase 9 → "Absorbed by MVP Task 17". Seguir el formato existente del archivo.
- `docs/MVP_RELEASE_CHECKLIST.md`: funcionalidades completadas, tests ejecutados con resultados reales (pegar los totales de `dotnet test`, `ng test` y Playwright), problemas conocidos, funcionalidades postergadas (spec §3), pasos para ejecutar y estado final. No marcar nada como verificado si no se ejecutó.
- [ ] **Step 1:** Escribir los e2e.
- [ ] **Step 2:** `cd e2e && npx playwright test`. Esperado: PASS. Antes leer `e2e/global-setup.ts` para ver qué variables de entorno necesita (`UAT_EMAIL`, `UAT_PASSWORD`).
- [ ] **Step 3:** Suite completa: `dotnet test GamePlanner.Tests`, `cd game-planner-ui && npx ng test --watch=false --browsers=ChromeHeadless`, e2e. Todo PASS.
- [ ] **Step 4:** Escribir los documentos con los resultados reales.
- [ ] **Step 5: Commit.** `docs: MVP release checklist, README and roadmap update; e2e daily flow`
