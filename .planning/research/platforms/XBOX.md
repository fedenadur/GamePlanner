# Xbox / Game Pass — Viabilidad de integración

**Proyecto:** GamePlanner v1.1 (solo documentación, sin cambios de código)
**Investigado:** 2026-10-27
**Confianza global:** MEDIA (flujo de auth y endpoints verificados contra docs de Microsoft, código fuente de librerías comunitarias y llamadas reales a los catálogos públicos; el acceso a datos de usuario no se pudo probar end-to-end sin una cuenta Xbox)

## Resumen

**Veredicto: PARCIAL.** Se puede vincular una cuenta Microsoft/Xbox con un flujo OAuth documentado por Microsoft, y con el token resultante leer historial de títulos jugados, logros, horas (cuando el juego las reporta) y capturas del usuario. Pero casi todo lo que está detrás del login son endpoints Xbox Live **no documentados como API pública para terceros** (titlehub, mediahub) o documentados solo para titles/partners (userstats, achievements), y no existe ninguna API —oficial ni no oficial— para la **biblioteca poseída** del usuario: solo "títulos jugados". El catálogo de Game Pass y los metadatos/portadas/screenshots oficiales de cada juego sí están disponibles **sin autenticación** (no oficial, pero estable y verificado hoy). A diferencia de Steam (una API key + `GetOwnedGames`), aquí hay que mantener un OAuth con refresh token por usuario, aceptar riesgo de ToS "zona gris" y asumir que la librería importada será "lo que jugaste", no "lo que tenés".

## Tabla de capacidades

| Capacidad | ¿Posible? | Vía | Oficial / No oficial | Confianza |
|-----------|-----------|-----|----------------------|-----------|
| **Login / vincular cuenta** | Sí | OAuth2 Microsoft Entra v2 (`/consumers`, scope `Xboxlive.signin Xboxlive.offline_access`) → Xbox User Token → XSTS token | Flujo **documentado** por Microsoft ("Xbox services sign-in for title websites"), pero pensado para webs de títulos/publishers; uso por una app de terceros no es un programa soportado | Alta (flujo) / Media (aceptación por MS) |
| **Biblioteca (jugados)** | Sí, parcial | `titlehub.xboxlive.com .../titles/titlehistory/decoration/...` (o OpenXBL `GET /v2/titles`) | No oficial (endpoint sin documentación pública en Learn; reverse-engineered) | Media-Alta |
| **Biblioteca (poseídos)** | **No** | Store Collections API solo sirve a titles propios con Partner Center; no hay endpoint de "mi biblioteca" | — | Alta (que no existe) |
| **Catálogo Game Pass** | Sí (catálogo, **no** pertenencia del usuario) | `catalog.gamepass.com/sigls/v2?id=<siglId>` + `displaycatalog.mp.microsoft.com/v7.0/products` (sin auth) u OpenXBL `/v2/gamepass/*` | No oficial (endpoints de la web/app de Xbox, sin contrato) | Alta (verificado hoy), Media (estabilidad) |
| **Metadatos de juego** | Sí | `displaycatalog` (título, descripción, desarrollador, fecha, rating ESRB/PEGI, categoría, `XboxTitleId`, PFN) o `titlehub` decoration `detail` | No oficial / semi-público | Alta |
| **Fotos / screenshots** | Sí | (a) oficiales del juego: `displaycatalog` `ImagePurpose=Screenshot` (3840x2160); (b) capturas del usuario: `mediahub.xboxlive.com/screenshots/search` o `screenshotsmetadata.xboxlive.com` / OpenXBL `GET /v2/dvr/screenshots` | No oficial | (a) Alta, (b) Media |
| **Portadas** | Sí | `displaycatalog` `ImagePurpose=Poster` (p. ej. 720x1080 / 1440x2160), `BoxArt` (cuadrada 1080x1080 / 2160x2160); titlehub decoration `image` | No oficial | Alta (verificado hoy) |
| **Horas jugadas** | Parcial | `userstats.xboxlive.com/users/xuid({xuid})/scids/{scid}/stats/MinutesPlayed` (o `POST /batch`); OpenXBL `POST /v2/player/stats` | Endpoint **documentado** en Microsoft Learn, pero la stat es definida por cada título; cobertura no garantizada | Media |
| **Logros** | Sí | `achievements.xboxlive.com/users/xuid({xuid})/achievements?titleId=...` (v2) y titlehub decoration `achievement` (progreso/gamerscore); OpenXBL `/v2/achievements`, `/v3/achievements` | Endpoint Xbox Live REST documentado (rate limit publicado); acceso por terceros vía token de usuario no soportado formalmente | Alta (técnico) / Media (política) |

## Autenticación / login

### Flujo (documentado por Microsoft)
1. Registrar una app en **Microsoft Entra ID** (portal Azure) con *Supported account types = "Personal Microsoft accounts only"*, redirect URI web, y un **client secret**. Cualquiera con una cuenta Azure/Microsoft puede hacerlo (no requiere aprobación ni ID@Xbox).
2. Redirigir al usuario a `https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?...&scope=xboxlive.signin xboxlive.offline_access`. Solo esos scopes (no se pueden mezclar otros; agregar `User.Read` rompe la petición).
3. Canjear el `code` en `.../consumers/oauth2/v2.0/token` (con client secret) → access token + refresh token.
4. `POST https://user.auth.xboxlive.com/user/authenticate` (`RpsTicket: d=<access token>`, `RelyingParty: http://auth.xboxlive.com`) → **User Token** + `uhs`.
5. `POST https://xsts.auth.xboxlive.com/xsts/authorize` → **XSTS token**; la comunidad usa `RelyingParty: http://xboxlive.com` para acceder a los servicios públicos de Xbox Live (la doc de MS solo describe "tu propio Relying Party", que es para partners).
6. Llamadas REST con `Authorization: XBL3.0 x=<uhs>;<xsts_token>` y header `x-xbl-contract-version` (1/2/3 según servicio; titlehub usa `2`, mediahub `3`, screenshotsmetadata `5`).

La librería comunitaria `xbox-webapi-python` implementa exactamente esto (`DEFAULT_SCOPES = ["Xboxlive.signin", "Xboxlive.offline_access"]`) y su última actividad es 2026-08-13 (v2.1.0), así que el flujo sigue vigente.

### Quién puede obtener credenciales
- **Azure app registration:** cualquiera. Con tenant Entra propio (cuenta gratuita). El scope `XboxLive.signin` **no aparece** en "Request API permissions"; hay que pedirlo directamente en la URL de autorización contra `/consumers`. Con `/common` o con scopes extra suele fallar (`invalid_scope` / `Unauthorized`).
- **Acceso "oficial" completo** (scope `service::user.auth.xboxlive.com::MBI_SSL`, sin pantalla de consentimiento manual, RP propio) requiere ser **ID@Xbox / Xbox Creators Program / Partner Center**. Según moderador de Microsoft: "si tu app no tiene partnership Xbox, el usuario debe autorizar manualmente".
- **Restricciones de cuenta:** cuentas de menores (<18) devuelven XErr `2148916238`; cuentas sin perfil Xbox `2148916233`; países sin Xbox Live `2148916235`. `xbox-webapi-python` documenta explícitamente "solo cuentas no-niño (>18)".
- **Sin XUID por OpenID:** a diferencia de Steam OpenID (devuelve SteamID64 sin secretos), aquí hay que guardar un **refresh token por usuario** (secreto de larga vida) y renovar User/XSTS tokens (expiran en horas/días). Si el refresh falla, hay que re-loguear.

### Alternativa: OpenXBL (xbl.io), proxy no oficial
- Se loguea en xbl.io con la cuenta Microsoft y se obtiene una API key personal; se llama con `X-Authorization: <key>` a `https://api.xbl.io/v2/...`. Para apps con varios usuarios existen "App Keys" con flujo SSO (`GET /app/auth/{publicKey}` → `/app/callback` → `POST /app/claim`).
- Ventaja: cero manejo de XSTS/refresh. Desventaja: **terceros guardan los tokens Xbox del usuario**, dependencia de un servicio pequeño sin SLA (salvo Enterprise), y hay que pagar para volumen.

## Endpoints / fuentes de datos (por capacidad)

> Todos los endpoints de Xbox Live listados salvo displaycatalog/catalog.gamepass requieren el header `Authorization: XBL3.0 x=<uhs>;<xsts>`.

### Biblioteca (títulos jugados)
- `GET https://titlehub.xboxlive.com/users/xuid({xuid})/titles/titlehistory/decoration/{fields}?maxItems=N` con `x-xbl-contract-version: 2`. Fields soportados por la librería: `scid, achievement, stats, gamepass, image, detail, friendswhoplayed, alternateTitleId, productId, contentBoard`.
- Devuelve por título: `titleId`, `pfn`, `serviceConfigId` (scid), `lastTimePlayed`, progreso de logros (`currentAchievements/totalAchievements/currentGamerscore/totalGamerscore/progressPercentage`), `gamePass.isGamePass`, imágenes y `detail` (género, fecha, descripción, minAge).
- **No incluye minutos jugados** en el modelo de titlehub; las horas vienen de userstats.
- Cubre Xbox One/Series, 360 (retrocompatible) y juegos de PC que usen servicios Xbox Live (Xbox app/Game Pass PC). **No** aparecen juegos de Microsoft Store/Steam sin integración Xbox Live ni juegos poseídos pero nunca jugados.
- OpenXBL: `GET /v2/titles` ("games played with playtime and last played dates") y `GET /v2/titles/{xuid}`.

### Horas jugadas
- `GET https://userstats.xboxlive.com/users/xuid({xuid})/scids/{scid}/stats/MinutesPlayed` (contract-version 2), o `POST https://userstats.xboxlive.com/batch` con `{"arrangebyfield":"xuid","stats":[{"name":"MinutesPlayed","titleId":...}],"xuids":[...]}`.
- Requiere el `scid`/`titleId` de cada juego (sale de titlehub). Son N llamadas (o batch) por sincronización.
- **Caveat:** Microsoft indica que no todos los juegos registran el tiempo jugado; las stats las define cada título en Partner Center. Los juegos sin la stat devolverán vacío. Offline/tiempo en PC puede no contarse (discusión sin resolver en el proyecto Xodus). Tratar como "mejor esfuerzo", igual que HLTB en tu app.
- OpenXBL: `POST /v2/player/stats` (misma forma de body: `xuids`, `stats[{name,titleId}]`).

### Logros
- `GET https://achievements.xboxlive.com/users/xuid({xuid})/achievements?titleId={titleId}` (v2, Xbox One/Series) y variantes `x-xbl-contract-version: 1` para 360.
- Progreso agregado por título ya viene en titlehub decoration `achievement`.
- OpenXBL: `/v2/achievements/player/{xuid}/{titleId}`, `/v3/achievements` (incluye title history/stats).

### Capturas del usuario
- `POST https://mediahub.xboxlive.com/screenshots/search` (contract-version 3), body `{"max":N,"skip":M,"query":"OwnerXuid eq <xuid>"}`; también `gameclips/search`.
- `GET https://screenshotsmetadata.xboxlive.com/users/xuid({xuid})/titles/{titleId}/screenshots` (contract-version 5) para filtrar por juego. Existe un issue abierto de la librería (#57, 2021) reportando que este endpoint no trae lo último; mediahub es el camino más actual.
- OpenXBL: `GET /v2/dvr/screenshots` (paginado con `continuationToken`).
- Las capturas dependen de que el usuario las haya subido a la nube y de su configuración de privacidad. Falta verificar si las URLs de contenido son permanentes o caducan (impacta si se descargan a disco o se enlazan).

### Metadatos y portadas (sin autenticación)
- `GET https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=<productId[,..]>&market=US&languages=en-US&fieldsTemplate=Details`
- **Verificado hoy (2026-10-27)** con `9NBLGGH537BL` (Minecraft) y `9NPDN9R45JX4` (1000xRESIST): respuesta JSON pública con `ProductTitle`, descripción, desarrollador/publisher, `OriginalReleaseDate`, `ContentRatings` (ESRB/PEGI/…), `Category`, `PackageFamilyName`, y `AlternateIds` incluyendo `XboxTitleId` (p. ej. `1828326430`) — es decir, permite puentear `productId` ⇄ `titleId`/PFN.
- Imágenes por `ImagePurpose`: `Poster` (720x1080 / 1440x2160, vertical, la más parecida a la portada IGDB), `BoxArt` (cuadrada), `SuperHeroArt`/`TitledHeroArt` (1920x1080 / 3840x2160), `BrandedKeyArt` (584x800), `Screenshot` (11-20 por juego, 3840x2160), a veces `CMSVideos`.
- Búsqueda por nombre: `GET .../v7.0/productFamilies/autosuggest?query=...&market=US&languages=en-US&productFamilyNames=Games,Apps` (según `xbox-webapi-python`).
- Estado: no es una API contratada; es el backend de la tienda de Microsoft. En la práctica lleva años estable y la usan muchas herramientas, pero puede cambiar sin aviso.

## Game Pass: catálogo vs biblioteca del usuario

- **Catálogo (qué juegos están en Game Pass hoy):** `GET https://catalog.gamepass.com/sigls/v2?id=<siglId>&language=en-US&market=US` devuelve un array: primer elemento = metadatos de la lista (`siglId`, `title`), el resto = `{ "id": "<productId>" }` (ProductId de la tienda, p. ej. `9NPDN9R45JX4`). Luego se resuelven detalles con `displaycatalog`. **Verificado hoy**, sin auth:
  - `fdd9e2a7-0fee-49f6-ad69-4354098401ff` — "All PC Games" (518 ids)
  - `f6f1f99f-9b49-4ccd-b3bf-4d9767a77f5e` — "All Console Games" (625 ids)
  - `29a81209-df6f-41fd-a528-2ae6b91f719c` — "All games" (564 ids)
  - `eab7757c-ff70-45af-bfa6-79d3cfb2bf81` — "Most popular" (20 ids)
  (Los siglId provienen de la comunidad/OpenXBL; no hay catálogo oficial de ids y pueden rotar.)
- OpenXBL ya envuelve esto: `GET /v2/gamepass/all|pc|ea-play|new|coming|leaving`, `POST /v2/gamepass/products`.
- **Pertenencia del usuario a Game Pass:** no encontré endpoint para saber si el usuario tiene suscripción activa ni qué juegos "tiene" por Game Pass. Lo único cercano: titlehub decoration `gamepass` → `isGamePass` marca si el *título* es de Game Pass, no si el *usuario* lo tiene.
- **Consecuencia de producto:** Game Pass sirve como **etiqueta de disponibilidad** ("este juego de tu backlog está en Game Pass ahora/saliendo") o como fuente de descubrimiento, no como biblioteca importable. Los juegos de Game Pass que el usuario instaló y jugó aparecerán en titlehistory como cualquier otro.

## Mapeo con IGDB

- IGDB `external_games` tiene los campos `external_game_source`, `uid`, `url`, `platform`, `game`, `name`, `year`. El campo `category` está **DEPRECADO** ("Use external_game_source instead"); el enum histórico incluye `microsoft = 11`, `xbox_marketplace = 31` y `xbox_game_pass_ultimate_cloud = 54` (más `steam = 1`, `epic_game_store = 26`, `gog = 5`). Los nombres/ids actuales se consultan en el endpoint `external_game_sources`.
- **Hipótesis a validar con un spike:** el `uid` de las fuentes Microsoft/Xbox es el Product ID de la Microsoft Store (el `9Nxxxxxxxxxx` que usa `displaycatalog`), que es lo que `titlehub` expone como `productId` (decoration) y lo que Game Pass devuelve en `sigls`. **No lo pude verificar** (requiere credenciales Twitch/IGDB y consulta real, p. ej. `fields game,uid,url,name,external_game_source; where external_game_source = X & uid = "9NBLGGH537BL";`). Tampoco medí qué porcentaje del catálogo Xbox tiene fila en IGDB (cobertura es mantenida por la comunidad → esperar huecos, sobre todo juegos indie y ports de PC).
- **Estrategia de mapeo recomendada (de mayor a menor confianza):**
  1. `productId` (titlehub `productId` / displaycatalog `ProductId`) → `external_games.uid` con fuente Microsoft/Xbox → `game` (IGDBId).
  2. Si no hay fila: búsqueda por nombre normalizado + año contra IGDB `games` (el mismo fallback que ya usás para HLTB por nombre) y confirmación manual en UI.
  3. Guardar `XboxTitleId`, `Pfn` y `ProductId` junto al juego para poder volver a mapear.
- `platform` en `external_games` ayuda a distinguir Xbox One/Series vs PC de un mismo juego.
- Las fuentes IGDB para Xbox (31, 54) apuntan a cobertura en tienda / cloud streaming; no asumir que todo juego de Game Pass tiene fila `microsoft`.

## Limitaciones, rate limits y Términos de servicio

### Rate limits
- **Xbox Live (directo):** *Fine-Grained Rate Limiting* por **usuario + título/app**, doble ventana (burst 15 s / sustain 300 s). Publicados: Stats Read 100/300, Achievements 100/300, Profile 10/30, Social 10/30, Presence read 10/100 (burst/sustain). Responde `429` + `Retry-After`; las peticiones rechazadas también cuentan para la ventana larga. Para una librería personal (cientos de títulos) alcanza con throttling básico (p. ej. ~100 req / 15 s para userstats) y ejecución por worker.
- **OpenXBL:** free 150 req/h, Small $5 (500/h), Medium $15 (2.500/h), Large $35 (5.000/h), Enterprise a medida. Además aplican los límites de Microsoft por usuario. (La página de npm de un cliente de terceros menciona 500/h gratis: dato viejo; vale la tabla de precios actual.) 150/h es justo para sincronizar una librería grande con stats por título; diseñar sync incremental.
- **displaycatalog / catalog.gamepass.com:** sin límite documentado; sin auth. Cachear agresivamente (catálogo Game Pass 1 vez/día).

### Términos de servicio y riesgo legal
- **No existe programa público que otorgue a una app personal acceso a Xbox Live REST.** El flujo `Xboxlive.signin` está documentado pero dentro de documentación GDK para publishers/titles; el acceso sin partnership requiere consentimiento manual del usuario y no es un contrato de API.
- Los Microsoft Services Agreement (resumen vía lectura de la página; las secciones exactas conviene revisarlas, la página es dinámica) prohíben "reverse engineer", "circumvent any restrictions on access" y uso no autorizado que interfiera con otros; los servicios Xbox son "solo uso personal y no comercial". Usar endpoints no documentados con el XSTS del propio usuario encaja en la zona gris: **no hay cláusula que lo permita explícitamente**, y hay riesgo teórico de revocación de token o acción sobre la cuenta. Para uso personal, solo lectura, con la cuenta propia y volumen bajo, el riesgo práctico es **bajo**, pero no cero; **no encontré** reportes de baneos por lectura con estas librerías (ausencia de evidencia, no garantía).
- Apps multiusuario/publicadas: riesgo mayor (muchos tokens de terceros en tu backend, dato personal, posible reclamo de MS). Además guardar refresh tokens implica obligaciones de seguridad (cifrado en reposo; tu milestone v1.0 ya endureció secretos).
- OpenXBL se declara "no afiliado a Microsoft", sin garantías ni uptime, y traslada a vos la responsabilidad de no violar los ToS de Microsoft (cláusulas de indemnización). No documenta cómo almacena tokens.
- `displaycatalog`/`catalog.gamepass.com`: no oficiales; sin ToS de API propios. Uso de imágenes de la tienda: son material de Microsoft/publishers (igual que carátulas IGDB, mostrar en UI personal es el uso habitual; no redistribuir).

### Estabilidad
- Auth y titlehub/userstats/achievements: estables hace años (mismo modelo desde Xbox One), mantenidos activamente por la comunidad (`xbox-webapi-python` activo en 2026). Riesgo: cambios en el consentimiento de Entra (ya hubo `AADSTS`/scope issues en 2023-2024).
- Endpoints de tienda (displaycatalog, sigls): estables pero sin contrato; IDs de lista de Game Pass pueden rotar.

## Comparación con la integración actual de Steam

| Aspecto | Steam (actual) | Xbox |
|---------|----------------|------|
| Login | OpenID 2.0, sin secretos del usuario, devuelve SteamID64 | OAuth2 Entra + XSTS; **refresh token por usuario**; menores no soportados |
| Credenciales de la app | 1 API key global (`Steam:ApiKey`) | 1 Azure app (client id + secret con expiración) + token de cada usuario |
| Biblioteca | `GetOwnedGames` = **poseídos** | Solo **jugados** (titlehub); sin poseídos |
| Horas | `playtime_forever` confiable, 1 llamada | `MinutesPlayed` por título, cobertura parcial |
| Metadatos/portadas | IGDB por SteamAppId (`external_games`) | IGDB por Store ProductId (a validar) + displaycatalog como fuente propia |
| API oficial | Sí (Steam Web API) | No para terceros; endpoints reverse-engineered |
| ToS | API key permitida | Zona gris |
| Rate limit | ~100k/día | 100/15 s por servicio (directo) o 150-5.000/h (OpenXBL) |
| Privacidad perfil | Perfil público requerido | Depende de la configuración de privacidad Xbox; token del propio usuario ve sus datos |

El patrón existente (`SteamService` singleton con `HttpClient`, `SteamPlaytimeSyncManager`, worker de sync) se puede replicar (`XboxService` + `XboxSyncManager` + worker), pero hay que agregar **almacenamiento cifrado de tokens** y una tabla nueva (siguiente script numerado + SPs de insert/update), algo que Steam no necesitaba.

## Esfuerzo estimado si se implementara (S/M/L) y riesgos

| Pieza | Esfuerzo | Notas |
|-------|----------|-------|
| Spike de validación (IGDB `uid` Xbox, cobertura, MinutesPlayed real de la cuenta del dueño) | **S** | Imprescindible antes de comprometer alcance |
| Catálogo Game Pass + metadatos/portadas/screenshots oficiales vía displaycatalog (sin login) | **S-M** | Mapear por nombre/productId; badge "en Game Pass"; independiente de login |
| Login Microsoft/Xbox (OAuth + XSTS + almacenamiento y refresh de tokens) | **M** | Backend como cliente confidencial; callback análogo al de Steam (state + `return_to` binding ya trabajado en v1.0) |
| Import de "jugados" + mapeo IGDB + horas + progreso de logros | **M-L** | Mucho código de mapeo/no-match y UI para juegos sin IGDB |
| Capturas del usuario (mediahub) | **S-M** | Verificar caducidad de URLs; decidir si se almacenan |
| Opción OpenXBL en lugar de XSTS directo | **-M** | Reduce esfuerzo de auth, añade dependencia y costo |
| **Total completo** | **L** | |

**Riesgos principales:** (1) ToS zona gris con el XSTS del usuario; (2) cambios sin aviso en endpoints no documentados; (3) biblioteca incompleta (sin poseídos ni juegos sin integración Xbox Live) puede decepcionar frente a Steam; (4) mapeo IGDB incierto hasta el spike; (5) custodia de refresh tokens (nuevo secreto de usuario); (6) cuentas de menores y regiones sin Xbox no funcionan.

**Recomendación para un milestone futuro:** empezar por el lado **sin login** (catálogo Game Pass + displaycatalog + etiqueta de disponibilidad) que da valor con riesgo mínimo; luego, si se quiere importar historial, usar la cuenta del dueño con XSTS directo (librería propia en C#) y no OpenXBL, y tratar horas/logros como datos opcionales "mejor esfuerzo".

## Fuentes (URLs)

**Oficiales (Microsoft) — confianza ALTA**
- Xbox services sign-in for title websites (flujo Entra/XSTS): https://learn.microsoft.com/en-us/gaming/gdk/docs/services/fundamentals/s2s-auth-calls/service-authentication/live-website-authentication
- Fine-Grained Rate Limiting (límites publicados): https://learn.microsoft.com/en-us/gaming/gdk/docs/services/develop/best-practices/live-fine-grained-rate-limiting
- userstats `GET /users/xuid({xuid})/scids/{scid}/stats`: https://learn.microsoft.com/en-us/gaming/gdk/docs/reference/live/rest/uri/userstats/uri-usersxuidscidsscidstatsget
- Xbox Player Data overview (stats definidos por título, Creators Program sin logros): https://learn.microsoft.com/en-us/gaming/gdk/docs/services/player-data/live-playerdata-overview
- Authenticating your service with the Microsoft Store APIs (Collections solo para titles propios vía Partner Center): https://learn.microsoft.com/en-us/gaming/gdk/docs/store/commerce/service-to-service/xstore-authenticating-your-service
- Microsoft Q&A, scope `XboxLive.SignIn` y MBI_SSL solo con partnership: https://learn.microsoft.com/en-us/answers/questions/1251517/where-is-the-xboxlive-scope-in-azure-ad-app
- Microsoft Q&A, no se ve el permiso en el portal / Partner Center: https://learn.microsoft.com/en-gb/answers/questions/1416915/how-to-get-azure-ad-xbox-live-api-access
- Microsoft Services Agreement: https://www.microsoft.com/en-us/servicesagreement (leído vía resumen automático; MEDIA)
- IGDB API docs, `external_games` / `external_game_sources`: https://api-docs.igdb.com/#external-game

**No oficiales / comunidad — confianza MEDIA (código fuente leído directamente)**
- xbox-webapi-python (repo): https://github.com/OpenXbox/xbox-webapi-python
  - titlehub: https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/api/provider/titlehub/__init__.py
  - userstats: https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/api/provider/userstats/__init__.py
  - achievements: https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/api/provider/achievements/__init__.py
  - mediahub: https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/api/provider/mediahub/__init__.py
  - screenshots: https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/api/provider/screenshots/__init__.py
  - catalog (displaycatalog): https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/api/provider/catalog/__init__.py
  - auth manager (scopes, URLs): https://github.com/OpenXbox/xbox-webapi-python/blob/master/xbox/webapi/authentication/manager.py
- OpenXBL docs / OpenAPI: https://api.xbl.io y https://api.xbl.io/docs/json
- OpenXBL precios: https://xbl.io/pricing
- OpenXBL términos: https://xbl.io/agreement
- XSTS error codes (XErr): https://minecraft.wiki/w/Microsoft_authentication
- Playtime vía userstats batch (PR comunitario, caveats offline): https://github.com/xodus-gaming/xodus/pull/222

**Verificado por llamada directa hoy (2026-10-27) — confianza ALTA para existencia/forma**
- https://catalog.gamepass.com/sigls/v2?id=fdd9e2a7-0fee-49f6-ad69-4354098401ff&language=en-US&market=US
- https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=9NBLGGH537BL&market=US&languages=en-US&fieldsTemplate=Details

**Gaps / no verificado:** valor real de `uid` y cobertura de las fuentes Microsoft/Xbox en IGDB; cobertura real de `MinutesPlayed` por título; caducidad de URLs de capturas de mediahub; vigencia de refresh tokens de cuentas personales; texto literal y numeración de cláusulas del Services Agreement; funcionamiento del flujo end-to-end con cuenta real (no probado en esta investigación).
