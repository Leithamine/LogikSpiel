public static class MathHangmanDifficulty
{
    public static (int Min, int Max) Range(string key) => key switch
    {
        _ => (10, 100000)
    };

    public static int RewardCoins(string key) => key switch
    {
        _ => 50
    };

    public static double PriceMultiplier(string key) => key switch
    {
        _ => 1.25
    };
}
