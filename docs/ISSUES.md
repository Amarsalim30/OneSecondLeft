# OneSecondLeft - Ideas, Creativity, Issues, and Roadmap

Last updated: 2026-02-28

## 1) Product Direction
- Core fantasy: "survive impossible gaps at rising speed with clutch slow-time control."
- Product goal: short-session, highly replayable runner with strong shareable moments.
- Quality bar: responsive controls, deterministic-feeling deaths, high-clarity HUD, and visible mastery curve.

## 2) Creative/Viral Ideas
1. Signature Moments (high priority)
- "Near-Miss Chain" moments with escalating combo labels and visual pulse.
- "Hyper Speed" moments after risky play (temporary speed burst).
- "One-Second Save" clips when meter is almost empty and player still survives.

2. Share Hooks (high priority)
- End-run card with score, best, combo peak, and speed peak.
- One-tap "Share Replay Snapshot" image format (portrait-first social layout).
- Daily challenge seed so players compare exact same obstacle pattern.

3. Retention Mechanics (medium priority)
- Daily mission set (example: 3 near-misses in one run, survive 45s, hit 2x combo).
- Streak rewards: cosmetic trail, color packs, wall skins.
- "Beat your ghost" mode with prior-best lane trace.

4. Creativity Extensions (medium priority)
- Zone themes (Neon, Ice, Glitch, Sunset) with unique VFX/audio layers.
- Event walls: moving gaps, split-gap fakeouts, rhythm walls synced to music pulse.
- Risk-reward pickups: temporary wider gap but faster speed ramp.

## 3) Current Known Issues (Open)

Status legend:
- `PLAN_READY`: planning artifact exists and is linked.
- `EXECUTION_PENDING`: plan exists but validation/implementation work still needs to run.
- `DONE`: implemented in current codebase.
- `OPEN`: no complete implementation yet.
- `BLOCKED`: waiting on external dependency/secret/setup.

### P0 - Must Fix Before Production Claim
1. `PLAN_READY` + `EXECUTION_PENDING` Device validation coverage.
- Artifact: [Device Validation Matrix](./DEVICE_VALIDATION_MATRIX.md).
- Remaining work: execute matrix on physical devices and attach evidence.
2. `OPEN` + `BLOCKED` CI license setup incomplete.
- Artifact: [CI Setup](./CI_SETUP.md).
- Remaining blocker: Unity secrets (`UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE`) not confirmed configured.

### P1 - Should Fix for "Production Ready" Feel
1. `PLAN_READY` + `EXECUTION_PENDING` Formal balancing plan documented.
- Artifact: [Balancing Pass Plan](./BALANCING_PASS_PLAN.md).
- Remaining work: run tuning protocol and lock final release parameters.
2. `DONE` End-run summary/share UX implemented.
- Implemented: death summary card, run recap stats, local share export (text + screenshot).
- Runtime files: `UIHud`, `HudFactory`, `GameManager`, `ScoreManager`.
3. `DONE` Core analytics instrumentation implemented.
- Artifact: [Analytics Instrumentation Taxonomy](./ANALYTICS_TAXONOMY.md).
- Implemented events: `app_boot_started`, `game_scene_loaded`, `run_start`, `run_end`, `run_auto_restarted`, `death_cause`, `near_miss`, `combo_state_changed`, `speed_tier_changed`, `new_best_reached`, `slow_enter`, `slow_exit`, `slow_meter_depleted`, `pause_focus_normalized`.
- Remaining work: optional remote SDK sink hookup (local sink exists).

### P2 - Nice-to-Have
1. `PLAN_READY` Content pipeline for themes/skins documented.
- Artifact: [Content Pipeline: Themes and Skins](./CONTENT_PIPELINE_THEMES_SKINS.md).
- Remaining work: implement runtime selector and persistence.
2. `PLAN_READY` + `EXECUTION_PENDING` Accessibility/localization checklist documented.
- Artifact: [Accessibility and Localization Checklist](./ACCESSIBILITY_LOCALIZATION_CHECKLIST.md).
- Remaining work: execute checklist and externalize strings for localization.
3. `PLAN_READY` + `EXECUTION_PENDING` Low-end Android performance/battery budget documented.
- Artifact: [Performance and Battery Budget](./PERFORMANCE_BATTERY_BUDGET.md).
- Remaining work: run 3-trial protocol on A1/A2 devices and log results.

## 4) Recently Completed Technical Fixes
- Prior technical review issues (`ISSUE-001` to `ISSUE-016`) have code-level implementation in current tree.
- Collision reliability and lethal checks were hardened with trigger/stay/overlap fail-safes.
- Gameplay feel improved with near-miss combo feedback, speed feedback, and danger-intensity wall visuals.
- HUD now includes safer layout handling and richer state/score signaling.
- Added daily challenge seed context plumbing and live run-context labeling.
- Added end-run viral share loop with summary card export to `Application.persistentDataPath/RunShares`.
- Added gameplay analytics pipeline + default local debug sink.

## 5) Prioritized Execution Plan
1. Production Verification Sprint
- Run device matrix test pass (Android low/mid/high + desktop).
- Validate input split, pause/focus recovery, and death consistency.
- Turn playmode CI from template to enforced check.

2. Viral Feature Sprint
- Keep iterating end-run share card polish and social format variants.
- Add user-facing mode toggle (Daily Challenge vs Random) and leaderboard backend hookup.
- Expand clip-worthy stat surfacing (best near-miss streak, max speed tier, one-second-save moments).

3. Content and Retention Sprint
- Ship first cosmetic pack and streak rewards.
- Add daily missions.
- Add one new obstacle behavior archetype.

## 6) Definition of "High-Quality App" for This Project
- Player understands state instantly (run/slow/crash, combo, speed).
- Every loss feels fair and explainable.
- Controls are consistent across desktop and touch devices.
- Performance is stable during speed ramps and repeated restart loops.
- End of run gives clear motivation to retry or share.

## 7) Validation Checklist
1. Gameplay
- Collision/death cannot be bypassed.
- Near-miss detection is accurate and consistent.
- Difficulty ramp feels progressive, not spiky.
2. UX
- HUD readable on small screens and notched devices.
- Death/restart transition is clear and polished.
- Combo/speed feedback is exciting but not noisy.
3. Technical
- Playmode smoke tests pass locally and in CI.
- No null-reference spam in console during long session.
- Restart loop remains stable for 200+ cycles.
