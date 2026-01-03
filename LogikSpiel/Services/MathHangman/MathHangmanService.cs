// LogikSpiel/Services/MathHangman/MathHangmanService.cs
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
            try { if (def.Test(n)) list.Add(new NumberProperty(def.Key, def.Article, def.KidDesc, def.Examples)); }
            catch { /* Fehler ignorieren */ }
        }
        return list;
    }

    private sealed record PropDef(string Key, string Article, string KidDesc, string Examples, Func<int, bool> Test);

    private static readonly IReadOnlyList<PropDef> PropertyDefs = BuildDefs();

    private static IReadOnlyList<PropDef> BuildDefs()
    {
        return new List<PropDef>
        {
            Def("Primzahl", "eine", "Nur durch 1 und sich selbst teilbar.", "2, 3, 5, 7, 11", IsPrime),
            Def("Zusammengesetzte Zahl", "eine", "Hat mehr als zwei Teiler.", "4, 6, 8, 9, 10", n => n > 1 && !IsPrime(n)),
            Def("Gerade Zahl", "eine", "Durch 2 teilbar.", "2, 4, 6, 8", n => n % 2 == 0),
            Def("Ungerade Zahl", "eine", "Nicht durch 2 teilbar.", "1, 3, 5, 7", n => n % 2 != 0),
            Def("Perfekte Zahl", "eine", "Summe der echten Teiler = Zahl selbst.", "6, 28, 496", IsPerfect),
            Def("Quadratzahl", "eine", "Ergebnis von n × n.", "1, 4, 9, 16, 25", IsSquare),
            Def("Kubikzahl", "eine", "Ergebnis von n × n × n.", "1, 8, 27, 64", IsCube),
            Def("Dreieckszahl", "eine", "Summe 1+2+...+n.", "1, 3, 6, 10, 15", IsTriangular),
            Def("Fibonacci-Zahl", "eine", "Summe der zwei vorherigen Zahlen.", "1, 2, 3, 5, 8", IsFibonacci),
            Def("Palindromzahl", "eine", "Vorwärts wie rückwärts gleich.", "11, 22, 121, 1331", IsPalindrome),
            Def("Harshad-Zahl", "eine", "Teilbar durch ihre Quersumme.", "18, 21, 72, 100", IsHarshad),
            Def("Glückliche Zahl", "eine", "Ziffernquadrat-Summe führt zu 1.", "1, 7, 10, 13, 19", IsHappy),
            Def("Automorphe Zahl", "eine", "Quadrat endet auf die Zahl.", "5, 6, 25, 76", IsAutomorphic),
            Def("Armstrong-Zahl", "eine", "Ziffern^Stellenzahl = Zahl.", "153, 370, 407", IsArmstrong),
            Def("Abundante Zahl", "eine", "Teilersumme > Zahl.", "12, 18, 20, 24", IsAbundant),
            Def("Defiziente Zahl", "eine", "Teilersumme < Zahl.", "8, 10, 14, 16", IsDeficient),
            Def("Semiprime", "eine", "Produkt aus genau zwei Primzahlen.", "4, 6, 9, 10, 14", IsSemiprime),
            Def("Faktorielle Zahl", "eine", "Zahl der Form n!.", "1, 2, 6, 24, 120", IsFactorial),
            Def("Starke Zahl", "eine", "Summe der Fakultäten ihrer Ziffern = Zahl.", "1, 2, 145", IsStrongNumber),
            Def("Sophie-Germain-Primzahl", "eine", "p prim und 2p+1 ist auch prim.", "2, 3, 5, 11, 23", IsSophieGermain),
            Def("Mersenne-Primzahl", "eine", "Primzahl der Form 2^p - 1.", "3, 7, 31, 127", IsMersenne)
        };
    }

    private static PropDef Def(string key, string art, string kid, string ex, Func<int, bool> t) => new(key, art, kid, ex, t);

    // Hilfsmethoden
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
    private static bool IsFactorial(int n) { int f = 1, i = 1; while (f < n) f *= ++i; return f == n; }
    private static bool IsStrongNumber(int n) { int Fact(int x) => x <= 1 ? 1 : x * Fact(x - 1); return n.ToString().Sum(c => Fact(c - '0')) == n; }
    private static bool IsSophieGermain(int n) => IsPrime(n) && IsPrime(2 * n + 1);
    private static bool IsMersenne(int n) { if (!IsPrime(n)) return false; double p = Math.Log2(n + 1); return p == (int)p && IsPrime((int)p); }
}