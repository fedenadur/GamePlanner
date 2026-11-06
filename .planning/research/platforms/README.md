# Integración de otras plataformas — Comparativa

**Fecha:** 2026-10-27 · **Milestone:** v1.1 · **Tipo:** solo análisis (sin código)

Análisis de si GamePlanner puede integrar otras tiendas como hoy integra Steam (login OpenID + `GetOwnedGames` con API key + metadatos/imágenes desde IGDB).

| Plataforma | Documento | Veredicto |
|---|---|---|
| GOG | [GOG.md](GOG.md) | Parcial |
| Epic Games Store | [EPIC.md](EPIC.md) | Parcial |
| Xbox / Game Pass | [XBOX.md](XBOX.md) | Parcial |

## Comparativa por capacidad

| Capacidad | Steam (actual) | GOG | Epic | Xbox / Game Pass |
|---|---|---|---|---|
| Login | ✅ OpenID oficial | ⚠️ No oficial (`client_id` de Galaxy, el usuario pega el código) | ⚠️ Oficial solo identidad (EOS); no oficial para todo lo demás | ✅ OAuth Microsoft documentado + XSTS (refresh token por usuario) |
| Biblioteca | ✅ Juegos poseídos | ⚠️ No oficial (`embed.gog.com/user/data/games`, solo IDs) | ⚠️ No oficial (`library-service` del launcher) | ⚠️ Solo **jugados** (titlehub); no hay API de poseídos |
| Metadatos | IGDB | ✅ Sin auth (`api.gog.com/products`, `catalog.gog.com`) | ⚠️ No oficial (`catalog-public-service`) / IGDB | ✅ Sin auth (`displaycatalog`) |
| Portadas / screenshots | IGDB | ✅ Sin auth (catálogo GOG) / IGDB | IGDB (los `keyImages` de Epic son arte de tienda) | ✅ Sin auth (Poster, BoxArt, screenshots 4K); capturas del usuario vía mediahub |
| Horas jugadas | ✅ `playtime_forever` | ❌ Sin endpoint confiable | ⚠️ No oficial (`playtime/account/{id}/all`) | ⚠️ `MinutesPlayed` solo si el juego lo reporta |
| Logros | — | — | — | ✅ `achievements.xboxlive.com` |
| Mapeo IGDB | `SteamAppId` | `external_game_source` GOG (falta confirmar `uid`) | `external_game_source` Epic (`uid` = offer id, mapeo indirecto, ~83% match) | `external_game_source` Microsoft/Xbox (falta confirmar `uid`) |
| Riesgo ToS | Ninguno | Alto para login/biblioteca (prohíbe ingeniería inversa) | Alto para la vía no oficial | Medio ("zona gris", uso personal de solo lectura) |
| Credenciales a guardar | Ninguna por usuario | `refresh_token` GOG | Refresh token del launcher (rota en cada uso) | Refresh token Microsoft |
| Esfuerzo | — | S (metadatos) / M (biblioteca) | L | L |

## Conclusiones

1. **Ninguna plataforma tiene un equivalente a `GetOwnedGames` de Steam.** Todas dependen de endpoints no oficiales para los datos de la cuenta del usuario.
2. **Epic y Xbox obligan a guardar un token por usuario**, algo que Steam no necesita. Eso agrega un tipo nuevo de credencial a proteger después del hardening de v1.0 (tabla nueva, cifrado, rotación).
3. **Xbox es la más completa**: login documentado, logros, historial de jugados y catálogo Game Pass sin login. Su límite es que solo sabe lo que jugaste, no lo que tenés.
4. **GOG sirve sobre todo para metadatos e imágenes** sin login, y para eso IGDB ya cubre la mayoría de los casos.
5. **Las horas de juegos que ninguna tienda sincroniza** (grupo familiar, juegos fuera de Steam) quedan cubiertas por la carga manual (PLAY-01 de v1.1).

## Recomendación propuesta (a confirmar en PLAT-04)

- **Siguiente candidata: Xbox**, empezando por un spike sobre la cuenta del dueño: verificar el `uid` de IGDB, la cobertura real de `MinutesPlayed` y la vida de los refresh tokens.
- **Epic y GOG**: no integrar el login. Como mucho, un import opt-in "best effort" marcado como no oficial, si algún día hace falta.
- Mientras tanto, la carga manual de horas y de juegos resuelve el caso práctico sin riesgo.

## Validaciones pendientes antes de implementar

- IGDB `external_game_sources`: IDs actuales y semántica del `uid` para GOG, Epic y Xbox (requiere credenciales IGDB).
- Xbox: cobertura de `MinutesPlayed`, expiración de URLs de mediahub, duración de refresh tokens, estabilidad de los `siglId` de Game Pass.
- GOG: si el `client_id` de Galaxy acepta un `redirect_uri` propio; respuesta real de `galaxy-library`. El intento de probar `/token` con el secret de Galaxy fue bloqueado por permisos y queda como decisión del dueño.
- Rate limits reales (solo hay datos de segunda mano para GOG y Epic).
