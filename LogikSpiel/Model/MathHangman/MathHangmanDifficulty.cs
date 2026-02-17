// LogikSpiel/Model/MathHangman/MathHangmanDifficulty.cs
#nullable enable
using System;

namespace LogikSpiel.Model.MathHangman;

public static class MathHangmanDifficulty
{
    public const int MinSecret = 1;
    public const int MaxSecret = 10000;

    // Fixer Bereich für alle Level
    public static (int Min, int Max) Range(string key) => (MinSecret, MaxSecret);

    public static int RangeDelta(int level) => level switch
    {
        <= 250 => 50,
        <= 750 => 150,
        <= 1250 => 300,
        _ => 500
    };

    public static (int Min, int Max) VisibleRange(int secret, int level)
    {
        int delta = RangeDelta(level);
        int min = Math.Max(MinSecret, secret - delta);
        int max = Math.Min(MaxSecret, secret + delta);
        return (min, max);
    }

    public static int RewardCoins(string key) => key.ToLowerInvariant() switch
    {
        "easy" => 3,
        "normal" => 5,
        "hard" => 7,
        "master" => 10,
        _ => 5
    };

    // Multiplikator auf 1.0, damit Basispreise (10) unverändert bleiben
    public static double PriceMultiplier(string key) => 1.0;
}
