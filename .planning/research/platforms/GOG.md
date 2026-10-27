# GOG — Viabilidad de integración

**Investigado:** 2026-10-27 | **Modo:** Feasibility | **Alcance:** solo documentación, sin cambios de código.

## Resumen

**Veredicto: PARCIAL.** GOG **no ofrece una API pública oficial** para terceros: no hay API key, ni registro de clientes OAuth, ni documentación de "owned games" (un hilo del foro oficial de agosto 2025 sigue pidiéndola y no hay respuesta de staff; las peticiones se remontan a 2013). Todo lo que sirve para esta integración es **no oficial / ingeniería inversa** de los endpoints que usan el sitio web y el cliente GOG Galaxy.

Lo que sí es viable con riesgo bajo: **metadatos, portadas y screenshots** (`api.gog.com/products` y `catalog.gog.com` responden sin autenticación; verificado en vivo hoy) y el **mapeo a IGDB**. Lo que es viable pero frágil y con riesgo de ToS: **login + biblioteca** (reutiliza el `client_id` público del cliente Galaxy; requiere flujo "pegar el código", no hay redirect propio). **Horas jugadas**: no hay endpoint confirmado; solo vía el perfil público del usuario (scraping, frágil) o la base local de Galaxy. A diferencia de Steam, el esfuerzo y el riesgo de mantenimiento son altos para la parte de usuario.

## Tabla de capacidades

| Capacidad | ¿Posible? | Vía | Oficial / No oficial | Confianza |
|-----------|-----------|-----|----------------------|-----------|
| Login / vincular cuenta | Parcial | OAuth2 code flow contra `auth.gog.com` con el `client_id` público de Galaxy; el usuario pega el `code` (el `redirect_uri` fijo es de GOG). Alternativa sin login: pedir el nombre de usuario del perfil público | No oficial (cliente prestado) | Media |
| Biblioteca (juegos poseídos) | Parcial | `GET https://embed.gog.com/user/data/games` (solo IDs, en `owned[]`) + `GET .../account/getFilteredProducts` (detalle); candidato: `galaxy-library.gog.com/users/{id}/releases`. Alternativa: `/u/{user}/games/stats` del perfil público | No oficial | Media (IDs/getFilteredProducts), Baja (galaxy-library y perfil público) |
| Metadatos | Sí | `GET https://api.gog.com/products/{id}?expand=description,screenshots,videos`, `GET https://api.gog.com/v2/games/{id}`, `GET https://catalog.gog.com/v1/catalog` (búsqueda) | No oficial en cuanto a documentación; públicos y sin auth (existe un uso oficial para afiliados) | Alta (probado en vivo), estabilidad Media |
| Fotos / screenshots | Sí | `expand=screenshots` en `api.gog.com/products` (`formatter_template_url` + `{formatter}`) y `screenshots[]` en el catálogo | No oficial | Alta |
| Portadas | Sí | `coverVertical`, `coverHorizontal`, `logo`, `galaxyBackgroundImage` en `catalog.gog.com/v1/catalog`; `images.*` en `api.gog.com/products` | No oficial | Alta |
| Horas jugadas | Casi no | No hay endpoint documentado ni confirmado. Posibles: `gameplay.gog.com/clients/{id}/users/{id}/sessions` (listado sin descripción en docs de la comunidad), perfil público `/u/{user}/games/stats`, o `galaxy-2.0.db` local | No oficial | Baja |

## Autenticación / login

**Qué existe de forma oficial.** El **GOG Galaxy SDK** (docs.gog.com/galaxyapi) y los *encrypted app tickets* (docs.gog.com/sdk-encrypted-tickets) están pensados para **desarrolladores de juegos** que integran su propio título con Galaxy, no para apps web que leen la biblioteca de un usuario. El **Galaxy Integrations Python API** (repo `gogcom/galaxy-integrations-python-api`) es oficial pero sirve para escribir plugins que **corren dentro del cliente Galaxy 2.1 local** y exponen `authenticate`, `get_owned_games`, `get_game_time` hacia el cliente; no es una API REST consumible desde un backend. **No hay portal de desarrolladores para registrar clientes OAuth**: un hilo del foro y la documentación comunitaria confirman que solo se puede usar el cliente del propio Galaxy, y que no hay cuentas de developer ni API keys para no-partners.

**Flujo no oficial (documentado por la comunidad, Yepoleb/gogapidocs).**
1. Redirigir al usuario a `https://auth.gog.com/auth?client_id=46899977096215655&redirect_uri=https://embed.gog.com/on_login_success?origin=client&response_type=code&layout=client2`.
2. Tras el login, el navegador termina en el `redirect_uri` con `?code=...`. Como ese dominio es de GOG, **GamePlanner no puede recibir el redirect**: el usuario tendría que copiar el `code` de la URL y pegarlo en la app (es el diseño que contempla el issue #128 de Ludarium).
3. Intercambio: `GET https://auth.gog.com/token` con `client_id`, `client_secret`, `grant_type=authorization_code`, `code`, `redirect_uri` -> `access_token` (expira ~3600 s), `refresh_token`, `user_id`.
4. Renovación con `grant_type=refresh_token`. Uso: `Authorization: Bearer <access_token>`.

- El `client_id` (`46899977096215655`) y el `client_secret` son los **públicos del cliente Galaxy** (están embebidos en cada instalación). No se pueden registrar credenciales propias. No hay scopes (`scope` vacío).
- Verificado en vivo (2026-10-27): `https://auth.gog.com/auth?client_id=46899977096215655&...` redirige a `https://login.gog.com/auth?...` con ese mismo `client_id`, o sea el flujo sigue activo. **No probé el endpoint `/token`** (usaría el secret prestado). Que un `redirect_uri` propio (p. ej. `localhost`) sea aceptado: **no verificado**, y probablemente no (el ejemplo `localhost:8000` de la documentación comunitaria es de 2016-2018).
- Alternativa de sesión: cookies de `www.gog.com`/`embed.gog.com`; requieren pasar el login web (puede haber CAPTCHA). No recomendable.
- Verificado: `https://embed.gog.com/user/data/games` sin credenciales responde 302 a la home (`/en##openlogin`); es decir, **la biblioteca exige token o sesión**.
- **Alternativa "estilo Steam" sin login:** el perfil público `https://www.gog.com/u/{username}`; un proyecto de terceros (trophyhunter, PR #35) lee la biblioteca de `/u/<username>/games/stats` sin token y datos de `window.profilesData.profileUser`. Depende de que el usuario tenga el perfil/estadísticas públicos (en GOG la visibilidad se configura, y hubo confusión sobre el default). Confianza baja: formato (JSON o HTML) no confirmado.
- **Riesgo de credenciales:** guardar el `refresh_token` de GOG de un usuario equivale a guardar acceso a su cuenta de tienda (con métodos de pago y wishlist). Choca con la postura de v1.0 (hardening). Habría que cifrarlo en reposo y limitar su alcance; no hay scopes que lo mitiguen.

**Quién puede obtener credenciales:** nadie fuera de GOG/partners. La vía comunitaria sugerida en el foro es un proyecto FOSS que se convierta en partner oficial; no hay evidencia de que exista el programa.

## Endpoints / fuentes de datos (por capacidad)

### Biblioteca (requiere token; no oficial)
- `GET https://embed.gog.com/user/data/games` -> `{ "owned": [ids...] }`. Incluye juegos, **películas, DLC y bundles**; filtrar luego por `game_type`. (gogapidocs `account.html`, foro.)
- `GET https://embed.gog.com/account/getFilteredProducts?mediaType=1&page=N` -> detalle paginado (título, slug, imágenes). Mencionado por la documentación comunitaria y el foro; esa documentación no describe sus parámetros, **no lo probé**.
- `GET https://galaxy-library.gog.com/users/{user_id}/releases` -> candidato a listar releases con fecha de adquisición y tipo; **sin documentar y sin verificar** (solo aparece como pregunta abierta en el issue #128 de Ludarium). Confianza baja.
- `GET https://embed.gog.com/userData.json` -> `userId`, `username`, `email`, país. Útil para saber el `user_id` de la sesión.
- `GET https://users.gog.com/users/{user_id}` -> perfil (username, avatar).

### Metadatos (públicos; probados el 2026-10-27, HTTP 200 sin auth)
- `GET https://api.gog.com/products/{id}?expand=description,screenshots,videos` -> `title`, `slug`, `release_date`, `game_type`, `description`, `languages`, `links.product_card`, `dlcs`, `images`, `screenshots`, `videos`. Máx. 50 IDs con `GET /products?ids=a,b,c`.
- `GET https://api.gog.com/v2/games/{id}?locale=en-US` (usado por gogdb.org como API de tienda v2).
- `GET https://catalog.gog.com/v1/catalog?query=like:<texto>&limit=N` -> `id`, `slug`, `title`, `releaseDate`, `productType`, `developers`, `publishers`, `genres`, `tags`, `reviewsRating`, `storeLink`, `screenshots[]`, precios por país.
- Con el `id` numérico (el mismo que devuelve `user/data/games`) se obtiene todo lo anterior.

### Screenshots y portadas
- Catálogo: `coverVertical` (póster tipo caja), `coverHorizontal`, `logo`, `galaxyBackgroundImage` (URLs directas en `images.gog-statics.com`).
- `api.gog.com/products?expand=screenshots`: cada screenshot trae `image_id` y `formatter_template_url` con `{formatter}`; el JSON ya incluye `formatted_images` (`ggvgt`, `ggvgm`, `ggvgl` y variantes `_2x`).
- Los `images.*` de `api.gog.com/products` vienen con `//` sin esquema, a normalizar a `https:`.
- Una vez que el juego esté enlazado a IGDB, **IGDB sigue siendo la fuente primaria de covers/screenshots** (ya existe en `GameImageManager`); GOG solo es fallback.

### Horas jugadas
- No existe un endpoint oficial. `gameplay.gog.com/clients/{product_id}/users/{user_id}/sessions` aparece listado sin descripción en la documentación comunitaria; un issue de Heroic describe un POST de sesión (`.../games/{id}/users/{id}/sessions`) como formato reportado, no un contrato. Si GOG expone un total acumulado, **no lo he podido confirmar**.
- El issue #167 del repo oficial `galaxy-integrations-python-api` indica que el playtime en la nube es un único total que se **sobrescribe** entre dispositivos y que **no hay API para leerlo desde fuera**; el equipo no tiene fecha ni asignado.
- Opciones realistas, ambas frágiles: (a) perfil público `/u/{user}/games/stats` (estadísticas visibles solo si el usuario las hace públicas); (b) leer la base SQLite local de Galaxy (`galaxy-2.0.db`, workaround mencionado en un comentario de 2022 del issue #167, sin verificar), viable solo porque GamePlanner corre en el PC del usuario.
- Como GamePlanner ya planea **entrada manual de playtime** en v1.1, esa es la vía de menor riesgo para GOG.

## Mapeo con IGDB

Verificado contra la documentación oficial de IGDB (api-docs.igdb.com, HTML descargado hoy):
- Endpoint `https://api.igdb.com/v4/external_games` con campos `uid`, `url`, `name`, `external_game_source`, `game`, `platform`, `year`, `countries`.
- `category` está marcado **DEPRECATED, usar `external_game_source`**. Valores del enum viejo `category`: `steam = 1`, **`gog = 5`**, `epic` no figura en el fragmento que leí, etc.
- Los IDs de `external_game_source` se consultan en `https://api.igdb.com/v4/external_game_sources` (campos `name`, `created_at`...). Un paquete comunitario (omnigame/igdb) asume 1 = Steam, **5 = GOG**, 26 = Epic, 30 = itch.io, pero el propio README admite que no lo probó en vivo. **Hay que confirmarlo con una consulta real a `external_game_sources`** antes de implementar.
- `websites`: el campo `category` también está **DEPRECATED, usar `type`** (-> `website_types`). En el enum viejo `gog = 17`. Se puede usar como respaldo para obtener la URL de la tienda GOG de un juego.
- Consultas candidatas (a validar en vivo):
  - GOG -> IGDB: `fields game,uid,url,name; where external_game_source = 5 & uid = "<gog_product_id>";`
  - IGDB -> GOG: `fields uid,url; where game = <igdbId> & external_game_source = 5;`
- **No verifiqué qué contiene `uid` para GOG** (id numérico de producto vs. slug); el `url` típicamente trae el slug de `gog.com/game/<slug>`. Si el `uid` fuese el slug, hay que resolver el id con `api.gog.com/products?...` o `catalog.gog.com` (búsqueda por título).
- Cobertura: IGDB solo enlaza juegos que GOG vende y que alguien catalogó; juegos retirados, bundles, DLC y películas pueden no tener entrada. Fallback: coincidencia por título normalizado + año de lanzamiento (`releaseDate`) contra `games` de IGDB, igual que el match de nombre ya existente.
- A diferencia de Steam, el `SteamAppId` ya sirve de ancla de enlace; para GOG habría que añadir una columna equivalente (`GogProductId`). Fuera de alcance aquí.

## Limitaciones, rate limits y Términos de servicio

**Rate limits (no oficial, sin garantía):**
- `api.gog.com/products/*`: **200 requests/hora/IP** según un post del foro que cita la página de soporte del programa de afiliados; "sin límite" en `V1/games` y `V2/games` según la misma cita. No pude abrir esa página de soporte (403), así que el dato es de segunda mano y su fecha es desconocida. Los comentarios del foro afirman que enviar un token de Galaxy evita el límite (no verificado).
- Los endpoints de usuario (`embed.gog.com`, `galaxy-library`, `gameplay`): sin límites documentados. Asumir un tope bajo y cachear.
- IGDB: 4 requests/s (documentación oficial de IGDB), sin cambios respecto a lo existente.

**Términos de servicio (GOG User Agreement, vigente desde 9 marzo 2026, copia en tostracker.app):**
- §11.1(c): sin permiso previo de GOG no se debe modificar, traducir, hacer *reverse engineering* ni descompilar el software/servicios; GOG dice que podría "abrir protocolos de cliente" en el futuro para evitar esa necesidad.
- §11.1(e): prohíbe bots, scripts, herramientas de extracción y programas que "recopilen información" sobre los servicios de GOG.
- §11.1(f): prohíbe emulación de protocolos y acceder a zonas de GOG.COM/GALAXY/servidores "que no han sido puestas a disposición".
- §3.3 / §11.1(h): la cuenta es personal; no compartir credenciales.
- Interpretación (no es asesoría legal): usar el `client_id` del cliente Galaxy desde un backend propio y consumir endpoints no documentados **encaja mal con §11.1(c)/(e)/(f)**. Riesgo práctico: bloqueo/limitación de la cuenta del usuario. Para uso estrictamente personal y local el riesgo real es bajo, pero **el riesgo de ToS es real y no hay vía oficial que lo elimine**. Los endpoints públicos de catálogo (`catalog.gog.com`, `api.gog.com/products`) son los que usa el propio sitio para anónimos; son los de menor riesgo, aun así sin contrato.
- El programa de afiliados (email a affiliate@gog.com) incluye acceso oficial a un product feed, pero solo cubre datos de tienda (catálogo/precios), no bibliotecas de usuario, y GOG dice que no acepta a todos.

**Estabilidad:**
- La documentación comunitaria de referencia (gogapidocs, © 2016, Sphinx 1.7.4) es antigua y se declara "muy joven"; confirmé en vivo que `auth.gog.com`, `api.gog.com/products` y `catalog.gog.com` siguen funcionando, pero nadie garantiza que se mantengan. Un usuario del foro (agosto 2025) los describe como "unsupported, no guarantee that it'll keep working".
- `V1/games` figura como deprecado en bases comunitarias; usar V2/`catalog`.
- Cloudflare protege `support.gog.com` y puede desafiar a clientes sin navegador (me devolvió un desafío "Just a moment" al descargar el User Agreement).

## Comparación con la integración actual de Steam

| Aspecto | Steam (actual) | GOG |
|---------|----------------|-----|
| API oficial | Sí (`api.steampowered.com`) | No |
| Credenciales de app | API key propia (`Steam:ApiKey`) | No se pueden obtener; solo `client_id`/`secret` públicos de Galaxy |
| Login | Steam OpenID oficial, redirect a nuestro dominio | OAuth no oficial; el redirect es de GOG, el usuario debe pegar el `code` |
| Biblioteca | `IPlayerService/GetOwnedGames` por `steamid`, perfil público | `user/data/games` (IDs) + `getFilteredProducts`, requiere token del usuario |
| Horas jugadas | `playtime_forever` (minutos) en la misma llamada | Sin endpoint confirmado; perfil público o DB local |
| Metadatos/imágenes | Steam + IGDB | `api.gog.com` / `catalog.gog.com` + IGDB |
| Identificador de enlace | `SteamAppId` | `GogProductId` (columna nueva) |
| Almacenar secretos del usuario | Solo SteamId (público) | Habría que guardar un `refresh_token` de cuenta de tienda |
| ToS | Cubierto por los términos de la Web API | Zona gris/contraria (ingeniería inversa, extracción) |
| Estabilidad | Alta | Media-baja, sin soporte |

## Esfuerzo estimado si se implementara (S/M/L) y riesgos

| Componente | Esfuerzo | Riesgo principal |
|------------|----------|------------------|
| Metadatos + portadas + screenshots GOG por ID (fallback a IGDB) | **S** | Rate limit 200/h/IP; endpoints no contractuales |
| Mapeo GOG<->IGDB vía `external_games` + columna `GogProductId` + fallback por título/año | **S-M** | Confirmar `external_game_source` de GOG y qué es `uid`; cobertura incompleta |
| Login "pegar el código" + intercambio/refresh de tokens | **M** | UX torpe, `refresh_token` sensible, posible cambio del flujo o bloqueo del `client_id` |
| Importar biblioteca (`user/data/games` + detalle, filtrar DLC/películas) | **M** | `getFilteredProducts` y `galaxy-library` sin documentar; quiebres silenciosos |
| Horas jugadas automáticas | **L / no recomendado** | Sin endpoint confirmado; scraping de perfil o DB local |
| Alternativa "solo usuario público" (sin login, perfil `/u/{user}`) | **M** | Perfiles privados por defecto/variable; HTML cambiante |

**Recomendación por fases (si se decide avanzar):**
1. Primero la parte de bajo riesgo: metadatos/imágenes públicos + mapeo IGDB + importación manual/CSV de la biblioteca y playtime manual (ya planeado en v1.1).
2. Después, si se acepta el riesgo de ToS, una importación de biblioteca de una sola vez con login "pegar código", sin persistir el `refresh_token` (solo el `access_token` en memoria durante la importación).
3. Playtime automático de GOG: diferir; no hay base estable.

**Antes de implementar, validar en vivo (no lo hice aquí):** (a) `GET external_game_sources` y una consulta `external_games` de GOG con credenciales IGDB; (b) respuesta real de `getFilteredProducts` y `galaxy-library` con un token propio; (c) si `/u/{user}/games/stats` devuelve JSON y con qué campos de playtime; (d) si el `client_id` de Galaxy acepta un `redirect_uri` propio.

## Fuentes (URLs)

Oficiales / primarias:
- IGDB API docs, endpoints `external_games`, `external_game_sources`, `websites`, `website_types`, rate limit 4 req/s: https://api-docs.igdb.com/ (HTML descargado 2026-10-27; la herramienta WebFetch devolvía 403, usé `curl`) — Confianza ALTA
- GOG Galaxy SDK (para desarrolladores de juegos): https://docs.gog.com/galaxyapi — ALTA
- GOG Galaxy SDK, encrypted tickets: https://docs.gog.com/sdk-encrypted-tickets/ — ALTA
- Galaxy Integrations Python API (oficial, plugins locales de Galaxy 2.1): https://github.com/gogcom/galaxy-integrations-python-api — ALTA
- Issue #167 (playtime en la nube, sin API de lectura): https://github.com/gogcom/galaxy-integrations-python-api/issues/167 — ALTA para "no hay API", sin respuesta oficial
- GOG User Agreement (copia con fecha de vigencia 9-mar-2026): https://tostracker.app/document/gog (original: https://support.gog.com/hc/en-us/articles/212632089-GOG-COM-User-Agreement) — MEDIA (copia de terceros; no pude abrir el original por Cloudflare)
- Programa de afiliados GOG: https://support.gog.com/hc/en-us/articles/4405004689297-How-to-join-the-GOG-Affiliate-Program — BAJA (403, solo vía resumen de búsqueda)

Verificaciones en vivo del 2026-10-27 (curl): `api.gog.com/products/1207658924?expand=screenshots,description,videos` (200), `catalog.gog.com/v1/catalog?query=like:portal&limit=1` (200), `auth.gog.com/auth?...` (302 a `login.gog.com` con el `client_id` de Galaxy), `embed.gog.com/user/data/games` sin auth (302 a login) — ALTA.

No oficiales / comunidad:
- Documentación comunitaria de la API de GOG (Yepoleb/gogapidocs): https://gogapidocs.readthedocs.io/en/latest/ ; auth: https://gogapidocs.readthedocs.io/en/latest/auth.html ; cuenta: https://gogapidocs.readthedocs.io/en/latest/account.html ; Galaxy: https://gogapidocs.readthedocs.io/en/latest/galaxy.html — MEDIA (antigua, © 2016)
- Hilo del foro sobre falta de API oficial (agosto 2025): https://www.gog.com/forum/general/gog_official_api_only/post7 — MEDIA
- Foro "Unofficial GOG API documentation" (credenciales para no-partners, límite 200/h): https://gog.com/forum/general/unofficial_gog_api_documentation/page6 y https://gog.com/forum/general/unofficial_gog_api_documentation/page14 — BAJA/MEDIA
- Ludarium issue #128 (preguntas abiertas sobre `galaxy-library`, `getFilteredProducts`, `gameplay`): https://github.com/Halaburda00/ludarium/issues/128 — BAJA
- Trophyhunter PR #35 (biblioteca pública `/u/<user>/games/stats`): https://github.com/tristan-spinnewyn/trophyhunter/pull/35 — BAJA
- Heroic issue #1240 (formato de sesiones en `gameplay.gog.com`): https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/issues/1240 — BAJA
- GOG DB, lista de endpoints usados: https://www.gogdb.org/moreinfo — MEDIA
- omnigame/igdb (supuestos de `external_game_source` 1/5/26/30, no probados en vivo): https://packagist.org/packages/omnigame/igdb — BAJA
- Comet (cliente alternativo que usa el mismo `client_id`): https://github.com/quantenzitrone/comet — BAJA
