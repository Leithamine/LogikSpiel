namespace LogikSpiel.Core;

public static class SeedHelper
{
    public static int CalculateSeed(string gameId, string difficulty, int level)
        => StableHash($"{gameId}:{difficulty}:{Math.Max(1, level)}");

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (var ch in s)
                h = h * 31 + ch;
            return h & 0x7fffffff;
        }
    }
}
