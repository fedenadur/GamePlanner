# Epic Games Store — Viabilidad de integración

**Investigado:** 2026-10-27 | **Alcance:** solo documentación (sin cambios de código) | **Milestone:** v1.1 Cleanup + Platform Analysis

## Resumen

**Veredicto: PARCIAL.** Con la vía **oficial** (Epic Online Services / Epic Account Services) solo se puede hacer "Login with Epic" para obtener identidad básica (account id, display name). No existe ningún endpoint oficial ni scope para leer la biblioteca/entitlements de un usuario, ni horas jugadas, y además los términos de EOS limitan su uso a "video games and related applications" (GamePlanner está en zona gris).

Biblioteca, horas jugadas y metadatos **sí son técnicamente obtenibles** por la vía **no oficial** que usan Legendary, Heroic y Playnite (OAuth con el client id del launcher de Epic + `library-service` / `catalog-service`). Es la misma ruta que usa Playnite desde hace años, pero es ingeniería inversa sin documentación estable, puede romperse sin aviso y tiene riesgo de ToS. Portadas y screenshots deben seguir viniendo de IGDB; el mapeo Epic→IGDB es posible pero indirecto (uid = offer id, no el id de la biblioteca).

Recomendación: no integrar Epic como "el login de Steam". Si se hace, que sea como import opt-in "best effort" (usuario pega un authorization code), claramente marcado como no oficial; alternativa de bajo riesgo: carga manual (ya planificada: playtime manual).

## Tabla de capacidades

| Capacidad | ¿Posible? | Vía | Oficial / No oficial | Confianza |
|-----------|-----------|-----|----------------------|-----------|
| Login / vincular cuenta Epic | Sí (solo identidad) | Epic Account Services (OAuth2/OIDC, scope `basic_profile`) vía Developer Portal; o code manual con client del launcher | Oficial (EAS) / No oficial (launcher) | Media (oficial: la doc del Portal es JS-only y no pude leerla directamente; ver Fuentes) |
| Biblioteca (juegos poseídos) | Sí, solo no oficial | `library-service .../library/api/public/items` con token del launcher; opcional `catalog` para títulos | No oficial | Alta (que funciona: código de Playnite) / Media (estabilidad) |
| Metadatos (título, descripción, developer, fecha) | Parcial | Catalog service (`bulk/items`) y store GraphQL/`store-content` (no oficiales); mejor: IGDB | No oficial (Epic) / Oficial (IGDB) | Media-Alta |
| Fotos / screenshots | Parcial | Epic: `keyImages` del catálogo/store (arte de tienda, no screenshots del usuario); screenshots reales: IGDB | No oficial (Epic) / Oficial (IGDB) | Media |
| Portadas (cover art) | Sí | IGDB (ya integrado). Epic `keyImages` como fallback | Oficial (IGDB) | Alta |
| Horas jugadas | Sí, solo no oficial | `GET .../library/api/public/playtime/account/{accountId}/all` → `artifactId` + `totalTime` | No oficial | Media (endpoint real y usado por Playnite, pero sin doc pública estable) |

## Autenticación / login

### Vía oficial: Epic Account Services (EAS) / "Login with Epic"

- EAS es parte de Epic Online Services (EOS). Expone OAuth 2.0 / OIDC: issuer `https://api.epicgames.dev/epic/oauth/v2`, authorization endpoint `https://www.epicgames.com/id/authorize`, scope por defecto `basic_profile` (fuente: guía de Ory y AccelByte; Media).
- **Requiere registrarse en el Developer Portal**: crear Organization → Product → configurar la "Application" de Epic Account Services, activar permisos (Basic Profile, Friends, Online Presence…) y vincular Client(s). AccelByte indica que además hace falta un Domain verificado en la Organization. Un foro de Unreal reporta `scope_not_found` si falta enviar el cuestionario de age rating o permitir el scope en la client policy (Media/Baja: relato de un usuario).
- Scopes documentados en terceros: `basic_profile`, `friends_list`, `presence`, `country`. **No encontré ningún scope de biblioteca/entitlements/ownership/playtime** (ausencia de evidencia; Media).
- Quién puede obtener credenciales: cualquier cuenta Epic puede entrar al Developer Portal y crear un producto; la licencia dice "Commercial, non-commercial, educational, and personal use" pero también **"Epic Online Services is not for applications not related to video games"**. El Developer Agreement define *Video Game* como "interactive video game software and related applications developed by you … for entertainment purposes", y el AUP resume "only use in connection to your video game". Un tracker de backlog podría interpretarse como "related application", pero **no es seguro**; habría que confirmar con Epic (Baja-Media).
- Lo que da: `accountId` + display name (+ amigos/presence si se aprueban). Sirve para "vincular cuenta", no para importar nada.

### Vía no oficial: flujo del Epic Games Launcher (Legendary / Playnite / Heroic)

1. El usuario inicia sesión en `https://www.epicgames.com/id/login` y abre `https://www.epicgames.com/id/api/redirect?clientId=<launcher client id>&responseType=code`, que devuelve un JSON con `authorizationCode` (Playnite lo hace con webview; Legendary lo pide por consola; `legendary auth --code/--token/--sid` admite valores pre-obtenidos).
2. El backend intercambia el code en `https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token` (`grant_type=authorization_code`, `token_type=eg1`) con **Basic auth = client id/secret públicos del propio launcher** (están en el código fuente de Playnite/Legendary), y un User-Agent `EpicGamesLauncher`.
3. Se obtienen `access_token`, `refresh_token`, `account_id`; el refresh token **rota en cada uso** (hay que persistir el nuevo atómicamente; lo advierte el issue de ludarium).
4. Cada request de datos usa `Authorization: bearer <access_token>`.

Es decir: no hay client credentials propias; GamePlanner se haría pasar por el launcher. Nada de esto está documentado ni soportado por Epic.

## Endpoints / fuentes de datos (por capacidad)

> Solo se listan endpoints vistos en código real (Playnite `EpicAccountClient.cs`, egs-api, issues de proyectos que los consumen). No hay documentación oficial; pueden cambiar.

| Capacidad | Endpoint | Notas |
|-----------|----------|-------|
| Biblioteca | `GET https://library-service.live.use1a.on.epicgames.com/library/api/public/items?includeMetadata=true&platform=Windows` (+ `&cursor=`) | Paginado por `responseMetadata.nextCursor`. Devuelve `records[]` con `namespace`, `catalogItemId`, `appName` (artifactId), `sandboxType`, `buildVersion`. Hay que filtrar DLC, assets de Unreal (`namespace == "ue"`), extras digitales, etc. (Playnite lo hace en `EpicLibrary.cs`). |
| Metadatos | `GET https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/{ns}/bulk/items?id={catalogItemId}&country=US&locale=en-US&includeMainGameDetails=true` | Devuelve `title`, `description`, `developer`, `keyImages[]`, `categories`, `customAttributes`. |
| Búsqueda/ficha de tienda | `POST https://store.epicgames.com/graphql` (`searchStore`) y `GET https://store-content.ak.epicgames.com/api/en-US/content/products/{productSlug}` | Usados por Playnite para metadata; tampoco oficiales. |
| Horas jugadas | `GET https://library-service.live.use1a.on.epicgames.com/library/api/public/playtime/account/{accountId}/all` | Lista `{accountId, artifactId, totalTime}`; `totalTime` en **segundos**. Se cruza con la biblioteca por `artifactId == appName`. Un PR de otro proyecto confirma el campo y advierte que no tiene doc pública estable. |
| Ofertas (para IGDB) | `GET .../catalog/api/shared/namespace/{ns}/offers?status=SUNSET\|ACTIVE` | Paginado de a 100; una llamada por namespace (un caso medido: 79 s para 562 juegos). |
| Screenshots / covers | IGDB `screenshots`, `covers` (ya en `IgdbService`) | Epic `keyImages` es arte de tienda (portada vertical/horizontal), no capturas del jugador. |
| Instalados (alternativa local) | Manifests del launcher en disco (Playnite los lee) | Solo juegos instalados y solo si la API corre en la misma máquina del launcher. |

Oficial EOS: existen Web APIs/SDK (Auth, Connect, Ecom, Friends, Presence…), pero el **Ecom/ownership oficial está acotado a los items del propio producto** del desarrollador; no pude confirmar en la doc oficial que no pueda consultar otros juegos, pero ningún scope ni cliente conocido lo permite (Media). El único listado de "owned" de terceros que se ve en la comunidad pasa por el library-service no oficial.

Horas jugadas: la UI del launcher muestra "You've Played"/columna Time Played en vista de lista (ayuda oficial de Epic), pero **no hay API pública**; el dato lo expone el endpoint no oficial de arriba. Baja la fiabilidad de que Epic mantenga el endpoint estable.

## Mapeo con IGDB

- **IGDB soporta Epic**:
  - `external_games`: valor de enum `epic_game_store = 26` (campo `category`) y `external_game_source` (nuevo). IGDB tiene una migración "Enums to Tables" (`external_game.category` → `external_game_source`; endpoint nuevo `external_game_sources`) con periodo de migración del 18-feb al 31-ago; en la protobuf `EXTERNALGAME_EPIC_GAME_STORE = 26` figura `deprecated`. **Usar `external_game_source` + `uid` y no `category`** en cualquier implementación futura.
  - `websites`: `category` 16 = `epicgames` (también migrando a `website_types`, campo `type`). Las URLs de tienda de Epic en `websites` sirven como pista secundaria.
- **Problema de claves**: el `uid` de IGDB para Epic es el **offer id** de la tienda (hex de 32 caracteres según desarrolladores que lo usan), y **ningún campo del registro de biblioteca (`namespace`, `catalogItemId`, `appName`) coincide con ese uid** (issue ludarium #74). Camino viable: `namespace` → `.../namespace/{ns}/offers` (ofertas `BASE_GAME`) → offer id → consulta a IGDB `external_games where external_game_source = 26 & uid = "..."`. Un proyecto midió **83% de aciertos únicos, 1% a dos juegos, 16% sin match** sobre 562 juegos (Media; una sola muestra).
- Namespaces de publishers contienen muchos juegos, y hay namespaces con más de 100 ofertas (paginar). Fallback: búsqueda IGDB por nombre (con fuzzy match) y confirmación manual.
- El `IgdbService.cs` actual no consulta `external_games` (sin coincidencias en el archivo): hoy se enlaza por `IGDBId`/`SteamAppId`. Epic requeriría una consulta nueva a `external_games` y una columna tipo `EpicCatalogItemId`/`EpicNamespace` en `Games`.

## Limitaciones, rate limits y Términos de servicio

- **Rate limits**: no hay cifras publicadas para los endpoints no oficiales ni encontré las oficiales de EOS Web API (No verificado). Tratar como desconocido: caché agresivo, sync poco frecuente, backoff ante 429.
- **Estabilidad (Media-Baja)**: sin contrato; el refresh token rota; Heroic tuvo un hotfix de emergencia por un cambio en los logins de Epic (2022). Los desarrolladores que lo consumen dicen que "can break without notice and nobody owes us a fix". Legendary sigue publicado (repo con actividad visible, GPL-3.0); Playnite mantiene el plugin con "full library import via account login".
- **ToS / riesgo legal (no es asesoría legal)**:
  - Epic Games Terms of Service (actualizados 10-sep-2026) prohíben "reverse engineer, modify, decompile … Licensed Products" y usar los productos "beyond the scope of their intended use". Usar el client id/secret del launcher y simular su User-Agent es, como mínimo, una zona gris.
  - Los Gameplay Integrity Tools apuntan a cheats/fraude *en juego*; no encontré reportes de baneos de cuentas por usar Legendary/Playnite/Heroic solo para listar biblioteca (ausencia de evidencia, no garantía).
  - Uso personal/local reduce la exposición pero no la elimina; guardar refresh tokens de terceros añade responsabilidad de seguridad (cifrar en reposo, nunca loguear).
  - Vía oficial EOS: ToS más limpia pero uso restringido a videojuegos y sin datos útiles de biblioteca.
- **Datos sensibles**: authorization code/refresh token dan acceso amplio a la cuenta Epic; cifrar (p. ej. Data Protection API) y permitir desvincular.

## Comparación con la integración actual de Steam

| Aspecto | Steam (actual) | Epic |
|---------|----------------|------|
| Login | Steam OpenID 2.0, sin registro; verifica identidad | Oficial: EAS (OAuth2) requiere Developer Portal + producto; no oficial: code pegado manualmente |
| Credenciales de la app | Una Web API key propia (`Steam:ApiKey`) | Oficial: client id/secret del producto; no oficial: client del launcher (no es nuestro) |
| Biblioteca | `IPlayerService/GetOwnedGames` con key (perfil público) | Solo no oficial: `library-service` con token del usuario |
| Horas jugadas | `playtime_forever` en el mismo endpoint | Solo no oficial: `playtime/account/{id}/all` |
| Metadatos/portadas/screenshots | IGDB por `SteamAppId` (external_games source 1) | IGDB por offer id (source 26) con mapeo indirecto, ~83% |
| Documentación / estabilidad | Documentada y estable | Sin documentación; puede romperse |
| Autenticación por usuario | No necesaria (SteamID público) | Necesaria (token del usuario, refresh rotativo, hay que guardarlo cifrado) |
| ToS | API pública con key permitida | Oficial: límites de uso; no oficial: riesgo de ToS |

El `SteamService` actual (key en header/config, `GetOwnedGamesAsync`) no es reutilizable como patrón: Epic exige estado por usuario (tokens) además de estar sin contrato.

## Esfuerzo estimado si se implementara (S/M/L) y riesgos

| Alcance | Esfuerzo | Notas |
|---------|----------|-------|
| Solo "Login with Epic" oficial (identidad) | **M** | Registro en Developer Portal, dominio verificado, flujo OAuth, mapear `accountId`; valor bajo (no trae datos) y ToS dudosa |
| Import no oficial: code manual + biblioteca + playtime | **L** | Intercambio de code, almacenamiento cifrado de tokens rotativos, paginación, filtros DLC/UE, tabla de enlaces Epic↔IGDB, cola de re-matching manual, tests con fixtures grabados |
| Mapeo Epic→IGDB (offers + `external_game_source`) | **M** | Una llamada por namespace; ~16% sin match requiere UI de resolución manual |
| Alternativa mínima: carga manual (juegos + horas) | **S** | Ya está planificada la carga manual de playtime; sin riesgo de ToS |

Riesgos principales: (1) rotura silenciosa del API, (2) ToS/cuenta Epic del usuario, (3) custodia de tokens, (4) matching incompleto con IGDB, (5) restricción de uso en EOS. Mitigaciones: feature opt-in con aviso, errores de Epic aislados del resto de syncs, no marcar juegos como eliminados con listas parciales, fixtures grabados en tests (nunca el servicio vivo en CI).

## Fuentes (URLs)

Oficiales / primarias:
- Epic Online Services — Licensing (uso permitido, "not for applications not related to video games"): https://onlineservices.epicgames.com/licensing
- EOS Developer Agreement (definición de *Video Game*): https://onlineservices.epicgames.com/services/terms/agreements
- EOS Acceptable Use Policy: https://onlineservices.epicgames.com/services/terms/aup
- EOS overview (llms.txt): https://onlineservices.epicgames.com/llms.txt
- Player authentication with Epic Account Services (EAS): https://onlineservices.epicgames.com/news/player-authentication-with-epic-account-services-eas
- Querying for Epic Friends (permisos EAS): https://onlineservices.epicgames.com/news/querying-for-epic-friends-and-their-status
- Epic Games Terms of Service: https://legal.epicgames.com/epicgames/tos
- Ayuda de Epic — playtime en el launcher: https://www.epicgames.com/help/en-US/c-Category_EpicGamesStore/c-EpicGamesStore_LauncherSupport/how-to-check-your-play-time-in-the-epic-games-launcher-a000087738
- IGDB API docs (external_games enum 26, websites `epicgames` 16, migración Enums to Tables): https://api-docs.igdb.com/
- IGDB protobuf (deprecated flags): https://api.igdb.com/v4/igdbapi.proto

Secundarias / código comunitario (no oficial):
- Playnite `EpicAccountClient.cs` (OAuth del launcher, library items, playtime, catalog): https://github.com/JosefNemec/PlayniteExtensions/blob/master/source/Libraries/EpicLibrary/Services/EpicAccountClient.cs
- Playnite `WebStoreClient.cs` (graphql/store-content): https://github.com/JosefNemec/PlayniteExtensions/blob/master/source/Libraries/EpicLibrary/Services/WebStoreClient.cs
- Legendary: https://github.com/legendary-gl/legendary
- egs-api (cliente Rust, endpoints): https://docs.rs/crate/egs-api/0.9.0
- ludarium #64 (provider Epic, advertencias): https://github.com/Halaburda00/ludarium/issues/64
- ludarium #74 (uid IGDB = offer id, 83% de match): https://github.com/Halaburda00/ludarium/issues/74
- Ludex PR #31 (library/playtime sin doc pública estable): https://github.com/Gabriel-Liz2003/Ludex/pull/31
- Gist de endpoints de Epic: https://gist.github.com/Amrsatrio/20d2174583354ae4f0a24cf63764049f
- Ory (issuer/endpoints OAuth de Epic): https://www.ory.com/docs/integrates-with/social-sign-in/epic-games
- AccelByte (requisitos del Developer Portal/dominio): https://docs.accelbyte.io/gaming-services/services/access/authentication/epic-identity
- Foro Unreal `scope_not_found`: https://forums.unrealengine.com/t/eos-auth-login-fails-with-scope-not-found-on-basic-profile-despite-application-permissions-being-enabled/2754164
- Heroic hotfix logins Epic (2022): https://www.gamingonlinux.com/2022/09/heroic-games-launcher-gets-an-emergency-hotfix-for-epic-games-store-logins

Huecos: la documentación oficial en `dev.epicgames.com/docs` es JS-only y no se pudo leer; la lista de scopes y el límite de Ecom/ownership oficial provienen de fuentes secundarias. Rate limits no verificados.
