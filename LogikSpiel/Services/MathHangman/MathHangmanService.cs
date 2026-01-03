#nullable enable
using LogikSpiel.Model.MathHangman;

namespace LogikSpiel.Services.MathHangman;

public sealed class MathHangmanService : IMathHangmanService
{
    public int GenerateSecretNumber(int seed, string difficultyKey)
    {
        var (min, max) = MathHangmanDifficulty.Range(difficultyKey);
        return Random.Shared.Next(min, max + 1);
    }

    public IReadOnlyList<NumberProperty> GetAllTrueProperties(int n)
    {
        var list = new List<NumberProperty>();
        foreach (var def in PropertyDefs)
        {
            bool ok;
            try { ok = def.Test(n); }
            catch { ok = false; }

            if (ok)
                list.Add(new NumberProperty(def.Key, def.Article, def.KidDesc, def.Examples));
        }
        return list;
    }

    private sealed record PropDef(string Key, string Article, string KidDesc, string Examples, Func<int, bool> Test);

    private static readonly IReadOnlyList<PropDef> PropertyDefs = BuildDefs();

    private static IReadOnlyList<PropDef> BuildDefs()
    {
        return new List<PropDef>
        {
            Def("Primzahl", "eine", "Nur durch 1 und sich selbst teilbar.", "2, 3, 5, 7, 11, 13", IsPrime),
            Def("zusammengesetzte Zahl", "eine", "Hat mehr als zwei Teiler.", "4, 6, 8, 9, 10, 12", n => n > 1 && !IsPrime(n)),
            Def("perfekte Zahl", "eine", "Summe der echten Teiler = Zahl selbst.", "6, 28, 496", IsPerfect),
            Def("Quadratzahl", "eine", "Ergebnis von n × n.", "1, 4, 9, 16, 25, 36", IsSquare),
            Def("Kubikzahl", "eine", "Ergebnis von n × n × n.", "1, 8, 27, 64, 125", IsCube),
            Def("Dreieckszahl", "eine", "Summe 1+2+...+n.", "1, 3, 6, 10, 15, 21", IsTriangular),
            Def("Fibonacci-Zahl", "eine", "Jede Zahl = Summe der zwei vorherigen.", "1, 2, 3, 5, 8, 13", IsFibonacci),
            Def("Palindromzahl", "eine", "Vorwärts = rückwärts.", "11, 22, 121, 1331", IsPalindrome),
            Def("Harshad-Zahl", "eine", "Teilbar durch ihre Quersumme.", "18, 21, 72, 100", IsHarshad),
            Def("glückliche Zahl", "eine", "Ziffernquadrat-Summe führt zu 1.", "1, 7, 10, 13, 19", IsHappy),
            Def("gerade Zahl", "eine", "Durch 2 teilbar.", "2, 4, 6, 8, 10", n => n % 2 == 0),
            Def("ungerade Zahl", "eine", "Nicht durch 2 teilbar.", "1, 3, 5, 7, 9", n => n % 2 != 0),
            Def("durch 3 teilbar", "eine", "Rest bei Division durch 3 ist 0.", "3, 6, 9, 12, 15", n => n % 3 == 0),
            Def("durch 5 teilbar", "eine", "Endet auf 0 oder 5.", "5, 10, 15, 20, 25", n => n % 5 == 0),
            Def("durch 7 teilbar", "eine", "Division durch 7 geht auf.", "7, 14, 21, 28, 35", n => n % 7 == 0),
            Def("durch 11 teilbar", "eine", "Division durch 11 geht auf.", "11, 22, 33, 44, 55", n => n % 11 == 0),
            Def("Zwillingsprimzahl", "eine", "Primzahl mit Abstand 2 zu anderer Primzahl.", "3, 5, 7, 11, 13", IsTwinPrime),
            Def("Armstrong-Zahl", "eine", "Ziffern^Stellenzahl = Zahl.", "153, 370, 371, 407", IsArmstrong),
            Def("abundante Zahl", "eine", "Teilersumme > Zahl.", "12, 18, 20, 24", IsAbundant),
            Def("defiziente Zahl", "eine", "Teilersumme < Zahl.", "8, 10, 14, 16", IsDeficient),
            Def("Semiprime", "eine", "Produkt genau zweier Primzahlen.", "4, 6, 9, 10, 14", IsSemiprime),
            Def("automorphe Zahl", "eine", "Quadrat endet auf die Zahl.", "5, 6, 25, 76", IsAutomorphic),
            Def("Pronic-Zahl", "eine", "Form n×(n+1).", "2, 6, 12, 20, 30", IsPronic),
            Def("Pentagonalzahl", "eine", "Form n(3n-1)/2.", "1, 5, 12, 22, 35", IsPentagonal),
            Def("Hexagonalzahl", "eine", "Form n(2n-1).", "1, 6, 15, 28, 45", IsHexagonal),
        };
    }

    private static PropDef Def(string key, string article, string kid, string ex, Func<int, bool> test)
        => new(key, article, kid, ex, test);

    // Math helpers
    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        if (n % 2 == 0) return n == 2;
        var r = (int)Math.Sqrt(n);
        for (int i = 3; i <= r; i += 2)
            if (n % i == 0) return false;
        return true;
    }

    private static int ProperDivisorSum(int n)
    {
        if (n <= 1) return 0;
        int sum = 1;
        int r = (int)Math.Sqrt(n);
        for (int i = 2; i <= r; i++)
        {
            if (n % i == 0)
            {
                sum += i;
                int j = n / i;
                if (j != i) sum += j;
            }
        }
        return sum;
    }

    private static bool IsPerfect(int n) => n > 1 && ProperDivisorSum(n) == n;
    private static bool IsAbundant(int n) => n > 1 && ProperDivisorSum(n) > n;
    private static bool IsDeficient(int n) => n > 1 && ProperDivisorSum(n) < n;

    private static int[] Digits(int n) => Math.Abs(n).ToString().Select(c => c - '0').ToArray();
    private static int SumDigits(int n) => Digits(n).Sum();

    private static bool IsSquare(int n)
    {
        if (n < 0) return false;
        int r = (int)Math.Sqrt(n);
        return r * r == n;
    }

    private static bool IsCube(int n)
    {
        if (n < 0) return false;
        int r = (int)Math.Round(Math.Cbrt(n));
        return r * r * r == n;
    }

    private static bool IsTriangular(int n) => IsSquare(8 * n + 1);

    private static bool IsFibonacci(int n) => IsSquare(5 * n * n + 4) || IsSquare(5 * n * n - 4);

    private static bool IsPalindrome(int n)
    {
        var s = n.ToString();
        return s.SequenceEqual(s.Reverse());
    }

    private static bool IsHarshad(int n)
    {
        int s = SumDigits(n);
        return s > 0 && n % s == 0;
    }

    private static bool IsHappy(int n)
    {
        var seen = new HashSet<int>();
        int x = n;
        while (x != 1 && !seen.Contains(x))
        {
            seen.Add(x);
            x = Digits(x).Select(d => d * d).Sum();
        }
        return x == 1;
    }

    private static bool IsTwinPrime(int n) => IsPrime(n) && (IsPrime(n - 2) || IsPrime(n + 2));

    private static bool IsArmstrong(int n)
    {
        var ds = Digits(n);
        int k = ds.Length;
        int s = ds.Sum(d => (int)Math.Pow(d, k));
        return s == n;
    }

    private static bool IsSemiprime(int n)
    {
        int count = 0;
        int x = n;
        for (int p = 2; p * p <= x && count < 3; p++)
        {
            while (x % p == 0) { count++; x /= p; }
        }
        if (x > 1) count++;
        return count == 2;
    }

    private static bool IsAutomorphic(int n)
    {
        long sq = (long)n * n;
        return sq.ToString().EndsWith(n.ToString());
    }

    private static bool IsPronic(int n)
    {
        int r = (int)Math.Sqrt(n);
        return r * (r + 1) == n;
    }

    private static bool IsPentagonal(int n)
    {
        for (int k = 1; k <= 2000; k++)
        {
            int val = k * (3 * k - 1) / 2;
            if (val == n) return true;
            if (val > n) break;
        }
        return false;
    }

    private static bool IsHexagonal(int n)
    {
        for (int k = 1; k <= 2000; k++)
        {
            int val = k * (2 * k - 1);
            if (val == n) return true;
            if (val > n) break;
        }
        return false;
    }
}
