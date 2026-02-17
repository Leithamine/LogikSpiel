// LogikSpiel/Services/MathHangman/MathHangmanService.cs
#nullable enable
using LogikSpiel.Model.MathHangman;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.Services.MathHangman;

public sealed class MathHangmanService : IMathHangmanService
{
    public int GenerateSecretNumber(string difficultyKey)
    {
        var (min, max) = MathHangmanDifficulty.Range(difficultyKey);
        return Random.Shared.Next(min, max + 1);
    }

    public IReadOnlyList<NumberProperty> GetAllTrueProperties(int n)
    {
        var list = new List<NumberProperty>();
        foreach (var def in PropertyDefs)
        {
            try { if (def.Test(n)) list.Add(new NumberProperty(def.Key, def.Article, def.KidDesc, def.Examples)); }
            catch { /* Fehler ignorieren */ }
        }
        return list;
    }

    private sealed record PropDef(string Key, string Article, string KidDesc, string Examples, Func<int, bool> Test);

    // KORRIGIERT: Lazy Loading ohne unnötige Prüfung
    private static readonly Lazy<IReadOnlyList<PropDef>> _propertyDefs = new(BuildDefs);
    private static IReadOnlyList<PropDef> PropertyDefs => _propertyDefs.Value;

    private static IReadOnlyList<PropDef> BuildDefs()
    {
        // Die Localization wird in App.xaml.cs initialisiert
        // Lazy sorgt dafür, dass dies nur einmal aufgerufen wird
        return new List<PropDef>
        {
            Def("Prime", IsPrime),
            Def("Composite", n => n > 1 && !IsPrime(n)),
            Def("Even", n => n % 2 == 0),
            Def("Odd", n => n % 2 != 0),
            Def("Perfect", IsPerfect),
            Def("Square", IsSquare),
            Def("Cube", IsCube),
            Def("Triangular", IsTriangular),
            Def("Fibonacci", IsFibonacci),
            Def("Palindrome", IsPalindrome),
            Def("Harshad", IsHarshad),
            Def("Happy", IsHappy),
            Def("Automorphic", IsAutomorphic),
            Def("Armstrong", IsArmstrong),
            Def("Abundant", IsAbundant),
            Def("Deficient", IsDeficient),
            Def("Semiprime", IsSemiprime),
            Def("Factorial", IsFactorial),
            Def("Strong", IsStrongNumber),
            Def("SophieGermain", IsSophieGermain),
            Def("Mersenne", IsMersenne)
        };
    }

    private static PropDef Def(string keySuffix, Func<int, bool> test)
    {
        return new PropDef(
            LocalizationService.GetString($"MathHangman_Property_{keySuffix}_Key"),
            LocalizationService.GetString($"MathHangman_Property_{keySuffix}_Article"),
            LocalizationService.GetString($"MathHangman_Property_{keySuffix}_KidDesc"),
            LocalizationService.GetString($"MathHangman_Property_{keySuffix}_Examples"),
            test);
    }

    // Hilfsmethoden (unverändert)
    private static bool IsPrime(int n) { if (n < 2) return false; for (int i = 2; i * i <= n; i++) if (n % i == 0) return false; return true; }
    private static int ProperDivisorSum(int n) { int sum = 0; for (int i = 1; i < n; i++) if (n % i == 0) sum += i; return sum; }
    private static bool IsPerfect(int n) => n > 1 && ProperDivisorSum(n) == n;
    private static bool IsSquare(int n) => n >= 0 && Math.Sqrt(n) % 1 == 0;
    private static bool IsCube(int n) { int r = (int)Math.Round(Math.Pow(n, 1.0 / 3.0)); return r * r * r == n; }
    private static bool IsTriangular(int n) => IsSquare(8 * n + 1);
    private static bool IsFibonacci(int n) => IsSquare(5 * n * n + 4) || IsSquare(5 * n * n - 4);
    private static bool IsPalindrome(int n) { string s = n.ToString(); return s.SequenceEqual(s.Reverse()); }
    private static bool IsHarshad(int n) { int s = n.ToString().Sum(c => c - '0'); return s > 0 && n % s == 0; }
    private static bool IsHappy(int n) { var seen = new HashSet<int>(); while (n != 1 && seen.Add(n)) n = n.ToString().Sum(c => (c - '0') * (c - '0')); return n == 1; }
    private static bool IsAbundant(int n) => ProperDivisorSum(n) > n;
    private static bool IsDeficient(int n) => ProperDivisorSum(n) < n;
    private static bool IsArmstrong(int n) { string s = n.ToString(); return s.Sum(c => Math.Pow(c - '0', s.Length)) == n; }
    private static bool IsSemiprime(int n) { int count = 0, temp = n; for (int i = 2; i * i <= temp && count < 2; i++) { while (temp % i == 0) { temp /= i; count++; } } if (temp > 1) count++; return count == 2; }
    private static bool IsAutomorphic(int n) => ((long)n * n).ToString().EndsWith(n.ToString());
    private static bool IsFactorial(int n)
    {
        if (n < 1) return false;

        int f = 1;
        int i = 1;
        while (f < n && i < 20)
        {
            i++;
            f *= i;
        }

        return f == n;
    }

    private static bool IsStrongNumber(int n)
    {
        if (n < 0) return false;

        int Fact(int x)
        {
            int result = 1;
            for (int i = 2; i <= x; i++) result *= i;
            return result;
        }

        return n.ToString().Sum(c => Fact(c - '0')) == n;
    }
    private static bool IsSophieGermain(int n) => IsPrime(n) && IsPrime(2 * n + 1);
    private static bool IsMersenne(int n)
    {
        if (!IsPrime(n)) return false;
        long np1 = (long)n + 1;
        if (np1 <= 0) return false;
        return (np1 & (np1 - 1)) == 0;
    }
}
