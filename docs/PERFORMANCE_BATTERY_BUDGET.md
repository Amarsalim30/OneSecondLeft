# Performance and Battery Budget (Low-End Android)

Last updated: 2026-02-28
Owner: Performance QA
Status: PLAN_READY (execution pending)

## Target Device Profile
- Android 8-10, 2-3 GB RAM, 60 Hz display, mid/low-tier CPU/GPU.

## Budget Targets
| Area | Target | Fail threshold |
|---|---|---|
| Frame rate | 60 fps target | sustained <45 fps for >10s |
| Frame time | p95 <= 22 ms | p95 > 28 ms |
| CPU main thread | avg <= 12 ms | avg > 16 ms |
| GC alloc in gameplay | 0 B/frame steady-state | recurring per-frame allocations |
| Memory footprint | <= 300 MB RSS | > 380 MB RSS |
| Battery drain | <= 10% per 30 min run | > 15% per 30 min run |
| Thermal behavior | no severe throttling in 20 min | sustained thermal throttling |

## Reproducible Test Protocol
1. Device prep:
- Battery 80-100%, unplugged, brightness fixed (50%), airplane mode on, close background apps.
2. Build prep:
- Use same build SHA; release profile with identical graphics settings.
3. Scenario:
- 20 minutes continuous play/restart loop in `Game` scene.
- Include at least 5 pause/resume cycles and 5 orientation changes.
4. Capture:
- Unity Profiler (CPU, Memory, Rendering).
- Android profiling captures (`adb shell dumpsys batterystats`, `adb shell dumpsys gfxinfo`).
5. Repeat:
- Run 3 trials per device; report median and worst-case.

## Report Template
| Date | Build SHA | Device | Trial | Avg FPS | P95 frame (ms) | Avg CPU (ms) | Peak memory (MB) | Battery delta | Result |
|---|---|---|---|---|---|---|---|---|---|

## Escalation Rules
- Any fail-threshold breach opens a P0/P1 perf issue with profiler evidence.
- Balancing/content changes cannot ship if they regress this budget.

