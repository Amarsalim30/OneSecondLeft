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
- Added geometric overlap death fail-safe in `ObstacleSpawner` (`geometry_overlap`) to prevent wall bypass from missed trigger callbacks.
- Gameplay feel improved with near-miss combo feedback, speed feedback, and danger-intensity wall visuals.
- Added a creative obstacle archetype: oscillating/moving-gap walls with deterministic behavior under seeded runs.
- Touch slow-time activation is now stabilized at run start: touch slow input is briefly locked and can optionally require a post-start touch release before meter drain.
- Player movement touch input now also uses a run-start lock/release gate to prevent unstable carry-over touches at run start.
- Speed pressure ramp is now non-linear (safe -> tense -> intense -> dangerous overtime) with a 15s signature speed escalation.
- Gap width now shrinks over controlled runtime duration to raise pressure without unfair spikes.
- Presentation layer now adds slow-mo vignette, near-miss burst particles, signature tint shift + bass cue, and death freeze/shake/shatter feedback.
- Restart pacing is now faster by default to keep retry flow sharp.
- Added title identity/start layer: one-scene title card with game name, best score, and tap-to-start framing.
- Added player-facing run mode controls (Random vs Daily Challenge) in HUD/title flow.
- Death summary now supports deliberate restart flow (`PLAY AGAIN` button + keyboard restart) so share interactions are not interrupted by accidental instant restart.
- Added continuous ambient hum audio layer with runtime intensity shaping during run/signature states.
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
- `DONE` User-facing mode toggle (Daily Challenge vs Random) is implemented.
- Remaining: leaderboard backend hookup.
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

## 8) External Playtest Feedback (Added 2026-02-28)
Summary:
- Current state is solvable and fun but too easy and visually dull.
- This is a polishing/tuning phase, not a genre pivot.
- Core mechanic works; raise pressure and improve tension signaling.

Implementation status (2026-02-28):
- `DONE` Non-linear pressure ramp + controlled gap shrink are implemented in `ObstacleSpawner`.
- `DONE` Visual identity pass and slow-mo/near-miss/death presentation are implemented in `GameplayPresentationController` + camera palette updates.
- `DONE` Signature escalation moment at 15s (tint + hum + speed jump) is implemented.
- `DONE` Death feel polish (freeze, shake, shatter, faster restart) is implemented.

Hard constraints:
- Do not pivot genre.
- Do not scope explode.
- Do not add unrelated progression systems during this pass.
- Focus on tuning, pacing, and feedback quality.

### Step 1 - Fix "Too Easy" Without Unfairness
1. Non-linear speed ramp:
- Use non-linear scaling so runs move from safe -> tense -> intense.
- Desired pacing bands:
- `0-5s`: slow introduction.
- `5-10s`: noticeable increase.
- `10-20s`: aggressive pressure.
- `20s+`: dangerous.
- Example reference formula:
- `speed = baseSpeed + Mathf.Pow(timeAlive, 1.3f) * rampFactor;`

2. Controlled gap shrink over time:
- Narrow gap width progressively, deterministically.
- Example reference:
- `gapWidth = Mathf.Lerp(maxGap, minGap, timeAlive / 30f);`
- No random spikes; pressure should feel authored and fair.

3. Strong near-miss reward loop:
- Detect very close passes and reward skill expression with:
- brief edge flash,
- sharp audio cue,
- small score bonus.

### Step 2 - Fix Visual Dullness With Minimal Scope
Visual identity direction:
- Black background.
- One accent color (cyan or magenta).
- White walls.
- Bright/glowy player read.

Add only lightweight polish:
- subtle player trail,
- slight vignette while slow-mo is active,
- small near-miss particle burst.

### Step 3 - Add One Signature Escalation Moment
At `timeAlive > 15s`:
- slight screen tint shift,
- subtle bass hum layer,
- small speed jump.

Goal:
- Introduce a memorable emotional escalation point in each strong run.

### Step 4 - Make Death Feel Sharp and Satisfying
Death presentation target:
- `0.15s` freeze frame,
- small camera shake,
- quick shatter-style feedback burst,
- fast restart flow.

Goal:
- Death should feel impactful and fair, not flat or disappointing.

### Explicit "Do Not Add More Systems" Note for This Pass
Do not introduce during this tuning phase:
- skins,
- shards/currency systems,
- upgrades/meta progression,
- additional new obstacle families.

### Tuning Principle
- Addictiveness is likely blocked by parameter tuning, not idea volume.
- Small numeric changes should be prioritized and measured:
- speed curve,
- gap envelope,
- early-run ramp softness,
- near-miss feedback intensity.

### Required Metric to Drive Next Balance Pass
- Capture and track current average run duration baseline.
- Segment target question for next tuning decision:
- Is average run duration near `5s`, `15s`, or `30s+`?
- Latest reported baseline: average run duration is around `10s`.

## 9) Product Completeness Guidance (Added 2026-02-28)
Framing:
- Current game is a working single-screen loop, which is valid for core prototype stage.
- "Complete" does not require many screens; it requires structure, identity, progression, and closure.
- Keep scope tight and polish-forward.

### Four Layers Needed to Feel Complete
1. Title identity layer (first ~5 seconds before play):
- Minimal title state.
- Game name.
- "Tap to Start".
- Best score visible.
- Keep style simple: black background + logo + subtle motion.

2. Run structure layer:
- Rhythm target in one scene:
- `Idle -> Start animation -> Build tension -> Peak -> Death -> Score reveal -> Restart`.
- Goal is pacing/closure, not extra mechanics.

3. Minimal progression layer:
- Persistent best score.
- Strong "New Best!" celebration.
- Optional lightweight unlocks only (for example 3 color themes by score milestones).
- No upgrade economy for this phase.

4. Audio identity layer:
- Subtle continuous hum bed.
- Distinct slow-mo cue.
- Distinct near-miss cue.
- Clean death impact cue.

Implementation status (2026-02-28):
- `DONE` Title identity layer is implemented (game title, best score, tap-to-start).
- `DONE` Run structure now explicitly supports idle start, active run, death summary, and deliberate restart.
- `DONE` Minimal progression signal is implemented via persistent best score + "NEW BEST" state feedback.
- `DONE` Audio identity layer now includes continuous hum bed plus distinct slow/near-miss/death/signature cues.

### Explicitly Out of Scope
Do not add for this pass:
- shop,
- settings-heavy UI,
- multi-level architecture,
- story/character systems,
- full tutorial flow.

### Death Flow Target (Tight)
Target death timeline:
- `0.00s`: sharp white flash.
- `~0.10s`: freeze frame.
- immediate small camera shake.
- simple burst/shatter particles.
- `~0.20s`: score snap and best highlight.
- `~0.40s`: ready to restart.
- Total target: `< 0.6s`.

### Visual Readability Principle
- Prefer clarity over spectacle.
- Strip distracting noise; keep obstacles highly readable.
- Baseline style guidance:
- strong contrast,
- subtle player trail,
- slight vignette,
- simple glow accents.
