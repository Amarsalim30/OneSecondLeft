using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private const string BestScorePlayerPrefsKey = "OneSecondLeft.BestScore";

    [SerializeField, Min(0f)] private float pointsPerUnit = 1f;
    [SerializeField, Min(0f)] private float nearMissBonusPoints = 25f;

    public float CurrentScore { get; private set; }
    public float BestScore { get; private set; }
    public bool IsCurrentRunNewBest { get; private set; }
    public bool IsNewBestRun => IsCurrentRunNewBest;

    private float runStartBestScore;

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
    }

    public void AddDistance(float amount)
    {
        if (!IsFinitePositive(amount))
        {
            return;
        }

        CurrentScore += amount * Mathf.Max(0f, pointsPerUnit);
        IsCurrentRunNewBest = CurrentScore > runStartBestScore;
    }

    public void AddNearMissBonus()
    {
        if (!IsFinitePositive(nearMissBonusPoints))
        {
            return;
        }

        CurrentScore += nearMissBonusPoints;
        IsCurrentRunNewBest = CurrentScore > runStartBestScore;
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
}
