# Content Pipeline: Themes and Skins

Last updated: 2026-02-28
Owner: Design + Gameplay
Status: PLAN_READY (runtime selector not implemented)

## Goal
- Ship repeatable visual variants (theme palettes + skins) without code churn each release.

## Content Units
- `HUD Theme`: colors and readability of state/meter/score/combo/speed UI.
- `Wall Skin`: obstacle color/gradient/danger response styling.
- `Player Skin`: player/tail color identity and contrast.

## Authoring Pipeline
1. Create a theme spec card:
- Name, palette (base/accent/warning/danger), contrast checks, screenshots.
2. Build asset variants:
- UI color preset (for `UIHud`/`HudFactory` values).
- Wall visual preset (for `ObstacleWall` colors/gradient/curve).
- Optional audio accent preset (`AudioManager` clip set).
3. Integrate into content catalog:
- Add metadata entry (`id`, `display_name`, `rarity`, `unlock_rule`, `version`).
4. Validate:
- Run [ACCESSIBILITY_LOCALIZATION_CHECKLIST.md](./ACCESSIBILITY_LOCALIZATION_CHECKLIST.md).
- Run safe area/orientation checks from [DEVICE_VALIDATION_MATRIX.md](./DEVICE_VALIDATION_MATRIX.md).
5. Approve and release:
- Mark content as `release_candidate` then `shipped`.

## Required Metadata Schema
| Field | Example | Notes |
|---|---|---|
| `id` | `theme_neon_01` | Stable key |
| `type` | `hud_theme` / `wall_skin` / `player_skin` | Content class |
| `display_name` | `Neon Pulse` | User-facing |
| `version` | `1` | Increment on balance/readability changes |
| `unlock_rule` | `streak_3_days` | Design-owned logic |
| `status` | `draft` / `rc` / `shipped` | Release stage |

## Quality Gates (Must Pass)
- Readability: score and state labels remain legible in all themes.
- Gameplay clarity: danger cues remain distinct from safe/neutral cues.
- Accessibility: no color-only critical signal without secondary cue.
- Performance: no extra per-frame allocations or expensive shaders on low-end Android.

## Current Blockers
- No runtime theme/skin selector UI yet.
- No persisted player selection plumbing yet.

