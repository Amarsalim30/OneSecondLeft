using UnityEngine;

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

    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        if (obstacleParent == null)
        {
            obstacleParent = transform;
        }

        TryInitializePool();
        CurrentSpeed = Mathf.Max(0f, startSpeed);
    }

    private void Update()
    {
        TryInitializePool();
        if (pool == null || activeWalls == null)
        {
            return;
        }

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
            if (player == null) return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        runElapsedSeconds += dt;
        float difficulty = rampDuration <= 0f ? 1f : Mathf.Clamp01(runElapsedSeconds / rampDuration);
        CurrentSpeed = Mathf.Lerp(startSpeed, maxSpeed, difficulty);

        float distance = CurrentSpeed * dt;
        scoreManager?.AddDistance(distance);
        MoveAndRecycleWalls(distance, player.transform.position.y, player.transform.position.x);
        SpawnBySpacing(distance, difficulty);
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

    public void ResetRun()
    {
        TryInitializePool();
        if (pool == null || activeWalls == null) return;

        pool.ReleaseAll(activeWalls, ref activeWallCount);
        runElapsedSeconds = 0f;
        distanceSinceSpawn = 0f;
        hasLastGapCenter = false;
        CurrentSpeed = Mathf.Max(0f, startSpeed);
        wasPlayingLastFrame = true;
        scoreManager?.ResetRun();
        SpawnWall(EvaluateGapWidth(0f));
    }

    private void MoveAndRecycleWalls(float distance, float playerY, float playerX)
    {
        for (int i = 0; i < activeWallCount; i++)
        {
            ObstacleWall wall = activeWalls[i];
            wall.MoveDown(distance);

            if (wall.TryRegisterPass(playerY, playerX, nearMissThreshold) && wall.WasNearMiss)
            {
                scoreManager?.AddNearMissBonus();
                audioManager?.PlayNearMiss();
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

        while (distanceSinceSpawn >= spacing)
        {
            if (!SpawnWall(EvaluateGapWidth(difficulty)))
            {
                distanceSinceSpawn = spacing;
                return;
            }

            distanceSinceSpawn -= spacing;
            spacing = EvaluateSpacing(difficulty);
        }
    }

    private bool SpawnWall(float gapWidth)
    {
        if (pool == null || activeWalls == null || activeWallCount >= activeWalls.Length) return false;
        if (!pool.TryGet(out ObstacleWall wall)) return false;

        float fairGapWidth = ClampGapWidthToFairRange(gapWidth);
        float centerLimit = Mathf.Max(0f, playerBoundsX - (fairGapWidth * 0.5f));

        float center = Random.Range(-centerLimit, centerLimit);
        if (hasLastGapCenter)
        {
            center = Mathf.Clamp(center, lastGapCenter - maxGapShiftPerSpawn, lastGapCenter + maxGapShiftPerSpawn);
            center = Mathf.Clamp(center, -centerLimit, centerLimit);
        }

        wall.Configure(center, fairGapWidth, wallHalfWidth, spawnY);
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
    }

    private void TryInitializePool()
    {
        if (pool != null || obstaclePrefab == null)
        {
            return;
        }
        if (obstacleParent == null) obstacleParent = transform;

        int safePoolSize = Mathf.Max(1, poolSize);
        pool = new Pool<ObstacleWall>(obstaclePrefab, safePoolSize, obstacleParent);
        activeWalls = new ObstacleWall[safePoolSize];
    }
}
