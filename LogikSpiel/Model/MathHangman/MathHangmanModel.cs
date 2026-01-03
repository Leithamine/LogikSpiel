// LogikSpiel/Model/MathHangman/MathHangmanModel.cs
#nullable enable

namespace LogikSpiel.Model.MathHangman;

public static class MathHangmanDifficulty
{
    // Fixer Bereich für alle Level
    public static (int Min, int Max) Range(string key) => (10, 100000);

    // Jeder Sieg bringt 50 Coins
    public static int RewardCoins(string key) => 50;

    // Multiplikator auf 1.0, damit Basispreise (10) unverändert bleiben
    public static double PriceMultiplier(string key) => 1.0;
}

public sealed record NumberProperty(string Key, string Article, string KidDescription, string Examples);

public sealed record ShopHint(string Id, string Title, string Description, int BasePrice);