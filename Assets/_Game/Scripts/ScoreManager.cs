using UnityEngine;
using System.Collections.Generic;

public class ScoreManager : MonoBehaviour
{
    private const string BestScorePlayerPrefsKey = "OneSecondLeft.BestScore";

    [SerializeField, Min(0f)] private float pointsPerUnit = 1f;
    [SerializeField, Min(0f)] private float nearMissBonusPoints = 25f;
    [SerializeField, Min(0f)] private float nearMissComboStep = 0.25f;
    [SerializeField, Min(1f)] private float maxNearMissMultiplier = 3f;
    [SerializeField, Min(0f)] private float nearMissComboDecayDistance = 12f;

    public float CurrentScore { get; private set; }
    public float BestScore { get; private set; }
    public bool IsCurrentRunNewBest { get; private set; }
    public bool IsNewBestRun => IsCurrentRunNewBest;
    public int NearMissStreak { get; private set; }
    public int PeakNearMissStreak { get; private set; }
    public float NearMissMultiplier { get; private set; } = 1f;
    public float LastNearMissUnscaledTime { get; private set; }

    private float runStartBestScore;
    private float distanceSinceLastNearMiss;
    private bool hasEmittedNewBestThisRun;

    private void Awake()
    {
        BestScore = Mathf.Max(0f, PlayerPrefs.GetFloat(BestScorePlayerPrefsKey, 0f));
        ResetRun();
    }

    public void ResetRun()
    {
        CurrentScore = 0f;
        runStartBestScore = BestScore;
        IsCurrentRunNewBest = false;
        hasEmittedNewBestThisRun = false;
        PeakNearMissStreak = 0;
        ResetNearMissCombo();
        LastNearMissUnscaledTime = float.NegativeInfinity;
    }

    public void AddDistance(float amount)
    {
        if (!IsFinitePositive(amount))
        {
            return;
        }

        CurrentScore += amount * Mathf.Max(0f, pointsPerUnit);
        UpdateBestStateAndEmitTransition();

        if (NearMissStreak <= 0 || nearMissComboDecayDistance <= 0f)
        {
            return;
        }

        distanceSinceLastNearMiss += amount;
        if (distanceSinceLastNearMiss >= nearMissComboDecayDistance)
        {
            ResetNearMissCombo(reason: "decay", emitIfChanged: true);
        }
    }

    public void AddNearMissBonus(float nearMissDistance = float.NaN)
    {
        if (!IsFinitePositive(nearMissBonusPoints))
        {
            return;
        }

        NearMissStreak++;
        PeakNearMissStreak = Mathf.Max(PeakNearMissStreak, NearMissStreak);
        float comboStep = Mathf.Max(0f, nearMissComboStep);
        float cap = Mathf.Max(1f, maxNearMissMultiplier);
        NearMissMultiplier = Mathf.Min(cap, 1f + ((NearMissStreak - 1) * comboStep));
        distanceSinceLastNearMiss = 0f;

        CurrentScore += nearMissBonusPoints * NearMissMultiplier;
        UpdateBestStateAndEmitTransition();
        LastNearMissUnscaledTime = Time.unscaledTime;
        EmitComboStateAnalytics("gain");
        EmitNearMissAnalytics(nearMissDistance);
    }

    public void CommitRunIfBest()
    {
        if (!IsCurrentRunNewBest)
        {
            return;
        }

        BestScore = CurrentScore;
        PlayerPrefs.SetFloat(BestScorePlayerPrefsKey, BestScore);
        PlayerPrefs.Save();
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private void ResetNearMissCombo(string reason = "reset", bool emitIfChanged = false)
    {
        bool hadCombo = NearMissStreak > 0 || NearMissMultiplier > 1f;
        NearMissStreak = 0;
        NearMissMultiplier = 1f;
        distanceSinceLastNearMiss = 0f;

        if (emitIfChanged && hadCombo)
        {
            EmitComboStateAnalytics(reason);
        }
    }

    private void UpdateBestStateAndEmitTransition()
    {
        bool wasNewBest = IsCurrentRunNewBest;
        IsCurrentRunNewBest = CurrentScore > runStartBestScore;

        if (!IsCurrentRunNewBest || wasNewBest || hasEmittedNewBestThisRun)
        {
            return;
        }

        hasEmittedNewBestThisRun = true;

        Dictionary<string, object> fields = new Dictionary<string, object>(6)
        {
            ["score"] = CurrentScore,
            ["previous_best"] = runStartBestScore
        };

        GameManager.Instance?.PopulateRunContext(fields);
        GameplayAnalytics.Track("new_best_reached", fields);
    }

    private void EmitComboStateAnalytics(string reason)
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(8)
        {
            ["near_miss_streak"] = NearMissStreak,
            ["multiplier"] = NearMissMultiplier,
            ["reason"] = reason
        };

        GameManager.Instance?.PopulateRunContext(fields);
        GameplayAnalytics.Track("combo_state_changed", fields);
    }

    private void EmitNearMissAnalytics(float nearMissDistance)
    {
        Dictionary<string, object> fields = new Dictionary<string, object>(8)
        {
            ["streak"] = NearMissStreak,
            ["multiplier"] = NearMissMultiplier,
            ["score"] = CurrentScore
        };

        if (!float.IsNaN(nearMissDistance) && !float.IsInfinity(nearMissDistance))
        {
            fields["near_miss_distance"] = nearMissDistance;
        }

        GameManager.Instance?.PopulateRunContext(fields);
        GameplayAnalytics.Track("near_miss", fields);
    }
}
