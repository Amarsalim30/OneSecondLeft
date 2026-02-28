public enum RunSeedMode
{
    Normal = 0,
    DailyChallenge = 1
}

public readonly struct RunSeedContext
{
    public RunSeedContext(RunSeedMode mode, int seed, string challengeDateKey, bool deterministic)
    {
        Mode = mode;
        Seed = seed;
        ChallengeDateKey = challengeDateKey ?? string.Empty;
        Deterministic = deterministic;
    }

    public RunSeedMode Mode { get; }
    public int Seed { get; }
    public string ChallengeDateKey { get; }
    public bool Deterministic { get; }
}
