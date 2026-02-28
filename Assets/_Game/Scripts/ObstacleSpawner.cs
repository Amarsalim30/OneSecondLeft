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
    [SerializeField, Min(0f)] private float maxSpeed = 7.5f;
    [SerializeField, Min(0.1f)] private float rampDuration = 80f;
    [SerializeField, Min(0.2f)] private float startSpacing = 2.6f;
    [SerializeField, Min(0.2f)] private float minSpacing = 1.9f;
    [SerializeField, Min(0.3f)] private float startGapWidth = 2.8f;
    [SerializeField, Min(0.3f)] private float minGapWidth = 1.8f;
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

    public float CurrentSpeed { get; private set; }
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
        deterministicAccumulator = 0f;
        if (deterministicSimulation)
        {
            ResetDeterministicRandom();
        }
        wasPlayingLastFrame = true;
        scoreManager?.ResetRun();

        if (pool != null && activeWalls != null)
        {
            SpawnWall(EvaluateGapWidth(0f), 0f);
        }
    }

    private void MoveAndRecycleWalls(float distance, float playerY, float playerX)
    {
        for (int i = 0; i < activeWallCount; i++)
        {
            ObstacleWall wall = activeWalls[i];
            wall.MoveDown(distance);

            if (wall.TryRegisterPass(playerY, playerX, nearMissThreshold))
            {
                if (!wall.IsInsideGap(playerX, lethalGapEdgePadding))
                {
                    KillPlayerIfPossible("gap_miss");
                }
                else if (wall.WasNearMiss)
                {
                    scoreManager?.AddNearMissBonus(wall.NearMissDistance);
                    audioManager?.PlayNearMiss();
                    transientSpeedBoost = Mathf.Min(
                        Mathf.Max(0f, maxNearMissSpeedBoost),
                        transientSpeedBoost + Mathf.Max(0f, nearMissSpeedBoost));
                }
            }

            if (wall.transform.position.y <= recycleY)
            {
                RecycleWallAt(i);
                i--;
            }
        }
    }

    private void SpawnBySpacing(float distance, float difficulty)
    {
        distanceSinceSpawn += distance;
        float spacing = EvaluateSpacing(difficulty);
        int iterations = 0;

        while (distanceSinceSpawn >= spacing)
        {
            if (!SpawnWall(EvaluateGapWidth(difficulty), difficulty))
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

    private float EvaluateGapWidth(float difficulty)
    {
        float clampedDifficulty = Mathf.Clamp01(difficulty);
        return ClampGapWidthToFairRange(Mathf.Lerp(startGapWidth, minGapWidth, clampedDifficulty));
    }

    private float ClampGapWidthToFairRange(float gapWidth)
    {
        float maxGap = Mathf.Max(0.3f, playerBoundsX * 2f);
        return Mathf.Clamp(gapWidth, 0.9f, maxGap);
    }

    private void SimulateStep(float dt)
    {
        runElapsedSeconds += dt;
        float difficulty = rampDuration <= 0f ? 1f : Mathf.Clamp01(runElapsedSeconds / rampDuration);
        float baseSpeed = Mathf.Lerp(startSpeed, maxSpeed, difficulty);
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
        MoveAndRecycleWalls(distance, playerY, playerX);
        SpawnBySpacing(distance, difficulty);
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

        if (!deterministicSimulation)
        {
            return UnityEngine.Random.Range(-centerLimit, centerLimit);
        }

        if (deterministicRandom == null)
        {
            ResetDeterministicRandom();
        }

        return Mathf.Lerp(-centerLimit, centerLimit, (float)deterministicRandom.NextDouble());
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
        minSpacing = Mathf.Max(0.2f, minSpacing);
        startSpacing = Mathf.Max(minSpacing, startSpacing);
        minGapWidth = Mathf.Max(0.9f, minGapWidth);
        startGapWidth = Mathf.Max(minGapWidth, startGapWidth);
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
