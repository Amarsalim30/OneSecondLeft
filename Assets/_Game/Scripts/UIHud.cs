using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UIHud : MonoBehaviour
{
    private const string StateRun = "RUN";
    private const string StateSlow = "SLOW";
    private const string StateCrash = "CRASH";
    private const string NewBestText = "NEW BEST";
    private const string ComboReadyText = "COMBO READY";
    private const string DefaultSpeedTier = "FLOW";
    private const string DefaultShareHintText = "Tap button or press S";
    private const string DefaultShareStatusText = " ";
    private const string DefaultSeedText = "N/A";
    private const float NearMissEventEpsilon = 0.0001f;

    [SerializeField] private TimeAbility timeAbility;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ObstacleSpawner obstacleSpawner;
    [SerializeField] private Text meterLabel;
    [SerializeField] private Image meterFill;
    [SerializeField] private Text stateLabel;
    [SerializeField] private Text scoreLabel;
    [SerializeField] private Text bestLabel;
    [SerializeField] private Text newBestLabel;
    [SerializeField] private Text comboLabel;
    [SerializeField] private Text speedLabel;
    [SerializeField] private Text runSeedLabel;
    [SerializeField] private Image nearMissPulse;
    [SerializeField] private GameObject deathOverlay;
    [Header("Death Summary")]
    [SerializeField] private Text deathSummaryScoreLabel;
    [SerializeField] private Text deathSummaryBestLabel;
    [SerializeField] private Text deathSummaryNearMissPeakLabel;
    [SerializeField] private Text deathSummaryTopSpeedLabel;
    [SerializeField] private Text deathSummaryDailySeedLabel;
    [SerializeField] private Text deathSummaryShareHintLabel;
    [SerializeField] private Text deathSummaryShareStatusLabel;
    [SerializeField] private Button deathSummaryShareButton;
    [SerializeField] private Color shareStatusOkColor = new Color(0.58f, 0.95f, 0.78f, 1f);
    [SerializeField] private Color shareStatusErrorColor = new Color(1f, 0.58f, 0.58f, 1f);
    [Header("Theme")]
    [SerializeField] private Color runStateColor = new Color(0.88f, 0.93f, 1f, 1f);
    [SerializeField] private Color slowStateColor = new Color(0.25f, 0.92f, 1f, 1f);
    [SerializeField] private Color crashStateColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color meterNormalColor = new Color(0.2f, 0.92f, 1f, 1f);
    [SerializeField] private Color meterWarningColor = new Color(1f, 0.47f, 0.2f, 1f);
    [SerializeField, Range(0f, 1f)] private float meterWarningThreshold = 0.22f;
    [Header("Run Feedback")]
    [SerializeField] private Color comboReadyColor = new Color(0.8f, 0.86f, 0.95f, 1f);
    [SerializeField] private Color comboActiveColor = new Color(0.38f, 0.95f, 1f, 1f);
    [SerializeField] private Color comboMaxColor = new Color(1f, 0.88f, 0.3f, 1f);
    [SerializeField, Min(1f)] private float comboVisualMaxMultiplier = 3f;
    [SerializeField] private Color speedLowColor = new Color(0.7f, 0.9f, 1f, 1f);
    [SerializeField] private Color speedHighColor = new Color(1f, 0.62f, 0.24f, 1f);
    [SerializeField, Min(0.1f)] private float speedFeedbackMin = 3.8f;
    [SerializeField, Min(0.1f)] private float speedFeedbackMax = 7.5f;
    [SerializeField] private Color nearMissPulseColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float nearMissPulseMaxAlpha = 0.2f;
    [SerializeField, Min(0.05f)] private float nearMissPulseDuration = 0.42f;
    [SerializeField, Range(0f, 1f)] private float nearMissScaleBoost = 0.22f;

    private int lastMeterCentiseconds = int.MinValue;
    private int lastScoreTenths = int.MinValue;
    private int lastBestTenths = int.MinValue;
    private int lastComboStreak = int.MinValue;
    private int lastComboMultiplierHundredths = int.MinValue;
    private int lastSpeedTenths = int.MinValue;
    private bool lastNewBestValue = true;
    private string lastStateText;
    private bool deathVisible;
    private float lastNearMissEventTime = float.NegativeInfinity;
    private float pulseStartUnscaledTime = float.NegativeInfinity;
    private Vector3 comboBaseScale = Vector3.one;
    private Vector3 scoreBaseScale = Vector3.one;
    private Color scoreBaseColor = Color.white;

    private int peakRunSpeedTenths;
    private string peakRunSpeedTier = DefaultSpeedTier;

    private bool shareCaptureInProgress;
    private string shareHintText = DefaultShareHintText;
    private string shareStatusText = DefaultShareStatusText;
    private Button boundShareButton;

    private struct RunSummarySnapshot
    {
        public string ScoreText;
        public string BestText;
        public int PeakNearMiss;
        public string TopSpeedText;
        public string SeedText;
        public bool IsNewBest;
        public string TimeStamp;
    }

    private void Awake()
    {
        if (timeAbility == null)
        {
            timeAbility = FindFirstObjectByType<TimeAbility>();
        }

        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        CaptureVisualDefaults();
        BindShareButton();
        ResetRunSummaryTracking();
        ResetShareFeedback();
        ApplyDeathOverlay(false);
        RefreshHud(force: true);
        RefreshDeathSummary(force: true);
        RefreshRunContextLabel();
    }

    private void OnDestroy()
    {
        UnbindShareButton();
    }

    public void Configure(TimeAbility ability, Text meter, Image fill, Text state)
    {
        Configure(
            ability,
            scoreManager,
            meter,
            fill,
            state,
            scoreLabel,
            bestLabel,
            newBestLabel,
            deathOverlay,
            comboLabel,
            speedLabel,
            nearMissPulse);
    }

    public void Configure(
        TimeAbility ability,
        ScoreManager score,
        Text meter,
        Image fill,
        Text state,
        Text scoreValue,
        Text bestValue,
        Text newBestValue,
        GameObject deathView)
    {
        Configure(
            ability,
            score,
            meter,
            fill,
            state,
            scoreValue,
            bestValue,
            newBestValue,
            deathView,
            comboLabel,
            speedLabel,
            nearMissPulse);
    }

    public void Configure(
        TimeAbility ability,
        ScoreManager score,
        Text meter,
        Image fill,
        Text state,
        Text scoreValue,
        Text bestValue,
        Text newBestValue,
        GameObject deathView,
        Text comboValue,
        Text speedValue,
        Image nearMissPulseView)
    {
        timeAbility = ability;
        scoreManager = score;
        meterLabel = meter;
        meterFill = fill;
        stateLabel = state;
        scoreLabel = scoreValue;
        bestLabel = bestValue;
        newBestLabel = newBestValue;
        deathOverlay = deathView;
        comboLabel = comboValue;
        speedLabel = speedValue;
        nearMissPulse = nearMissPulseView;

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        CaptureVisualDefaults();
        BindShareButton();
        ResetCache();
        ResetRunSummaryTracking();
        ApplyDeathOverlay(deathVisible);
        RefreshHud(force: true);
    }

    public void ConfigureDeathSummary(
        Text scoreValue,
        Text bestValue,
        Text nearMissPeakValue,
        Text topSpeedValue,
        Text dailySeedValue,
        Text shareHintValue,
        Text shareStatusValue,
        Button shareButton)
    {
        deathSummaryScoreLabel = scoreValue;
        deathSummaryBestLabel = bestValue;
        deathSummaryNearMissPeakLabel = nearMissPeakValue;
        deathSummaryTopSpeedLabel = topSpeedValue;
        deathSummaryDailySeedLabel = dailySeedValue;
        deathSummaryShareHintLabel = shareHintValue;
        deathSummaryShareStatusLabel = shareStatusValue;
        deathSummaryShareButton = shareButton;

        BindShareButton();
        ResetShareFeedback();
        RefreshDeathSummary(force: true);
    }

    public void ConfigureRunContextLabel(Text runSeedValue)
    {
        runSeedLabel = runSeedValue;
        RefreshRunContextLabel();
    }

    private void Update()
    {
        RefreshHud(force: false);
        RefreshRunContextLabel();
        HandleShareShortcut();
    }

    public void ShowDeath()
    {
        deathVisible = true;
        pulseStartUnscaledTime = float.NegativeInfinity;
        ApplyPulseVisual(0f);
        ResetShareFeedback();
        ApplyDeathOverlay(true);
        SetStateText(StateCrash);
        RefreshDeathSummary(force: true);
    }

    public void HideDeath()
    {
        deathVisible = false;
        ApplyDeathOverlay(false);
        SetStateText(StateRun);
        ResetRunSummaryTracking();
        ResetShareFeedback();
        RefreshDeathSummary(force: true);
    }

    private void RefreshHud(bool force)
    {
        RefreshMeter(force);
        RefreshScore(force);
        RefreshState(force);
        RefreshCombo(force);
        RefreshSpeed(force);
        RefreshNearMissPulse();

        if (deathVisible)
        {
            RefreshDeathSummary(force);
        }
    }

    private void RefreshMeter(bool force)
    {
        float ratio = 0f;
        int centiseconds = 0;
        if (timeAbility != null)
        {
            if (timeAbility.MaxSlowSeconds > 0f)
            {
                ratio = Mathf.Clamp01(timeAbility.RemainingSeconds / timeAbility.MaxSlowSeconds);
            }

            centiseconds = Mathf.RoundToInt(Mathf.Max(0f, timeAbility.RemainingSeconds) * 100f);
        }

        if (meterFill != null && (force || !Mathf.Approximately(meterFill.fillAmount, ratio)))
        {
            meterFill.fillAmount = ratio;
        }

        if (meterFill != null)
        {
            float blend = meterWarningThreshold <= 0f ? 0f : Mathf.Clamp01((meterWarningThreshold - ratio) / meterWarningThreshold);
            meterFill.color = Color.Lerp(meterNormalColor, meterWarningColor, blend);
        }

        if (meterLabel != null && meterFill != null)
        {
            meterLabel.color = meterFill.color;
        }

        if (meterLabel != null && (force || centiseconds != lastMeterCentiseconds))
        {
            lastMeterCentiseconds = centiseconds;
            meterLabel.text = $"{centiseconds * 0.01f:0.00}s";
        }
    }

    private void RefreshScore(bool force)
    {
        if (scoreManager == null)
        {
            return;
        }

        int scoreTenths = Mathf.RoundToInt(Mathf.Max(0f, scoreManager.CurrentScore) * 10f);
        int bestTenths = Mathf.RoundToInt(Mathf.Max(0f, scoreManager.BestScore) * 10f);
        bool isNewBest = scoreManager.IsCurrentRunNewBest;

        if (scoreLabel != null && (force || scoreTenths != lastScoreTenths))
        {
            lastScoreTenths = scoreTenths;
            scoreLabel.text = FormatScoreTenths(scoreTenths);
        }

        if (bestLabel != null && (force || bestTenths != lastBestTenths))
        {
            lastBestTenths = bestTenths;
            bestLabel.text = FormatScoreTenths(bestTenths);
        }

        if (newBestLabel != null && (force || isNewBest != lastNewBestValue))
        {
            lastNewBestValue = isNewBest;
            newBestLabel.text = isNewBest ? NewBestText : string.Empty;
        }
    }

    private void RefreshState(bool force)
    {
        string state = StateRun;
        if (deathVisible || (GameManager.Instance != null && !GameManager.Instance.IsPlaying))
        {
            state = StateCrash;
        }
        else if (timeAbility != null && timeAbility.SlowActive)
        {
            state = StateSlow;
        }

        if (force || state != lastStateText)
        {
            SetStateText(state);
        }
    }

    private void RefreshCombo(bool force)
    {
        if (comboLabel == null)
        {
            return;
        }

        int streak = scoreManager != null ? Mathf.Max(0, scoreManager.NearMissStreak) : 0;
        float multiplier = scoreManager != null ? Mathf.Max(1f, scoreManager.NearMissMultiplier) : 1f;
        int multiplierHundredths = Mathf.RoundToInt(multiplier * 100f);

        if (force || streak != lastComboStreak || multiplierHundredths != lastComboMultiplierHundredths)
        {
            lastComboStreak = streak;
            lastComboMultiplierHundredths = multiplierHundredths;
            comboLabel.text = streak > 0 ? $"COMBO x{multiplier:0.00}" : ComboReadyText;
        }

        comboLabel.color = GetComboColor(streak, multiplier);
    }

    private void RefreshSpeed(bool force)
    {
        if (speedLabel == null)
        {
            return;
        }

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        float speed = obstacleSpawner != null ? Mathf.Max(0f, obstacleSpawner.CurrentSpeed) : 0f;
        float speedRange = Mathf.Max(0.001f, speedFeedbackMax - speedFeedbackMin);
        float normalized = Mathf.Clamp01((speed - speedFeedbackMin) / speedRange);
        int speedTenths = Mathf.RoundToInt(speed * 10f);

        if (force || speedTenths != lastSpeedTenths)
        {
            lastSpeedTenths = speedTenths;
            speedLabel.text = $"{GetSpeedTier(normalized)} {speed:0.0}";
        }

        if (!deathVisible && speedTenths >= peakRunSpeedTenths)
        {
            peakRunSpeedTenths = speedTenths;
            peakRunSpeedTier = GetSpeedTier(normalized);
        }

        speedLabel.color = Color.Lerp(speedLowColor, speedHighColor, normalized);
    }

    private void RefreshNearMissPulse()
    {
        if (scoreManager != null)
        {
            float eventTime = scoreManager.LastNearMissUnscaledTime;
            if (!float.IsNegativeInfinity(eventTime) && eventTime > (lastNearMissEventTime + NearMissEventEpsilon))
            {
                lastNearMissEventTime = eventTime;
                pulseStartUnscaledTime = Time.unscaledTime;
            }
        }

        if (deathVisible || float.IsNegativeInfinity(pulseStartUnscaledTime))
        {
            ApplyPulseVisual(0f);
            return;
        }

        float safeDuration = Mathf.Max(0.05f, nearMissPulseDuration);
        float elapsed = Mathf.Max(0f, Time.unscaledTime - pulseStartUnscaledTime);
        if (elapsed >= safeDuration)
        {
            pulseStartUnscaledTime = float.NegativeInfinity;
            ApplyPulseVisual(0f);
            return;
        }

        float t = elapsed / safeDuration;
        ApplyPulseVisual(t);
    }

    private void SetStateText(string state)
    {
        lastStateText = state;
        if (stateLabel != null)
        {
            stateLabel.text = state;
            stateLabel.color = GetStateColor(state);
        }
    }

    private void ApplyDeathOverlay(bool visible)
    {
        if (deathOverlay != null && deathOverlay.activeSelf != visible)
        {
            deathOverlay.SetActive(visible);
        }
    }

    private void ResetCache()
    {
        lastMeterCentiseconds = int.MinValue;
        lastScoreTenths = int.MinValue;
        lastBestTenths = int.MinValue;
        lastComboStreak = int.MinValue;
        lastComboMultiplierHundredths = int.MinValue;
        lastSpeedTenths = int.MinValue;
        lastNewBestValue = true;
        lastStateText = null;
        lastNearMissEventTime = float.NegativeInfinity;
        pulseStartUnscaledTime = float.NegativeInfinity;
        ApplyPulseVisual(0f);
    }

    private void ResetRunSummaryTracking()
    {
        peakRunSpeedTenths = 0;
        peakRunSpeedTier = DefaultSpeedTier;
    }

    private void RefreshDeathSummary(bool force)
    {
        if (!deathVisible && !force)
        {
            return;
        }

        int scoreTenths = scoreManager != null ? Mathf.RoundToInt(Mathf.Max(0f, scoreManager.CurrentScore) * 10f) : 0;
        int bestTenths = scoreManager != null ? Mathf.RoundToInt(Mathf.Max(0f, scoreManager.BestScore) * 10f) : 0;
        int peakNearMiss = scoreManager != null ? Mathf.Max(0, scoreManager.PeakNearMissStreak) : 0;
        float peakSpeed = Mathf.Max(0f, peakRunSpeedTenths * 0.1f);
        string seedText = ResolveSeedText();

        if (deathSummaryScoreLabel != null)
        {
            deathSummaryScoreLabel.text = FormatScoreTenths(scoreTenths);
        }

        if (deathSummaryBestLabel != null)
        {
            deathSummaryBestLabel.text = FormatScoreTenths(bestTenths);
        }

        if (deathSummaryNearMissPeakLabel != null)
        {
            deathSummaryNearMissPeakLabel.text = peakNearMiss.ToString(CultureInfo.InvariantCulture);
        }

        if (deathSummaryTopSpeedLabel != null)
        {
            deathSummaryTopSpeedLabel.text = $"{peakRunSpeedTier} {peakSpeed:0.0}";
        }

        if (deathSummaryDailySeedLabel != null)
        {
            deathSummaryDailySeedLabel.text = seedText;
        }

        if (deathSummaryShareHintLabel != null)
        {
            deathSummaryShareHintLabel.text = shareHintText;
        }

        if (deathSummaryShareStatusLabel != null)
        {
            deathSummaryShareStatusLabel.text = shareStatusText;
        }

        if (deathSummaryShareButton != null)
        {
            deathSummaryShareButton.interactable = deathVisible && !shareCaptureInProgress;
        }
    }

    private void RefreshRunContextLabel()
    {
        if (runSeedLabel == null)
        {
            return;
        }

        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            runSeedLabel.text = "RANDOM";
            return;
        }

        if (manager.CurrentRunMode == RunSeedMode.DailyChallenge)
        {
            if (!string.IsNullOrEmpty(manager.CurrentChallengeDateKey))
            {
                runSeedLabel.text = $"DAILY {manager.CurrentChallengeDateKey} #{manager.CurrentRunSeed}";
            }
            else
            {
                runSeedLabel.text = $"DAILY #{manager.CurrentRunSeed}";
            }

            return;
        }

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        if (obstacleSpawner != null && obstacleSpawner.IsDeterministicSimulationActive)
        {
            runSeedLabel.text = $"SEEDED #{obstacleSpawner.CurrentDeterministicSeed}";
            return;
        }

        runSeedLabel.text = "RANDOM";
    }

    private void HandleShareShortcut()
    {
        if (!deathVisible || shareCaptureInProgress)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.F12))
        {
            TrySaveShareArtifacts();
        }
    }

    private void BindShareButton()
    {
        if (boundShareButton == deathSummaryShareButton)
        {
            return;
        }

        UnbindShareButton();
        boundShareButton = deathSummaryShareButton;
        if (boundShareButton == null)
        {
            return;
        }

        boundShareButton.onClick.RemoveListener(OnShareButtonPressed);
        boundShareButton.onClick.AddListener(OnShareButtonPressed);
    }

    private void UnbindShareButton()
    {
        if (boundShareButton == null)
        {
            return;
        }

        boundShareButton.onClick.RemoveListener(OnShareButtonPressed);
        boundShareButton = null;
    }

    private void OnShareButtonPressed()
    {
        TrySaveShareArtifacts();
    }

    private void TrySaveShareArtifacts()
    {
        if (!deathVisible || shareCaptureInProgress)
        {
            return;
        }

        RunSummarySnapshot snapshot = BuildRunSummarySnapshot();
        StartCoroutine(SaveShareArtifactsCoroutine(snapshot));
    }

    private RunSummarySnapshot BuildRunSummarySnapshot()
    {
        int scoreTenths = scoreManager != null ? Mathf.RoundToInt(Mathf.Max(0f, scoreManager.CurrentScore) * 10f) : 0;
        int bestTenths = scoreManager != null ? Mathf.RoundToInt(Mathf.Max(0f, scoreManager.BestScore) * 10f) : 0;

        return new RunSummarySnapshot
        {
            ScoreText = FormatScoreTenths(scoreTenths),
            BestText = FormatScoreTenths(bestTenths),
            PeakNearMiss = scoreManager != null ? Mathf.Max(0, scoreManager.PeakNearMissStreak) : 0,
            TopSpeedText = $"{peakRunSpeedTier} {Mathf.Max(0f, peakRunSpeedTenths * 0.1f):0.0}",
            SeedText = ResolveSeedText(),
            IsNewBest = scoreManager != null && scoreManager.IsCurrentRunNewBest,
            TimeStamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
        };
    }

    private IEnumerator SaveShareArtifactsCoroutine(RunSummarySnapshot snapshot)
    {
        shareCaptureInProgress = true;
        SetShareStatus("Saving share assets...", isError: false);
        RefreshDeathSummary(force: true);

        string outputDirectory = Path.Combine(Application.persistentDataPath, "RunShares");
        string reportPath = string.Empty;
        string screenshotPath = string.Empty;

                    yield return new WaitForEndOfFrame();

        try
        {
            Directory.CreateDirectory(outputDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string filePrefix = $"run_{timestamp}";
            reportPath = Path.Combine(outputDirectory, filePrefix + ".txt");
            screenshotPath = Path.Combine(outputDirectory, filePrefix + ".png");

            string report = BuildShareReport(snapshot, screenshotPath);
            File.WriteAllText(reportPath, report, Encoding.UTF8);

            ScreenCapture.CaptureScreenshot(screenshotPath);

            SetShareStatus($"Saved {Path.GetFileName(reportPath)} and {Path.GetFileName(screenshotPath)}", isError: false);
            shareHintText = $"Output: {outputDirectory}";
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"UIHud share export failed: {ex.Message}", this);
            SetShareStatus("Share export failed", isError: true);
        }

        shareCaptureInProgress = false;
        RefreshDeathSummary(force: true);
    }

    private string BuildShareReport(RunSummarySnapshot snapshot, string screenshotPath)
    {
        StringBuilder builder = new StringBuilder(256);
        builder.AppendLine("ONE SECOND LEFT - RUN SUMMARY");
        builder.AppendLine(snapshot.TimeStamp);
        builder.AppendLine();
        builder.AppendLine(BuildShareCaption(snapshot));
        builder.AppendLine();
        builder.AppendLine($"Score: {snapshot.ScoreText}");
        builder.AppendLine($"Best: {snapshot.BestText}");
        builder.AppendLine($"Peak near-miss streak: {snapshot.PeakNearMiss}");
        builder.AppendLine($"Top speed tier: {snapshot.TopSpeedText}");
        builder.AppendLine($"Daily seed: {snapshot.SeedText}");
        builder.AppendLine($"New best: {(snapshot.IsNewBest ? "YES" : "NO")}");
        builder.AppendLine($"Screenshot: {screenshotPath}");
        return builder.ToString();
    }

    private static string BuildShareCaption(RunSummarySnapshot snapshot)
    {
        string bestSuffix = snapshot.IsNewBest ? " | NEW BEST" : string.Empty;
        return $"I scored {snapshot.ScoreText} in One Second Left | {snapshot.TopSpeedText} | Combo Peak {snapshot.PeakNearMiss} | Seed {snapshot.SeedText}{bestSuffix}";
    }

    private void ResetShareFeedback()
    {
        shareHintText = DefaultShareHintText;
        shareStatusText = DefaultShareStatusText;
        if (deathSummaryShareHintLabel != null)
        {
            deathSummaryShareHintLabel.text = shareHintText;
        }

        if (deathSummaryShareStatusLabel != null)
        {
            deathSummaryShareStatusLabel.color = shareStatusOkColor;
        }
    }

    private void SetShareStatus(string status, bool isError)
    {
        shareStatusText = string.IsNullOrEmpty(status) ? DefaultShareStatusText : status;
        if (deathSummaryShareStatusLabel != null)
        {
            deathSummaryShareStatusLabel.color = isError ? shareStatusErrorColor : shareStatusOkColor;
        }
    }

    private string ResolveSeedText()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null && manager.CurrentRunMode == RunSeedMode.DailyChallenge)
        {
            if (!string.IsNullOrEmpty(manager.CurrentChallengeDateKey))
            {
                return $"{manager.CurrentChallengeDateKey} #{manager.CurrentRunSeed}";
            }

            return manager.CurrentRunSeed.ToString(CultureInfo.InvariantCulture);
        }

        if (obstacleSpawner == null)
        {
            obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        }

        if (obstacleSpawner != null && obstacleSpawner.TryGetConfiguredSeed(out int seed))
        {
            return seed.ToString(CultureInfo.InvariantCulture);
        }

        return DefaultSeedText;
    }

    private static string FormatScoreTenths(int tenths)
    {
        if (tenths >= 1000)
        {
            int whole = tenths / 10;
            return whole.ToString("N0", CultureInfo.InvariantCulture);
        }

        return (tenths * 0.1f).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private Color GetStateColor(string state)
    {
        if (state == StateSlow)
        {
            return slowStateColor;
        }

        if (state == StateCrash)
        {
            return crashStateColor;
        }

        return runStateColor;
    }

    private Color GetComboColor(int streak, float multiplier)
    {
        if (streak <= 0)
        {
            return comboReadyColor;
        }

        float maxVisual = Mathf.Max(1f, comboVisualMaxMultiplier);
        float normalized = Mathf.InverseLerp(1f, maxVisual, multiplier);
        return Color.Lerp(comboActiveColor, comboMaxColor, normalized);
    }

    private void CaptureVisualDefaults()
    {
        if (comboLabel != null)
        {
            comboBaseScale = comboLabel.rectTransform.localScale;
        }

        if (scoreLabel != null)
        {
            scoreBaseScale = scoreLabel.rectTransform.localScale;
            scoreBaseColor = scoreLabel.color;
        }

        ApplyPulseVisual(0f);
    }

    private void ApplyPulseVisual(float normalizedTime)
    {
        float clamped = Mathf.Clamp01(normalizedTime);
        float envelope = 1f - clamped;
        float wave = Mathf.Sin(clamped * Mathf.PI);
        float intensity = wave * envelope;

        if (nearMissPulse != null)
        {
            Color tint = nearMissPulseColor;
            tint.a = Mathf.Max(0f, nearMissPulseMaxAlpha) * intensity;
            nearMissPulse.color = tint;
        }

        if (comboLabel != null)
        {
            float scale = 1f + (Mathf.Max(0f, nearMissScaleBoost) * intensity);
            comboLabel.rectTransform.localScale = comboBaseScale * scale;
            comboLabel.color = Color.Lerp(comboLabel.color, nearMissPulseColor, intensity);
        }

        if (scoreLabel != null)
        {
            float scoreScale = 1f + ((Mathf.Max(0f, nearMissScaleBoost) * 0.6f) * intensity);
            scoreLabel.rectTransform.localScale = scoreBaseScale * scoreScale;
            scoreLabel.color = Color.Lerp(scoreBaseColor, nearMissPulseColor, intensity * 0.4f);
        }
    }

    private static string GetSpeedTier(float normalizedSpeed)
    {
        if (normalizedSpeed >= 0.85f)
        {
            return "HYPER";
        }

        if (normalizedSpeed >= 0.55f)
        {
            return "BLAZE";
        }

        if (normalizedSpeed >= 0.25f)
        {
            return "RUSH";
        }

        return DefaultSpeedTier;
    }
}
