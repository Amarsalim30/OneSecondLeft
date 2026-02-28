# Unity Review - OneSecondLeft

## 1) Summary
- Runtime bootstrapping is currently the single point of failure; if bootstrap preconditions are not met, the game can load with UI/background only.
- Input design is conflicted: the same pointer press both drags player and activates slow-mo, so movement consumes the full slow meter.
- Core loop progression is tightly coupled to obstacle pool initialization; one missing reference can freeze gameplay progression.
- Mobile lifecycle handling is incomplete (`pause/resume` + `timeScale`), which can produce mobile-only stuck/slow states.
- Scene data and runtime mutations are inconsistent (camera/game objects are configured at runtime, not serialized reliably).
- Collision/death logic is overly broad (any trigger/collision kills), increasing accidental deaths as content grows.
- Editor scaffolding has hidden side effects (auto scene/build setting generation), increasing config drift risk.
- There are no playmode tests or deterministic hooks for validating loop/start/restart behavior.

## 2) Top 10 Critical Issues
1. ISSUE-004 - Movement input and slow-mo share the same press stream.
2. ISSUE-001 - Bootstrap short-circuits on `GameManager` existence without validating dependencies.
3. ISSUE-002 - Spawner initialization failure halts both obstacle spawning and score progression.
4. ISSUE-007 - No explicit mobile pause/resume handling for `timeScale` while app backgrounding.
5. ISSUE-005 - Player death triggers on any collision/trigger, no layer/tag filtering.
6. ISSUE-012 - Runtime camera mutation hides scene misconfiguration (serialized scene is not authoritative).
7. ISSUE-003 - `Boot -> Game` load is synchronous and can appear hung on slower devices.
8. ISSUE-008 - HUD is hardcoded and not safe-area aware on mobile.
9. ISSUE-014 - Editor auto-scaffold mutates scenes/build settings on editor load.
10. ISSUE-006 - Core loop nondeterminism (frame-time and RNG) blocks reliable regression testing.

## 3) Full Issue List Grouped by Category

### Crash/Freeze

### ISSUE-001 - Bootstrap short-circuits on partial scene state
- Severity: Critical
- Evidence: `Assets/_Game/Scripts/Bootstrapper.cs` - `OnSceneLoaded`
```csharp
if (Object.FindFirstObjectByType<GameManager>() != null)
{
    return;
}
```
- Repro steps:
1. Add a `GameManager` manually in `Game.unity` (without wiring all runtime dependencies).
2. Launch app.
3. Observe missing systems (no spawner/score/hud links) and stalled gameplay.
- Root cause hypothesis: bootstrap checks only one sentinel (`GameManager`) and assumes the full graph exists.
- Fix proposal: validate full required graph (`PlayerController`, `TimeAbility`, `ObstacleSpawner`, `UIHud`) before returning; repair missing nodes.
- Acceptance criteria: with only `GameManager` present in scene, app still auto-creates/repairs missing systems and run starts.
- Effort: S
- Risk of fix: Medium

### ISSUE-002 - Gameplay loop depends on pool init success
- Severity: Critical
- Evidence: `Assets/_Game/Scripts/ObstacleSpawner.cs` - `Update`
```csharp
TryInitializePool();
if (pool == null || activeWalls == null)
{
    return;
}
```
- Repro steps:
1. Break obstacle template assignment (or fail template creation).
2. Run on device.
3. Observe score and obstacles both frozen (static HUD state).
- Root cause hypothesis: score progression (`AddDistance`) is inside the same update path that bails if pool is null.
- Fix proposal: decouple score/time progression from spawner availability and emit an explicit fatal diagnostic when pool cannot initialize.
- Acceptance criteria: if pool init fails, error is visible and score/timer behavior is explicitly handled (not silent freeze).
- Effort: S
- Risk of fix: Low

### ISSUE-003 - Synchronous scene load in boot path
- Severity: Major
- Evidence: `Assets/_Game/Scripts/BootLoader.cs` - `Start`
```csharp
if (!SceneManager.GetSceneByName(gameSceneName).isLoaded)
{
    SceneManager.LoadScene(gameSceneName);
}
```
- Repro steps:
1. Install on lower-end mobile.
2. Launch app from cold start.
3. Observe apparent hang/blank period during blocking scene load.
- Root cause hypothesis: blocking `LoadScene` on main thread with no transitional state.
- Fix proposal: use `LoadSceneAsync` with simple loading state.
- Acceptance criteria: startup remains responsive and does not appear frozen during scene transition.
- Effort: S
- Risk of fix: Low

### Gameplay correctness

### ISSUE-004 - Drag movement always consumes slow-mo
- Severity: Blocker
- Evidence: `Assets/_Game/Scripts/PlayerController.cs` - `TryGetPointerScreenPosition`, and `Assets/_Game/Scripts/TimeAbility.cs` - `IsHoldActive`
```csharp
if (touch != null && touch.primaryTouch.press.isPressed) { ... } // movement
if (touch != null && touch.primaryTouch.press.isPressed) { return true; } // slow-mo
```
- Repro steps:
1. Start run.
2. Drag player left/right to dodge.
3. Observe slow meter draining while simply moving.
- Root cause hypothesis: one pointer state drives two independent mechanics with no gesture separation.
- Fix proposal: split inputs (e.g., left half drag = move, right half hold = slow; or second-finger hold for slow).
- Acceptance criteria: player can move at normal speed without spending slow meter.
- Effort: M
- Risk of fix: Medium

### ISSUE-005 - Any trigger/collision causes death
- Severity: Critical
- Evidence: `Assets/_Game/Scripts/PlayerController.cs` - `OnCollisionEnter2D`, `OnTriggerEnter2D`
```csharp
private void OnCollisionEnter2D(Collision2D _) { NotifyCollisionDeath(); }
private void OnTriggerEnter2D(Collider2D _) { NotifyCollisionDeath(); }
```
- Repro steps:
1. Add any trigger collider in scene (debug/helper object).
2. Run and intersect.
3. Player dies even if object is non-lethal.
- Root cause hypothesis: no tag/layer filtering for lethal obstacles.
- Fix proposal: restrict death checks to a dedicated layer/tag (e.g., `Obstacle`), or explicit component marker.
- Acceptance criteria: non-obstacle triggers do not kill player.
- Effort: S
- Risk of fix: Low

### ISSUE-006 - Nondeterministic loop progression
- Severity: Major
- Evidence: `Assets/_Game/Scripts/ObstacleSpawner.cs` - `Update`, `SpawnWall`
```csharp
float dt = Time.deltaTime;
runElapsedSeconds += dt;
float center = Random.Range(-centerLimit, centerLimit);
```
- Repro steps:
1. Run on two devices with different frame rates.
2. Compare score/time progression and obstacle timing.
3. Behavior diverges.
- Root cause hypothesis: frame-time integration + unseeded RNG without deterministic test mode.
- Fix proposal: optional deterministic mode with fixed-step simulation and seeded RNG for test runs.
- Acceptance criteria: deterministic mode reproduces identical obstacle/score timeline across runs.
- Effort: M
- Risk of fix: Medium

### Mobile-specific

### ISSUE-007 - No pause/resume policy for timeScale on mobile
- Severity: Critical
- Evidence: `Assets/_Game/Scripts/GameManager.cs` (`OnDisable` only) and `ProjectSettings/ProjectSettings.asset`
```csharp
private void OnDisable()
{
    timeAbility?.ForceNormalTime();
}
```
```yaml
runInBackground: 0
```
- Repro steps:
1. Activate slow-mo.
2. Background app (home button), then resume.
3. ASSUMPTION: some devices can resume with stale slow state/feel.
- Root cause hypothesis: no `OnApplicationPause/OnApplicationFocus` lifecycle normalization path.
- Fix proposal: handle pause/focus callbacks in `GameManager` or `TimeAbility` to force normalize time and input state.
- Acceptance criteria: after resume, `Time.timeScale == 1f` unless user is actively re-holding slow input.
- Effort: S
- Risk of fix: Low

### ISSUE-008 - HUD ignores safe area and uses hardcoded coordinates
- Severity: Major
- Evidence: `Assets/_Game/Scripts/HudFactory.cs` - `Create`
```csharp
scaler.referenceResolution = new Vector2(1080f, 1920f);
bgRect.anchoredPosition = new Vector2(0f, -88f);
new Vector2(-70f, -66f)
```
- Repro steps:
1. Run on notched/rounded-corner devices.
2. Observe top labels/meter overlap status/notch zones.
- Root cause hypothesis: no safe-area container or padding calculations.
- Fix proposal: add a safe-area root `RectTransform` and anchor HUD elements inside it.
- Acceptance criteria: all HUD elements remain visible across common aspect ratios and notch devices.
- Effort: M
- Risk of fix: Medium

### Performance/GC

### ISSUE-009 - Pool allows duplicate release corruption
- Severity: Major
- Evidence: `Assets/_Game/Scripts/Pool.cs` - `Release`
```csharp
instance.transform.SetParent(parent, false);
instance.gameObject.SetActive(false);
available.Push(instance);
```
- Repro steps:
1. ASSUMPTION: a bug calls `Release` twice on same instance.
2. Pool returns same object multiple times in future `TryGet`.
3. State corruption/overlap occurs.
- Root cause hypothesis: no in-pool membership guard.
- Fix proposal: track active/inactive membership (`HashSet<int>` or flag component) and ignore/log duplicate release.
- Acceptance criteria: duplicate release attempts are safely rejected and logged once.
- Effort: S
- Risk of fix: Low

### ISSUE-010 - Unnecessary repeated timeScale writes
- Severity: Minor
- Evidence: `Assets/_Game/Scripts/TimeAbility.cs` - `SetSlowActive`
```csharp
if (slowActive == active)
{
    ApplyTimeScale(active);
    return;
}
```
- Repro steps:
1. Hold or release input for prolonged periods.
2. Observe `ApplyTimeScale` called each frame for unchanged state.
- Root cause hypothesis: method writes global timing values even when no transition happened.
- Fix proposal: early-return without applying when state unchanged.
- Acceptance criteria: `Time.timeScale/fixedDeltaTime` are written only on state transitions.
- Effort: S
- Risk of fix: Low

### UI/UX logic bugs

### ISSUE-011 - Score UX appears frozen at run start due integer rounding display
- Severity: Minor
- Evidence: `Assets/_Game/Scripts/UIHud.cs` - `RefreshScore`
```csharp
int scoreValue = Mathf.RoundToInt(scoreManager.CurrentScore);
scoreLabel.text = scoreValue.ToString();
```
- Repro steps:
1. Start run.
2. Watch score during first moments.
3. Score can remain `0` while game is actually progressing.
- Root cause hypothesis: display quantization hides sub-1 progress.
- Fix proposal: show one decimal or time-based score format early (`0.0`), then switch to int if desired.
- Acceptance criteria: player sees immediate progression feedback after run starts.
- Effort: S
- Risk of fix: Low

### Architecture/Maintainability

### ISSUE-012 - Scene serialization and runtime config are inconsistent
- Severity: Major
- Evidence: `Assets/_Game/Scenes/Game.unity` + `Assets/_Game/Scripts/Bootstrapper.cs`
```yaml
orthographic: 0
orthographic size: 5
```
```csharp
camera.orthographic = true;
camera.orthographicSize = 6f;
```
- Repro steps:
1. Disable bootstrap (or fail initialization).
2. Run scene.
3. Camera behavior differs from intended gameplay camera.
- Root cause hypothesis: canonical camera setup is not stored in scene/prefab; it is patched at runtime.
- Fix proposal: serialize authoritative camera setup in scene/prefab and keep bootstrap minimal.
- Acceptance criteria: `Game.unity` camera matches runtime camera settings without bootstrap mutation.
- Effort: M
- Risk of fix: Medium

### ISSUE-013 - Singleton lifecycle is incomplete
- Severity: Major
- Evidence: `Assets/_Game/Scripts/GameManager.cs`
```csharp
public static GameManager Instance { get; private set; }
...
Instance = this;
```
- Repro steps:
1. ASSUMPTION: enable Enter Play Mode without domain reload or destroy/recreate manager flows.
2. Observe stale static references across sessions.
- Root cause hypothesis: no `OnDestroy` clearing `Instance`.
- Fix proposal: set `if (Instance == this) Instance = null;` in `OnDestroy`.
- Acceptance criteria: static instance is always valid or null after object destruction/reload.
- Effort: S
- Risk of fix: Low

### ISSUE-014 - Editor auto-scaffold mutates project state implicitly
- Severity: Major
- Evidence: `Assets/_Game/Scripts/Editor/MvpScaffoldGenerator.cs` - `AutoGenerateIfMissing`, `UpdateBuildSettings`
```csharp
[InitializeOnLoadMethod]
private static void AutoGenerateIfMissing()
...
EditorBuildSettings.scenes = new[]
```
- Repro steps:
1. Open project in editor.
2. Missing scenes trigger auto-generation and build settings rewrite.
3. Project state changes without explicit user action.
- Root cause hypothesis: startup side effects in editor code.
- Fix proposal: remove auto-run and keep generation explicit via menu command only.
- Acceptance criteria: opening the project does not modify scenes/build settings automatically.
- Effort: S
- Risk of fix: Low

### Build/Config

### ISSUE-015 - Game scene is not self-contained; build relies on runtime object synthesis
- Severity: Major
- Evidence: `Assets/_Game/Scenes/Game.unity` - roots only camera/light/volume
```yaml
SceneRoots:
  m_Roots:
  - {fileID: 330585546}
  - {fileID: 410087041}
  - {fileID: 832575519}
```
- Repro steps:
1. Disable/strip bootstrap script accidentally.
2. Build/run.
3. Scene loads without gameplay objects.
- Root cause hypothesis: no serialized gameplay root/prefab references in scene.
- Fix proposal: store a minimal serialized `GameRoot` prefab in scene and use bootstrap only as fallback.
- Acceptance criteria: `Game` scene runs with essential gameplay objects even if bootstrap fails.
- Effort: M
- Risk of fix: Medium

### Testing gaps

### ISSUE-016 - No automated playmode coverage and low testability hooks
- Severity: Major
- Evidence: `Assets/_Game/Scripts/GameManager.cs` - hard static/object lookups
```csharp
public static GameManager Instance { get; private set; }
...
playerController = FindFirstObjectByType<PlayerController>();
```
- Repro steps:
1. Search for gameplay tests (none present in repo).
2. Attempt isolated tests; hard static lookups and scene searches make deterministic tests difficult.
- Root cause hypothesis: runtime code is tightly coupled to scene and statics, with no injectable interfaces.
- Fix proposal: add minimal playmode smoke tests (`start/run/death/restart`, `slow-mo drain`), and expose small injectable seams (input/time providers in test mode).
- Acceptance criteria: CI-playable tests catch regression for startup, input, slow-mo drain, and restart loop.
- Effort: L
- Risk of fix: Medium

## Quick wins (<= 1 hour)
- Add `OnDestroy` nulling for `GameManager.Instance`.
- Guard `Pool.Release` against duplicate releases and log once.
- Stop writing `Time.timeScale/fixedDeltaTime` when slow state is unchanged.
- Add explicit bootstrap diagnostics (`Debug.LogError`) when required systems are missing.
- Add safe-area root in `HudFactory` and move top HUD under it.
- Remove `InitializeOnLoadMethod` auto-generation side effect from `MvpScaffoldGenerator`.

