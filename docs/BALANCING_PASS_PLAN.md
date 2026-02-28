# Balancing Pass Plan

Last updated: 2026-02-28
Owner: Gameplay
Status: PLAN_READY

## Objective
- Tune difficulty ramp and near-miss reward loop so runs feel fair, rising, and replayable.
- Use measurable targets and controlled parameter changes only.

## Measurable Targets
Track over a minimum of 100 runs split by player segment.

| Metric | New players (first 10 runs) | Returning players (20+ runs) |
|---|---|---|
| Median survival time | 18-28s | 35-55s |
| 90th percentile survival | 35-50s | 60-90s |
| Near-miss rate | 1.0-3.0 per run | 2.0-6.0 per run |
| Peak combo multiplier | 1.25x-2.0x | 1.75x-3.0x |
| New-best frequency | 20-40% runs | 8-20% runs |

Guardrail:
- Deaths that feel unfair (player inside visible gap but killed) must be 0 in sampled videos.

## Tunable Parameters (Current Runtime)
Primary:
- `ObstacleSpawner`: `startSpeed`, `maxSpeed`, `rampDuration`, `startSpacing`, `minSpacing`, `startGapWidth`, `minGapWidth`, `nearMissThreshold`, `nearMissSpeedBoost`, `speedBoostDecayPerSecond`.
- `ScoreManager`: `nearMissBonusPoints`, `nearMissComboStep`, `maxNearMissMultiplier`, `nearMissComboDecayDistance`.

Secondary:
- `ObstacleSpawner`: `maxGapShiftPerSpawn`, `lethalGapEdgePadding`.

## Tuning Protocol
1. Lock a baseline build SHA and test settings.
2. Enable deterministic simulation (`ObstacleSpawner.deterministicSimulation = true`) for A/B tuning runs.
3. Collect baseline metrics from 30+ seeded runs before any edits.
4. Change one parameter cluster at a time:
- Cluster A: speed/spacing
- Cluster B: gap/fairness
- Cluster C: near-miss reward + combo
5. For each cluster, run 30+ seeded runs and compare delta vs baseline.
6. Promote a change only if:
- At least 3/5 target metrics move toward range.
- No fairness guardrail breach.
- No performance regression reported in [PERFORMANCE_BATTERY_BUDGET.md](./PERFORMANCE_BATTERY_BUDGET.md).
7. Re-validate on live play (non-deterministic) with at least 20 human runs.

## Change Log Template
| Date | Build SHA | Cluster | Params changed | Metrics delta | Decision | Owner |
|---|---|---|---|---|---|---|

## Exit Criteria
- All target metrics are in range for two consecutive balancing rounds.
- Parameter set is frozen and recorded for release candidate.

