public static class MathHangmanDifficulty
{
    public static (int Min, int Max) Range(string key) => key switch
    {
        "easy" => (1, 99),
        "normal" => (10, 499),
        "hard" => (10, 999),
        "master" => (10, 9999),
        _ => (10, 499)
    };

    public static int RewardCoins(string key) => key switch
    {
        "easy" => 25,
        "normal" => 50,
        "hard" => 80,
        "master" => 120,
        _ => 50
    };

    public static double PriceMultiplier(string key) => key switch
    {
        "easy" => 1.0,
        "normal" => 1.25,
        "hard" => 1.55,
        "master" => 2.0,
        _ => 1.25
    };
}
