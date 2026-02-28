using UnityEngine;
using System;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class ObstacleSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ObstacleWall obstaclePrefab;
    [SerializeField] private Transform obstacleParent;
    [Header("Pool")]
    [SerializeField, Min(1)] private int poolSize = 20;
    [Header("World")]
    [SerializeField] private float spawnY = 7f;
    [SerializeField] private float recycleY = -7f;
    [SerializeField, Min(0.1f)] private float wallHalfWidth = 6f;
    [SerializeField, Min(0.5f)] private float playerBoundsX = 3.5f;
    [SerializeField, Min(0f)] private float maxGapShiftPerSpawn = 1.5f;
    [Header("Difficulty Ramp")]
    [SerializeField, Min(0f)] private float startSpeed = 3.8f;
    [SerializeField, Min(0f)] private float maxSpeed = 8.8f;
    [SerializeField, Min(0.1f)] private float rampDuration = 80f;
    [SerializeField, Min(0.1f)] private float safePhaseSeconds = 5f;
    [SerializeField, Min(0.1f)] private float tensePhaseSeconds = 10f;
    [SerializeField, Min(0.1f)] private float intensePhaseSeconds = 20f;
    [SerializeField, Range(0.4f, 3f)] private float speedRampExponent = 1.3f;
    [SerializeField, Min(0.01f)] private float dangerOvertimeRate = 0.3f;
    [SerializeField, Min(0.2f)] private float startSpacing = 2.6f;
    [SerializeField, Min(0.2f)] private float minSpacing = 1.75f;
    [SerializeField, Min(0.3f)] private float startGapWidth = 2.8f;
    [SerializeField, Min(0.3f)] private float minGapWidth = 1.55f;
    [SerializeField, Min(0.1f)] private float gapShrinkDurationSeconds = 26f;
    [SerializeField, Range(0.4f, 3f)] private float gapShrinkExponent = 1.1f;
    [Header("Signature Escalation")]
    [SerializeField, Min(0f)] private float signatureMomentSeconds = 15f;
    [SerializeField, Min(0f)] private float signatureSpeedJump = 1f;
    [Header("Obstacle Variants")]
    [SerializeField, Range(0f, 1f)] private float movingGapChanceAtStart = 0.08f;
    [SerializeField, Range(0f, 1f)] private float movingGapChanceAtMaxDifficulty = 0.35f;
    [SerializeField, Min(0f)] private float movingGapAmplitudeMin = 0.15f;
    [SerializeField, Min(0f)] private float movingGapAmplitudeMax = 1.1f;
    [SerializeField, Min(0.01f)] private float movingGapFrequencyMin = 0.3f;
    [SerializeField, Min(0.01f)] private float movingGapFrequencyMax = 0.9f;
    [Header("Near Miss")]
    [SerializeField, Min(0f)] private float nearMissThreshold = 0.18f;
    [SerializeField, Min(0f)] private float lethalGapEdgePadding = 0.01f;
    [SerializeField, Min(0f)] private float nearMissSpeedBoost = 0.45f;
    [SerializeField, Min(0f)] private float maxNearMissSpeedBoost = 2.25f;
    [SerializeField, Min(0f)] private float speedBoostDecayPerSecond = 0.8f;
    [Header("Visual Intensity")]
    [SerializeField, Range(0f, 1f)] private float minDangerIntensity = 0f;
    [SerializeField, Range(0f, 1f)] private float maxDangerIntensity = 1f;
    [SerializeField] private AnimationCurve dangerIntensityByDifficulty = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Header("Deterministic Simulation")]
    [SerializeField] private bool deterministicSimulation;
    [SerializeField, Min(0.001f)] private float deterministicStep = 1f / 60f;
    [SerializeField] private int deterministicSeed = 12345;
    [SerializeField, Min(1)] private int maxDeterministicStepsPerFrame = 10;
    [SerializeField, Min(1)] private int maxSpawnIterationsPerStep = 12;

    private PlayerController player;
    private ScoreManager scoreManager;
    private AudioManager audioManager;
    private Pool<ObstacleWall> pool;
    private ObstacleWall[] activeWalls;
    private int activeWallCount;
    private float runElapsedSeconds;
    private float distanceSinceSpawn;
    private float lastGapCenter;
    private bool hasLastGapCenter;
    private bool wasPlayingLastFrame;
    private bool poolInitializationErrorLogged;
    private float deterministicAccumulator;
    private System.Random deterministicRandom;
    private bool previousDeterministicSimulation;
    private bool configuredDeterministicSimulation;
    private int configuredDeterministicSeed;
    private bool deterministicStepClampWarningLogged;
    private bool spawnIterationClampWarningLogged;
    private float transientSpeedBoost;
    private int lastAnalyticsSpeedTierIndex = -1;
    private Collider2D playerCollider;
    private bool signatureMomentTriggered;

    public float CurrentSpeed { get; private set; }
    public float RunElapsedSeconds => runElapsedSeconds;
    public bool SignatureMomentTriggered => signatureMomentTriggered;
    public bool DefaultDeterministicSimulation => configuredDeterministicSimulation;
    public bool IsDeterministicSimulationActive => deterministicSimulation;
    public int CurrentDeterministicSeed => deterministicSeed;

    public bool TryGetConfiguredSeed(out int seed)
    {
        if (deterministicSimulation)
        {
            seed = deterministicSeed;
            return true;
        }

        seed = 0;
        return false;
    }

    private void Awake()
    {
        if (obstacleParent == null)
        {
            obstacleParent = transform;
        }

        TryInitializePool();
        configuredDeterministicSimulation = deterministicSimulation;
        configuredDeterministicSeed = deterministicSeed;
        previousDeterministicSimulation = deterministicSimulation;
        if (deterministicSimulation)
        {
            ResetDeterministicRandom();
        }
        CurrentSpeed = Mathf.Max(0f, startSpeed);
    }

    private void Update()
    {
        TryInitializePool();

        bool isPlaying = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        if (!isPlaying)
        {
            wasPlayingLastFrame = false;
            return;
        }

        if (!wasPlayingLastFrame)
        {
            ResetRun();
            wasPlayingLastFrame = true;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        HandleDeterministicModeToggle();
        if (deterministicSimulation)
        {
            deterministicAccumulator += dt;
            float step = Mathf.Max(0.001f, deterministicStep);
            int stepsThisFrame = 0;
            while (deterministicAccumulator >= step)
            {
                SimulateStep(step);
                deterministicAccumulator -= step;
                stepsThisFrame++;
                if (stepsThisFrame >= Mathf.Max(1, maxDeterministicStepsPerFrame))
                {
                    deterministicAccumulator = Mathf.Min(deterministicAccumulator, step * 0.5f);
                    LogDeterministicStepClampWarningOnce();
                    break;
                }
            }

            return;
        }

        SimulateStep(dt);
    }

    public void SetSystems(PlayerController player, ScoreManager scoreManager, AudioManager audioManager)
    {
        this.player = player;
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        this.scoreManager = scoreManager;
        this.audioManager = audioManager;
        if (player != null)
        {
            playerBoundsX = Mathf.Max(0.5f, player.MaxX);
            wallHalfWidth = Mathf.Max(playerBoundsX, wallHalfWidth);
        }
    }

    public void SetObstaclePrefab(ObstacleWall prefab, Transform parent)
    {
        obstaclePrefab = prefab;
        if (parent != null) obstacleParent = parent;
        TryInitializePool();
    }

    public void ConfigureRunSeed(int runSeed, bool forceDeterministic)
    {
        bool deterministicEnabled = forceDeterministic || configuredDeterministicSimulation;
        deterministicSimulation = deterministicEnabled;
        deterministicSeed = deterministicEnabled
            ? (forceDeterministic ? runSeed : configuredDeterministicSeed)
            : configuredDeterministicSeed;

        previousDeterministicSimulation = deterministicSimulation;
        deterministicAccumulator = 0f;
        if (deterministicSimulation)
        {
            ResetDeterministicRandom();
        }
    }

    public void ResetRun()
    {
        TryInitializePool();
        if (pool != null && activeWalls != null)
        {
            pool.ReleaseAll(activeWalls, ref activeWallCount);
        }
        else
        {
            activeWallCount = 0;
        }

        runElapsedSeconds = 0f;
        distanceSinceSpawn = 0f;
        hasLastGapCenter = false;
        CurrentSpeed = Mathf.Max(0f, startSpeed);
        lastAnalyticsSpeedTierIndex = GetSpeedTierIndex(CurrentSpeed);
        transientSpeedBoost = 0f;
        signatureMomentTriggered = false;
        deterministicAccumulator = 0f;
        if (deterministicSimulation)
        {
            ResetDeterministicRandom();
        }
        wasPlayingLastFrame = true;
        scoreManager?.ResetRun();

        if (pool != null && activeWalls != null)
        {
            SpawnWall(EvaluateGapWidth(), 0f);
        }
    }

    private bool MoveAndRecycleWalls(float distance, float dt, float playerY, float playerX, bool hasPlayerBounds, Bounds playerBounds)
    {
        for (int i = 0; i < activeWallCount; i++)
        {
            ObstacleWall wall = activeWalls[i];
            wall.Simulate(dt);
            wall.MoveDown(distance);

            if (hasPlayerBounds && wall.OverlapsSolidBounds(playerBounds, lethalGapEdgePadding))
            {
                KillPlayerIfPossible("geometry_overlap");
                return true;
            }

            if (wall.TryRegisterPass(playerY, playerX, nearMissThreshold))
            {
                if (!wall.IsInsideGap(playerX, lethalGapEdgePadding))
                {
                    KillPlayerIfPossible("gap_miss");
                    return true;
                }
                else if (wall.WasNearMiss)
                {
                    scoreManager?.AddNearMissBonus(wall.NearMissDistance);
                    audioManager?.PlayNearMiss();
                    transientSpeedBoost = Mathf.Min(
                        GetTransientSpeedBoostCap(),
                        transientSpeedBoost + Mathf.Max(0f, nearMissSpeedBoost));
                }
            }

            if (wall.transform.position.y <= recycleY)
            {
                RecycleWallAt(i);
                i--;
            }
        }

        return false;
    }

    private void SpawnBySpacing(float distance, float difficulty)
    {
        distanceSinceSpawn += distance;
        float spacing = EvaluateSpacing(difficulty);
        int iterations = 0;

        while (distanceSinceSpawn >= spacing)
        {
            if (!SpawnWall(EvaluateGapWidth(), difficulty))
            {
                distanceSinceSpawn = spacing;
                return;
            }

            distanceSinceSpawn -= spacing;
            spacing = EvaluateSpacing(difficulty);
            iterations++;
            if (iterations >= Mathf.Max(1, maxSpawnIterationsPerStep))
            {
                distanceSinceSpawn = Mathf.Min(distanceSinceSpawn, spacing);
                LogSpawnIterationClampWarningOnce();
                return;
            }
        }
    }

    private bool SpawnWall(float gapWidth, float difficulty)
    {
        if (pool == null || activeWalls == null || activeWallCount >= activeWalls.Length) return false;
        if (!pool.TryGet(out ObstacleWall wall)) return false;

        float fairGapWidth = ClampGapWidthToFairRange(gapWidth);
        float centerLimit = Mathf.Max(0f, playerBoundsX - (fairGapWidth * 0.5f));

        float center = SampleGapCenter(centerLimit);
        if (hasLastGapCenter)
        {
            center = Mathf.Clamp(center, lastGapCenter - maxGapShiftPerSpawn, lastGapCenter + maxGapShiftPerSpawn);
            center = Mathf.Clamp(center, -centerLimit, centerLimit);
        }

        wall.Configure(center, fairGapWidth, wallHalfWidth, spawnY);
        ConfigureWallVariant(wall, center, fairGapWidth, difficulty);
        wall.SetDangerIntensity(EvaluateDangerIntensity(difficulty));
        activeWalls[activeWallCount] = wall;
        activeWallCount++;
        lastGapCenter = center;
        hasLastGapCenter = true;
        return true;
    }

    private void RecycleWallAt(int index)
    {
        int last = activeWallCount - 1;
        ObstacleWall wall = activeWalls[index];

        activeWalls[index] = activeWalls[last];
        activeWalls[last] = null;
        activeWallCount = last;
        pool.Release(wall);
    }

    private float EvaluateSpacing(float difficulty)
    {
        float clampedDifficulty = Mathf.Clamp01(difficulty);
        return Mathf.Max(0.2f, Mathf.Lerp(startSpacing, minSpacing, clampedDifficulty));
    }

    private float EvaluateGapWidth()
    {
        float shrinkProgress = gapShrinkDurationSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(runElapsedSeconds / gapShrinkDurationSeconds);
        float curvedProgress = Mathf.Pow(shrinkProgress, Mathf.Max(0.4f, gapShrinkExponent));
        return ClampGapWidthToFairRange(Mathf.Lerp(startGapWidth, minGapWidth, curvedProgress));
    }

    private float ClampGapWidthToFairRange(float gapWidth)
    {
        float maxGap = Mathf.Max(0.3f, playerBoundsX * 2f);
        return Mathf.Clamp(gapWidth, 0.9f, maxGap);
    }

    private void SimulateStep(float dt)
    {
        runElapsedSeconds += dt;
        float difficulty = EvaluateSpeedProgress(runElapsedSeconds);
        float baseSpeed = Mathf.Lerp(startSpeed, maxSpeed, difficulty);
        TryTriggerSignatureEscalation();
        transientSpeedBoost = Mathf.MoveTowards(
            transientSpeedBoost,
            0f,
            Mathf.Max(0f, speedBoostDecayPerSecond) * dt);
        CurrentSpeed = baseSpeed + transientSpeedBoost;
        EmitSpeedTierChangedIfNeeded();
        ApplyDangerIntensityToActiveWalls(difficulty);

        float distance = CurrentSpeed * dt;
        scoreManager?.AddDistance(distance);
        float playerY = player != null ? player.transform.position.y : float.NegativeInfinity;
        float playerX = player != null ? player.transform.position.x : 0f;
        bool hasPlayerBounds = TryGetPlayerBounds(out Bounds playerBounds);
        if (MoveAndRecycleWalls(distance, dt, playerY, playerX, hasPlayerBounds, playerBounds))
        {
            return;
        }

        SpawnBySpacing(distance, difficulty);
    }

    private float EvaluateSpeedProgress(float elapsedSeconds)
    {
        float safeEnd = Mathf.Max(0.1f, safePhaseSeconds);
        float tenseEnd = Mathf.Max(safeEnd + 0.1f, tensePhaseSeconds);
        float intenseEnd = Mathf.Max(tenseEnd + 0.1f, intensePhaseSeconds);
        float exponent = Mathf.Max(0.4f, speedRampExponent);
        float elapsed = Mathf.Max(0f, elapsedSeconds);

        if (elapsed <= safeEnd)
        {
            float t = elapsed / safeEnd;
            return Mathf.Lerp(0f, 0.22f, Mathf.Pow(t, exponent));
        }

        if (elapsed <= tenseEnd)
        {
            float t = (elapsed - safeEnd) / Mathf.Max(0.1f, tenseEnd - safeEnd);
            float stageExponent = Mathf.Max(0.35f, exponent * 0.85f);
            return Mathf.Lerp(0.22f, 0.5f, Mathf.Pow(t, stageExponent));
        }

        if (elapsed <= intenseEnd)
        {
            float t = (elapsed - tenseEnd) / Mathf.Max(0.1f, intenseEnd - tenseEnd);
            float stageExponent = Mathf.Max(0.3f, exponent * 0.65f);
            return Mathf.Lerp(0.5f, 0.85f, Mathf.Pow(t, stageExponent));
        }

        float overtime = elapsed - intenseEnd;
        float dangerBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, dangerOvertimeRate) * overtime);
        return Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(dangerBlend));
    }

    private void TryTriggerSignatureEscalation()
    {
        if (signatureMomentTriggered || runElapsedSeconds < Mathf.Max(0f, signatureMomentSeconds))
        {
            return;
        }

        signatureMomentTriggered = true;
        transientSpeedBoost = Mathf.Min(GetTransientSpeedBoostCap(), transientSpeedBoost + Mathf.Max(0f, signatureSpeedJump));
    }

    private void ApplyDangerIntensityToActiveWalls(float difficulty)
    {
        if (activeWalls == null || activeWallCount <= 0)
        {
            return;
        }

        float dangerIntensity = EvaluateDangerIntensity(difficulty);
        for (int i = 0; i < activeWallCount; i++)
        {
            activeWalls[i]?.SetDangerIntensity(dangerIntensity);
        }
    }

    private float SampleGapCenter(float centerLimit)
    {
        if (centerLimit <= 0f)
        {
            return 0f;
        }

        return Mathf.Lerp(-centerLimit, centerLimit, SampleRandom01());
    }

    private void HandleDeterministicModeToggle()
    {
        if (previousDeterministicSimulation == deterministicSimulation)
        {
            return;
        }

        previousDeterministicSimulation = deterministicSimulation;
        deterministicAccumulator = 0f;
        if (deterministicSimulation)
        {
            ResetDeterministicRandom();
        }
    }

    private void ResetDeterministicRandom()
    {
        deterministicRandom = new System.Random(deterministicSeed);
    }

    private float EvaluateDangerIntensity(float difficulty)
    {
        float normalizedDifficulty = Mathf.Clamp01(difficulty);
        float curvedIntensity = normalizedDifficulty;
        if (dangerIntensityByDifficulty != null && dangerIntensityByDifficulty.length > 0)
        {
            curvedIntensity = Mathf.Clamp01(dangerIntensityByDifficulty.Evaluate(normalizedDifficulty));
        }

        return Mathf.Lerp(minDangerIntensity, maxDangerIntensity, curvedIntensity);
    }

    private void EmitSpeedTierChangedIfNeeded()
    {
        int tierIndex = GetSpeedTierIndex(CurrentSpeed);
        if (tierIndex == lastAnalyticsSpeedTierIndex)
        {
            return;
        }

        lastAnalyticsSpeedTierIndex = tierIndex;
        Dictionary<string, object> fields = new Dictionary<string, object>(8)
        {
            ["speed"] = CurrentSpeed,
            ["tier"] = GetSpeedTierLabel(tierIndex)
        };

        GameManager.Instance?.PopulateRunContext(fields);
        GameplayAnalytics.Track("speed_tier_changed", fields);
    }

    private int GetSpeedTierIndex(float speed)
    {
        float maxRange = Mathf.Max(startSpeed + 0.001f, maxSpeed);
        float normalized = Mathf.Clamp01(Mathf.InverseLerp(startSpeed, maxRange, speed));
        if (normalized >= 0.85f)
        {
            return 3;
        }

        if (normalized >= 0.55f)
        {
            return 2;
        }

        if (normalized >= 0.25f)
        {
            return 1;
        }

        return 0;
    }

    private static string GetSpeedTierLabel(int tierIndex)
    {
        return tierIndex switch
        {
            3 => "HYPER",
            2 => "BLAZE",
            1 => "RUSH",
            _ => "FLOW"
        };
    }

    private float GetTransientSpeedBoostCap()
    {
        return Mathf.Max(0f, maxNearMissSpeedBoost + Mathf.Max(0f, signatureSpeedJump));
    }

    private static void KillPlayerIfPossible(string deathCause)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            GameManager.Instance.KillPlayerWithCause(deathCause);
        }
    }

    private void OnValidate()
    {
        poolSize = Mathf.Max(1, poolSize);
        startSpeed = Mathf.Max(0f, startSpeed);
        maxSpeed = Mathf.Max(startSpeed, maxSpeed);
        rampDuration = Mathf.Max(0.1f, rampDuration);
        safePhaseSeconds = Mathf.Max(0.1f, safePhaseSeconds);
        tensePhaseSeconds = Mathf.Max(safePhaseSeconds + 0.1f, tensePhaseSeconds);
        intensePhaseSeconds = Mathf.Max(tensePhaseSeconds + 0.1f, intensePhaseSeconds);
        speedRampExponent = Mathf.Clamp(speedRampExponent, 0.4f, 3f);
        dangerOvertimeRate = Mathf.Max(0.01f, dangerOvertimeRate);
        minSpacing = Mathf.Max(0.2f, minSpacing);
        startSpacing = Mathf.Max(minSpacing, startSpacing);
        minGapWidth = Mathf.Max(0.9f, minGapWidth);
        startGapWidth = Mathf.Max(minGapWidth, startGapWidth);
        gapShrinkDurationSeconds = Mathf.Max(0.1f, gapShrinkDurationSeconds);
        gapShrinkExponent = Mathf.Clamp(gapShrinkExponent, 0.4f, 3f);
        signatureMomentSeconds = Mathf.Max(0f, signatureMomentSeconds);
        signatureSpeedJump = Mathf.Max(0f, signatureSpeedJump);
        movingGapChanceAtStart = Mathf.Clamp01(movingGapChanceAtStart);
        movingGapChanceAtMaxDifficulty = Mathf.Clamp01(movingGapChanceAtMaxDifficulty);
        movingGapAmplitudeMin = Mathf.Max(0f, movingGapAmplitudeMin);
        movingGapAmplitudeMax = Mathf.Max(movingGapAmplitudeMin, movingGapAmplitudeMax);
        movingGapFrequencyMin = Mathf.Max(0.01f, movingGapFrequencyMin);
        movingGapFrequencyMax = Mathf.Max(movingGapFrequencyMin, movingGapFrequencyMax);
        playerBoundsX = Mathf.Max(0.5f, playerBoundsX);
        wallHalfWidth = Mathf.Max(playerBoundsX, wallHalfWidth);
        deterministicStep = Mathf.Max(0.001f, deterministicStep);
        maxDeterministicStepsPerFrame = Mathf.Max(1, maxDeterministicStepsPerFrame);
        maxSpawnIterationsPerStep = Mathf.Max(1, maxSpawnIterationsPerStep);
        nearMissSpeedBoost = Mathf.Max(0f, nearMissSpeedBoost);
        maxNearMissSpeedBoost = Mathf.Max(0f, maxNearMissSpeedBoost);
        speedBoostDecayPerSecond = Mathf.Max(0f, speedBoostDecayPerSecond);
        minDangerIntensity = Mathf.Clamp01(minDangerIntensity);
        maxDangerIntensity = Mathf.Clamp(maxDangerIntensity, minDangerIntensity, 1f);
        lethalGapEdgePadding = Mathf.Max(0f, lethalGapEdgePadding);
    }

    private void ConfigureWallVariant(ObstacleWall wall, float baseGapCenter, float gapWidth, float difficulty)
    {
        if (wall == null)
        {
            return;
        }

        float movingChance = EvaluateMovingGapChance(difficulty);
        if (movingChance <= 0f || SampleRandom01() > movingChance)
        {
            return;
        }

        float centerLimit = Mathf.Max(0f, playerBoundsX - (gapWidth * 0.5f));
        if (centerLimit <= 0f)
        {
            return;
        }

        float maxFairAmplitude = Mathf.Max(0f, centerLimit - Mathf.Abs(baseGapCenter));
        if (maxFairAmplitude <= 0f)
        {
            return;
        }

        float amplitude = Mathf.Min(maxFairAmplitude, SampleRange(movingGapAmplitudeMin, movingGapAmplitudeMax));
        if (amplitude <= 0f)
        {
            return;
        }

        float frequency = Mathf.Max(0.01f, SampleRange(movingGapFrequencyMin, movingGapFrequencyMax));
        float phase = SampleRange(0f, Mathf.PI * 2f);
        wall.ConfigureOscillation(amplitude, frequency, phase, -centerLimit, centerLimit);
    }

    private float EvaluateMovingGapChance(float difficulty)
    {
        float clampedDifficulty = Mathf.Clamp01(difficulty);
        return Mathf.Lerp(movingGapChanceAtStart, movingGapChanceAtMaxDifficulty, clampedDifficulty);
    }

    private float SampleRange(float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return Mathf.Lerp(min, max, SampleRandom01());
    }

    private float SampleRandom01()
    {
        if (!deterministicSimulation)
        {
            return UnityEngine.Random.value;
        }

        if (deterministicRandom == null)
        {
            ResetDeterministicRandom();
        }

        return (float)deterministicRandom.NextDouble();
    }

    private bool TryGetPlayerBounds(out Bounds bounds)
    {
        if (player == null)
        {
            bounds = default;
            return false;
        }

        if (playerCollider == null)
        {
            playerCollider = player.GetComponent<Collider2D>();
        }

        if (playerCollider == null || !playerCollider.enabled)
        {
            bounds = default;
            return false;
        }

        bounds = playerCollider.bounds;
        return bounds.size.sqrMagnitude > 0f;
    }

    private bool TryInitializePool()
    {
        if (pool != null && activeWalls != null)
        {
            return true;
        }

        if (pool != null || activeWalls != null)
        {
            pool = null;
            activeWalls = null;
            activeWallCount = 0;
        }

        if (obstaclePrefab == null)
        {
            LogPoolInitializationErrorOnce(
                "ObstacleSpawner pool initialization failed: obstacle prefab is missing. Spawning is disabled until prefab is assigned.");
            return false;
        }

        if (obstacleParent == null) obstacleParent = transform;

        int safePoolSize = Mathf.Max(1, poolSize);
        try
        {
            pool = new Pool<ObstacleWall>(obstaclePrefab, safePoolSize, obstacleParent);
            activeWalls = new ObstacleWall[safePoolSize];
            poolInitializationErrorLogged = false;
            return true;
        }
        catch (Exception ex)
        {
            pool = null;
            activeWalls = null;
            activeWallCount = 0;
            LogPoolInitializationErrorOnce(
                $"ObstacleSpawner pool initialization failed with exception: {ex.Message}");
            return false;
        }
    }

    private void LogPoolInitializationErrorOnce(string message)
    {
        if (poolInitializationErrorLogged)
        {
            return;
        }

        poolInitializationErrorLogged = true;
        Debug.LogError(message, this);
    }

    private void LogDeterministicStepClampWarningOnce()
    {
        if (deterministicStepClampWarningLogged)
        {
            return;
        }

        deterministicStepClampWarningLogged = true;
        Debug.LogWarning("ObstacleSpawner clamped deterministic steps for this frame to avoid runaway simulation.", this);
    }

    private void LogSpawnIterationClampWarningOnce()
    {
        if (spawnIterationClampWarningLogged)
        {
            return;
        }

        spawnIterationClampWarningLogged = true;
        Debug.LogWarning("ObstacleSpawner spawn iterations were clamped to preserve frame stability.", this);
    }
}
