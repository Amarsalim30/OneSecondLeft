using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float restartDelaySeconds = 0.18f;
    [SerializeField, Min(0f)] private float deathSummaryMinVisibleSeconds = 0f;
    [SerializeField] private bool autoRestartAfterDeath;
    [SerializeField] private bool waitForStartInputOnLoad = true;
    [SerializeField] private bool requireStartInputRelease = true;
    [SerializeField, Range(30, 240)] private int targetFrameRate = 60;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TimeAbility timeAbility;
    [SerializeField] private ObstacleSpawner obstacleSpawner;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private UIHud uiHud;
    [Header("Ambient Hum")]
    [SerializeField, Range(0f, 1f)] private float titleHumIntensity = 0.2f;
    [SerializeField, Range(0f, 1f)] private float runHumIntensity = 0.5f;
    [SerializeField, Range(0f, 1f)] private float deathHumIntensity = 0.16f;
    [Header("Run Seeding")]
    [SerializeField] private RunSeedMode runSeedMode = RunSeedMode.Normal;
    [SerializeField] private bool dailyChallengeUseUtcDate = true;
    [SerializeField] private string dailyChallengeSeedSalt = "OneSecondLeft.DailyChallenge.v1";

    private float restartTimer;
    private bool restartQueued;
    private bool missingSystemWarningLogged;
    private RunSeedContext currentRunContext = new RunSeedContext(RunSeedMode.Normal, 0, string.Empty, false);
    private int runCount;
    private float currentRunStartUnscaledTime;
    private float lastDeathUnscaledTime = float.NegativeInfinity;
    private string lastDeathCause = "none";
    private PendingRunStart pendingRunStart = PendingRunStart.None;
    private bool runStartInputGateArmed;
    private bool manualRestartReady;

    public bool IsPlaying { get; private set; }
    public RunSeedContext CurrentRunContext => currentRunContext;
    public RunSeedMode CurrentRunMode => currentRunContext.Mode;
    public int CurrentRunSeed => currentRunContext.Seed;
    public string CurrentChallengeDateKey => currentRunContext.ChallengeDateKey;
    public int CurrentRunCount => runCount;
    public string LastDeathCause => lastDeathCause;
    public bool CanManualRestart => manualRestartReady;
    public event Action<RunSeedContext> RunContextChanged;

    private enum PendingRunStart
    {
        None,
        Initial
    }

    public void Configure(
        PlayerController player,
        TimeAbility ability,
        ObstacleSpawner spawner,
        ScoreManager score,
        AudioManager audio,
        UIHud hud)
    {
        playerController = player;
        timeAbility = ability;
        obstacleSpawner = spawner;
        scoreManager = score;
        audioManager = audio;
        uiHud = hud;
    }

    public void Configure(PlayerController player, TimeAbility ability, UIHud hud)
    {
        Configure(player, ability, obstacleSpawner, scoreManager, audioManager, hud);
    }

    public void SetRunSeedMode(RunSeedMode mode, bool restartIfPlaying = false)
    {
        runSeedMode = mode;
        if (restartIfPlaying && IsPlaying)
        {
            StartRun();
            return;
        }

        if (!IsPlaying)
        {
            UpdateRunContext(BuildRunSeedContext());
        }
    }

    public void PopulateRunContext(IDictionary<string, object> fields)
    {
        if (fields == null)
        {
            return;
        }

        fields["run_mode"] = currentRunContext.Mode == RunSeedMode.DailyChallenge ? "daily_challenge" : "normal";
        fields["run_seed"] = currentRunContext.Seed;
        fields["run_deterministic"] = currentRunContext.Deterministic;
        fields["run_index"] = runCount;

        if (!string.IsNullOrEmpty(currentRunContext.ChallengeDateKey))
        {
            fields["challenge_date"] = currentRunContext.ChallengeDateKey;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (targetFrameRate > 0)
        {
            Application.targetFrameRate = targetFrameRate;
        }
    }

    private void Start()
    {
        ResolveMissingReferences();
        WireSystems();
        ApplyRunSeeding();
        LogMissingSystemsIfAny();

        if (waitForStartInputOnLoad)
        {
            EnterPendingInitialStartState();
            return;
        }

        StartRun();
    }

    private void OnDisable()
    {
        NormalizeTimeState();
        audioManager?.SetAmbientHum(false);
    }

    private void OnApplicationPause(bool _)
    {
        NormalizeTimeState();
        EmitPauseFocusNormalizedAnalytics("pause");
    }

    private void OnApplicationFocus(bool _)
    {
        NormalizeTimeState();
        EmitPauseFocusNormalizedAnalytics("focus");
    }

    private void OnDestroy()
    {
        NormalizeTimeState();
        audioManager?.SetAmbientHum(false);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (restartQueued)
        {
            restartTimer -= Time.unscaledDeltaTime;
            if (restartTimer > 0f)
            {
                return;
            }

            restartQueued = false;
            if (autoRestartAfterDeath)
            {
                StartRunInternal(autoRestarted: true);
                return;
            }

            manualRestartReady = true;
        }

        if (manualRestartReady && IsManualRestartTriggeredThisFrame())
        {
            RequestManualRestart();
        }

        if (pendingRunStart == PendingRunStart.None || manualRestartReady)
        {
            return;
        }

        if (!runStartInputGateArmed)
        {
            if (!IsStartInputPressed())
            {
                runStartInputGateArmed = true;
            }

            return;
        }

        if (!IsStartInputTriggeredThisFrame())
        {
            return;
        }

        pendingRunStart = PendingRunStart.None;
        runStartInputGateArmed = false;
        StartRunInternal(autoRestarted: false);
    }

    public void KillPlayer()
    {
        KillPlayerWithCause("unknown");
    }

    public void KillPlayerWithCause(string deathCause)
    {
        if (!IsPlaying)
        {
            return;
        }

        lastDeathCause = string.IsNullOrWhiteSpace(deathCause) ? "unknown" : deathCause;
        lastDeathUnscaledTime = Time.unscaledTime;
        EmitDeathCauseAnalytics(lastDeathCause);
        EmitRunEndAnalytics(lastDeathCause);

        IsPlaying = false;
        pendingRunStart = PendingRunStart.None;
        runStartInputGateArmed = false;
        manualRestartReady = false;

        scoreManager?.CommitRunIfBest();
        audioManager?.PlayCrash();
        audioManager?.SetAmbientHum(true, deathHumIntensity);
        timeAbility?.ForceNormalTime();
        uiHud?.ShowDeath();

        float postDeathDelay = Mathf.Max(restartDelaySeconds, deathSummaryMinVisibleSeconds);
        if (autoRestartAfterDeath)
        {
            restartQueued = true;
            restartTimer = postDeathDelay;
            return;
        }

        if (postDeathDelay > 0f)
        {
            restartQueued = true;
            restartTimer = postDeathDelay;
            return;
        }

        restartQueued = false;
        restartTimer = 0f;
        manualRestartReady = true;
    }

    public void StartRun()
    {
        StartRunInternal(autoRestarted: false);
    }

    public void RequestManualRestart()
    {
        if (!manualRestartReady || IsPlaying)
        {
            return;
        }

        StartRunInternal(autoRestarted: false);
    }

    private void OnValidate()
    {
        restartDelaySeconds = Mathf.Max(0f, restartDelaySeconds);
        deathSummaryMinVisibleSeconds = Mathf.Max(0f, deathSummaryMinVisibleSeconds);
        targetFrameRate = Mathf.Clamp(targetFrameRate, 30, 240);
        titleHumIntensity = Mathf.Clamp01(titleHumIntensity);
        runHumIntensity = Mathf.Clamp01(runHumIntensity);
        deathHumIntensity = Mathf.Clamp01(deathHumIntensity);
        if (string.IsNullOrWhiteSpace(dailyChallengeSeedSalt))
        {
            dailyChallengeSeedSalt = "OneSecondLeft.DailyChallenge.v1";
        }
    }

    private void StartRunInternal(bool autoRestarted)
    {
        ResolveMissingReferences();
        WireSystems();
        ApplyRunSeeding();
        LogMissingSystemsIfAny();

        restartQueued = false;
        restartTimer = 0f;
        pendingRunStart = PendingRunStart.None;
        runStartInputGateArmed = false;
        manualRestartReady = false;
        IsPlaying = true;
        runCount++;
        currentRunStartUnscaledTime = Time.unscaledTime;
        lastDeathCause = "none";

        scoreManager?.ResetRun();
        obstacleSpawner?.ResetRun();
        playerController?.ResetRunPosition();
        timeAbility?.ResetMeter();
        audioManager?.SetSlowPitch(false);
        audioManager?.SetAmbientHum(true, runHumIntensity);
        uiHud?.HideTitle();
        uiHud?.HideDeath();
        EmitRunStartAnalytics();
        if (autoRestarted)
        {
            EmitRunAutoRestartedAnalytics();
        }
    }

    private void EnterPendingInitialStartState()
    {
        IsPlaying = false;
        restartQueued = false;
        restartTimer = 0f;
        lastDeathCause = "none";
        manualRestartReady = false;

        scoreManager?.ResetRun();
        obstacleSpawner?.ResetRun();
        playerController?.ResetRunPosition();
        timeAbility?.ResetMeter();
        audioManager?.SetSlowPitch(false);
        audioManager?.SetAmbientHum(true, titleHumIntensity);
        uiHud?.ShowTitle();
        uiHud?.HideDeath();

        QueuePendingRunStart(PendingRunStart.Initial);
    }

    private void QueuePendingRunStart(PendingRunStart startReason)
    {
        pendingRunStart = startReason;
        if (!requireStartInputRelease)
        {
            runStartInputGateArmed = true;
            return;
        }

        // Require a full release->press cycle so held touch/mouse cannot auto-start.
        runStartInputGateArmed = !IsStartInputPressed();
    }

    private bool IsStartInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            foreach (var candidate in touch.touches)
            {
                if (candidate.press.isPressed)
                {
                    return true;
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.isPressed)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.anyKey)
        {
            return true;
        }
#endif

        return false;
    }

    private bool IsStartInputTriggeredThisFrame()
    {
        bool suppressPointerStart = IsPointerOverUi();

#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null && !suppressPointerStart)
        {
            foreach (var candidate in touch.touches)
            {
                if (candidate.press.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !suppressPointerStart)
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!suppressPointerStart)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                {
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
        }

        if (Input.anyKeyDown)
        {
            return true;
        }
#endif

        return false;
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (eventSystem.IsPointerOverGameObject())
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        Touchscreen touch = Touchscreen.current;
        if (touch != null)
        {
            foreach (var candidate in touch.touches)
            {
                if (!candidate.press.isPressed)
                {
                    continue;
                }

                if (eventSystem.IsPointerOverGameObject(candidate.touchId.ReadValue()))
                {
                    return true;
                }
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount; i++)
        {
            if (eventSystem.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
            {
                return true;
            }
        }
#endif

        return false;
    }

    private static bool IsManualRestartTriggeredThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.rKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.R))
        {
            return true;
        }
#endif

        return false;
    }

    private void ResolveMissingReferences()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }

        if (timeAbility == null)
        {
            timeAbility = FindFirstObjectByType<TimeAbility>();
        }

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }

        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<AudioManager>();
        }

        if (uiHud == null)
        {
            uiHud = FindFirstObjectByType<UIHud>();
            if (uiHud == null && timeAbility != null && scoreManager != null)
            {
                uiHud = HudFactory.Create(timeAbility, scoreManager);
            }
        }
    }

    private void NormalizeTimeState()
    {
        timeAbility?.ForceNormalTime();

        if (timeAbility == null)
        {
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Time.timeScale = 1f;
            }

            if (!Mathf.Approximately(Time.fixedDeltaTime, 0.02f))
            {
                Time.fixedDeltaTime = 0.02f;
            }
        }

        audioManager?.SetSlowPitch(false);
    }

    private void WireSystems()
    {
        obstacleSpawner?.SetSystems(playerController, scoreManager, audioManager);
        timeAbility?.Configure(audioManager);
    }

    private void ApplyRunSeeding()
    {
        UpdateRunContext(BuildRunSeedContext());
        bool forceDeterministic = currentRunContext.Mode == RunSeedMode.DailyChallenge;
        obstacleSpawner?.ConfigureRunSeed(currentRunContext.Seed, forceDeterministic);
    }

    private RunSeedContext BuildRunSeedContext()
    {
        if (runSeedMode == RunSeedMode.DailyChallenge)
        {
            DateTime day = dailyChallengeUseUtcDate ? DateTime.UtcNow.Date : DateTime.Now.Date;
            string dayKey = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            int dailySeed = ComputeStableSeed($"{dailyChallengeSeedSalt}|{dayKey}");
            return new RunSeedContext(RunSeedMode.DailyChallenge, dailySeed, dayKey, deterministic: true);
        }

        bool deterministic = obstacleSpawner != null && obstacleSpawner.DefaultDeterministicSimulation;
        int seed = ComputeVolatileRunSeed();
        return new RunSeedContext(RunSeedMode.Normal, seed, string.Empty, deterministic);
    }

    private void UpdateRunContext(RunSeedContext context)
    {
        bool changed = context.Mode != currentRunContext.Mode ||
                       context.Seed != currentRunContext.Seed ||
                       context.Deterministic != currentRunContext.Deterministic ||
                       !string.Equals(context.ChallengeDateKey, currentRunContext.ChallengeDateKey, StringComparison.Ordinal);

        currentRunContext = context;
        if (changed)
        {
            RunContextChanged?.Invoke(currentRunContext);
        }
    }

    private int ComputeVolatileRunSeed()
    {
        unchecked
        {
            int ticks = (int)(DateTime.UtcNow.Ticks & 0x7fffffff);
            int seed = Environment.TickCount;
            seed = (seed * 397) ^ ticks;
            seed ^= (runCount + 1) * 7919;
            if (seed == 0)
            {
                seed = 1;
            }

            return seed;
        }
    }

    private static int ComputeStableSeed(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return 1;
        }

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < input.Length; i++)
            {
                hash ^= input[i];
                hash *= 16777619;
            }

            int seed = (int)(hash & 0x7fffffff);
            return seed == 0 ? 1 : seed;
        }
    }

    private void EmitRunStartAnalytics()
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(8)
        {
            ["restart_delay_seconds"] = restartDelaySeconds
        };

        PopulateRunContext(fields);
        GameplayAnalytics.Track("run_start", fields);
    }

    private void EmitRunAutoRestartedAnalytics()
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(8);
        if (!float.IsNegativeInfinity(lastDeathUnscaledTime))
        {
            fields["time_since_death_ms"] = Mathf.RoundToInt(Mathf.Max(0f, Time.unscaledTime - lastDeathUnscaledTime) * 1000f);
        }

        PopulateRunContext(fields);
        GameplayAnalytics.Track("run_auto_restarted", fields);
    }

    private void EmitDeathCauseAnalytics(string deathCause)
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(6)
        {
            ["death_cause"] = deathCause
        };

        PopulateRunContext(fields);
        GameplayAnalytics.Track("death_cause", fields);
    }

    private void EmitRunEndAnalytics(string deathCause)
    {
        float duration = Mathf.Max(0f, Time.unscaledTime - currentRunStartUnscaledTime);
        float score = scoreManager != null ? Mathf.Max(0f, scoreManager.CurrentScore) : 0f;
        float bestScore = scoreManager != null ? Mathf.Max(0f, scoreManager.BestScore) : 0f;
        bool newBest = scoreManager != null && scoreManager.IsCurrentRunNewBest;

        Dictionary<string, object> fields = new Dictionary<string, object>(10)
        {
            ["duration_seconds"] = duration,
            ["score"] = score,
            ["best_score"] = bestScore,
            ["new_best"] = newBest,
            ["death_cause"] = deathCause
        };

        PopulateRunContext(fields);
        GameplayAnalytics.Track("run_end", fields);
    }

    private void EmitPauseFocusNormalizedAnalytics(string reason)
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(6)
        {
            ["reason"] = reason,
            ["time_scale_after"] = Time.timeScale
        };

        PopulateRunContext(fields);
        GameplayAnalytics.Track("pause_focus_normalized", fields);
    }

    private void LogMissingSystemsIfAny()
    {
        List<string> missing = null;

        if (playerController == null)
        {
            missing ??= new List<string>();
            missing.Add(nameof(PlayerController));
        }

        if (timeAbility == null)
        {
            missing ??= new List<string>();
            missing.Add(nameof(TimeAbility));
        }

        if (obstacleSpawner == null)
        {
            missing ??= new List<string>();
            missing.Add(nameof(ObstacleSpawner));
        }

        if (scoreManager == null)
        {
            missing ??= new List<string>();
            missing.Add(nameof(ScoreManager));
        }

        if (missing == null || missing.Count == 0)
        {
            missingSystemWarningLogged = false;
            return;
        }

        if (missingSystemWarningLogged)
        {
            return;
        }

        missingSystemWarningLogged = true;
        Debug.LogWarning($"GameManager is missing runtime references: {string.Join(", ", missing)}. Gameplay will run in degraded mode.", this);
    }
}
