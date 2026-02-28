# Analytics Instrumentation Taxonomy

Last updated: 2026-02-28
Owner: Gameplay + Data
Status: IMPLEMENTED (local sink active, remote SDK optional)

## Canonical Event Envelope
All events include:
- `event_name` (snake_case)
- `event_time_utc`
- `session_id`
- `run_id` (nullable before first run)
- `build_sha`
- `platform`
- `device_model`
- `os_version`

## Event Catalog Aligned to Current Runtime Signals
| Event | Trigger in code | Required properties |
|---|---|---|
| `app_boot_started` | `BootLoader.Start()` begin | `scene_target` |
| `game_scene_loaded` | after async load activation in `BootLoader.Start()` | `load_duration_ms` |
| `run_start` | `GameManager.StartRun()` | `restart_delay_seconds`, `run_mode`, `run_seed`, `run_index` |
| `run_end` | `GameManager.KillPlayerWithCause(string)` | `duration_seconds`, `score`, `best_score`, `new_best`, `death_cause` |
| `run_auto_restarted` | `GameManager.StartRun()` after queued restart | `time_since_death_ms` |
| `death_cause` | `GameManager.KillPlayerWithCause(string)` | `death_cause`, `run_mode`, `run_seed` |
| `slow_enter` | `TimeAbility.SetSlowActive(bool)` enter transition | `remaining_slow_seconds`, `max_slow_seconds` |
| `slow_exit` | `TimeAbility.SetSlowActive(bool)` exit transition | `remaining_slow_seconds`, `max_slow_seconds` |
| `slow_meter_depleted` | `TimeAbility.Update()` when remaining reaches 0 | `remaining_slow_seconds`, `max_slow_seconds` |
| `near_miss` | `ObstacleSpawner.MoveAndRecycleWalls()` when `wall.WasNearMiss` | `near_miss_distance`, `streak`, `multiplier`, `score` |
| `combo_state_changed` | `ScoreManager.AddNearMissBonus()` / combo reset path | `near_miss_streak`, `multiplier`, `reason` (`gain`,`decay`) |
| `speed_tier_changed` | when crossing FLOW/RUSH/BLAZE/HYPER thresholds from `ObstacleSpawner.CurrentSpeed` | `speed`, `tier` |
| `new_best_reached` | first transition to `ScoreManager.IsCurrentRunNewBest == true` | `score`, `previous_best` |
| `pause_focus_normalized` | `GameManager.OnApplicationPause/OnApplicationFocus` | `reason` (`pause`,`focus`), `time_scale_after` |

## Death Source Enum
Use:
- `collision_enter`
- `collision_stay`
- `trigger_enter`
- `trigger_stay`
- `overlap_failsafe`
- `gap_miss`
- `geometry_overlap`
- `unknown`

Mapping:
- collision variants: `PlayerController` collision/trigger/failsafe handlers
- `gap_miss`: `ObstacleSpawner` pass check where player is outside safe gap
- `geometry_overlap`: `ObstacleSpawner` collider-bounds overlap fail-safe against wall solids

## Implementation Notes
- Local analytics pipeline is implemented via `GameplayAnalytics` with `IGameplayAnalyticsSink` and a default debug-log sink.
- Remote SDK integration can be added by registering another sink at runtime.
- Emit events at state transitions only (not every frame).

## Validation Rules
- One `run_start` must pair with exactly one `run_end`.
- `run_auto_restarted` count should equal `run_start - 1` within a session after first death.
- `new_best_reached` max once per run.
- Events must never allocate per-frame garbage in hot loops.
