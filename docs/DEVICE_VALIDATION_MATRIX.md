# Device Validation Matrix

Last updated: 2026-02-28
Owner: Docs closure
Status: PLAN_READY (execution pending on physical devices)

## Scope
- Validate runtime behavior for pause/resume, orientation changes, and safe area layout in the `Game` scene.
- Target platforms: Android (low/mid/high + tablet) and Windows desktop sanity.

## Required Device Matrix
| ID | Class | Minimum profile | OS range | Required checks |
|---|---|---|---|---|
| A1 | Low-end phone | 2-3 GB RAM, 60 Hz, small screen | Android 8-10 | PR-01/02/03, OR-01/02, SA-01/02 |
| A2 | Mid-range phone | 4-6 GB RAM, 60-90 Hz | Android 11-13 | PR-01/02/03, OR-01/02, SA-01/02/03 |
| A3 | High-end phone | 8+ GB RAM, 90-120 Hz, notch/punch-hole | Android 13-15 | PR-01/02/03, OR-01/02/03, SA-01/02/03 |
| A4 | Android tablet | 10"+, 16:10 or 4:3 | Android 11-15 | PR-01/02, OR-01/03, SA-02/03 |
| D1 | Desktop sanity | Windows + mouse | N/A | PR-01, OR-00, SA-00 |

Notes:
- Record exact model, OS build, refresh rate, and aspect ratio in test evidence.
- `ProjectSettings.asset` currently allows autorotate for portrait and landscape; orientation tests are mandatory.

## Reproducible Checklist

## Test Setup
1. Install the exact APK under test; capture git SHA in the report.
2. Clear app data before first run on each device.
3. Enable OS auto-rotate.
4. Run each case at least 3 times per device.
5. Save one screenshot/video per failed run.

## Pause/Resume Cases
- `PR-01` Background/foreground during normal run:
Expected: no freeze, no stuck input, run continues or restarts cleanly, no console error spam.
- `PR-02` Background/foreground while slow-time is active:
Expected: gameplay resumes with normal time state (no stuck `Time.timeScale`, no slow-pitch latch).
- `PR-03` Lock/unlock for 30+ seconds mid-run:
Expected: return path is stable; no black screen, no duplicated HUD, no soft lock.

## Orientation Cases
- `OR-01` Portrait -> Landscape -> Portrait during active run:
Expected: HUD remains anchored in safe area; no clipped score/meter/state labels.
- `OR-02` Rotate on death overlay:
Expected: overlay fills screen and restart loop remains functional.
- `OR-03` Rapid rotation stress (5 rotations in 10 seconds):
Expected: no layout drift or progressive offset.

## Safe Area Cases
- `SA-01` Notch/cutout phone:
Expected: top HUD elements remain inside visible safe area bounds.
- `SA-02` Gesture navigation bar enabled:
Expected: bottom overlays do not overlap system gesture region.
- `SA-03` Very tall aspect ratio (>= 20:9):
Expected: labels remain readable and do not collide.

## Pass Criteria
- 0 blocking failures in PR/OR/SA cases on A1-A4.
- 0 reproducible layout clipping issues for key HUD elements (`meter`, `state`, `score`, `best`, `combo`, `speed`).
- 0 stuck-time or stuck-audio regressions after pause/focus transitions.

## Evidence Template
Use one row per device/case pair:

| Date | Build SHA | Device | Case ID | Result (PASS/FAIL) | Notes | Artifact link |
|---|---|---|---|---|---|---|

