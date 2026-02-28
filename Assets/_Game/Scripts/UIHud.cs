using UnityEngine;
using UnityEngine.UI;

public class UIHud : MonoBehaviour
{
    private const string StateRun = "RUN";
    private const string StateSlow = "SLOW";
    private const string StateCrash = "CRASH";
    private const string NewBestText = "NEW BEST";

    [SerializeField] private TimeAbility timeAbility;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private Text meterLabel;
    [SerializeField] private Image meterFill;
    [SerializeField] private Text stateLabel;
    [SerializeField] private Text scoreLabel;
    [SerializeField] private Text bestLabel;
    [SerializeField] private Text newBestLabel;
    [SerializeField] private GameObject deathOverlay;

    private int lastMeterCentiseconds = int.MinValue;
    private int lastScoreValue = int.MinValue;
    private int lastBestValue = int.MinValue;
    private bool lastNewBestValue = true;
    private string lastStateText;
    private bool deathVisible;

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

        ApplyDeathOverlay(false);
        RefreshHud(force: true);
    }

    public void Configure(TimeAbility ability, Text meter, Image fill, Text state)
    {
        Configure(ability, scoreManager, meter, fill, state, scoreLabel, bestLabel, newBestLabel, deathOverlay);
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
        timeAbility = ability;
        scoreManager = score;
        meterLabel = meter;
        meterFill = fill;
        stateLabel = state;
        scoreLabel = scoreValue;
        bestLabel = bestValue;
        newBestLabel = newBestValue;
        deathOverlay = deathView;

        ResetCache();
        ApplyDeathOverlay(deathVisible);
        RefreshHud(force: true);
    }

    private void Update()
    {
        RefreshHud(force: false);
    }

    public void ShowDeath()
    {
        deathVisible = true;
        ApplyDeathOverlay(true);
        SetStateText(StateCrash);
    }

    public void HideDeath()
    {
        deathVisible = false;
        ApplyDeathOverlay(false);
        SetStateText(StateRun);
    }

    private void RefreshHud(bool force)
    {
        RefreshMeter(force);
        RefreshScore(force);
        RefreshState(force);
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

        int scoreValue = Mathf.RoundToInt(scoreManager.CurrentScore);
        int bestValue = Mathf.RoundToInt(scoreManager.BestScore);
        bool isNewBest = scoreManager.IsCurrentRunNewBest;

        if (scoreLabel != null && (force || scoreValue != lastScoreValue))
        {
            lastScoreValue = scoreValue;
            scoreLabel.text = scoreValue.ToString();
        }

        if (bestLabel != null && (force || bestValue != lastBestValue))
        {
            lastBestValue = bestValue;
            bestLabel.text = bestValue.ToString();
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

    private void SetStateText(string state)
    {
        lastStateText = state;
        if (stateLabel != null)
        {
            stateLabel.text = state;
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
        lastScoreValue = int.MinValue;
        lastBestValue = int.MinValue;
        lastNewBestValue = true;
        lastStateText = null;
    }
}
