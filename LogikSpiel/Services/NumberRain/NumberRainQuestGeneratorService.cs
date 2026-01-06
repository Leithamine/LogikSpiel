#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model.NumberRain;

namespace LogikSpiel.Services.NumberRain;

public sealed class NumberRainQuestGeneratorService
{
    public NumberRainDifficultySettings GetSettings(string difficultyKey, int level)
    {
        int safeLevel = Math.Max(1, level);
        return difficultyKey.ToLowerInvariant() switch
        {
            "easy" => BuildSettings(1, 50, 6, safeLevel, 900, 55, 12, 18, 20, 90, 5, 8),
            "normal" => BuildSettings(1, 80, 8, safeLevel, 820, 75, 18, 25, 20, 90, 7, 10),
            "hard" => BuildSettings(1, 140, 12, safeLevel, 740, 95, 22, 35, 20, 90, 10, 15),
            "master" => BuildSettings(1, 220, 18, safeLevel, 660, 115, 30, 50, 20, 90, 12, 20),
            _ => BuildSettings(1, 70, 7, safeLevel, 860, 65, 15, 20, 20, 90, 6, 9)
        };
    }

    public NumberRainDifficultySettings GetSettings(string difficultyKey)
        => GetSettings(difficultyKey, 1);

    public NumberRainQuest GenerateQuest(string difficultyKey, int level, int seed)
    {
        var settings = GetSettings(difficultyKey, level);
        var rnd = new Random(seed);
        var pool = difficultyKey.ToLowerInvariant() switch
        {
            "easy" => BuildEasyPool(settings, level),
            "normal" => BuildNormalPool(settings, level),
            "hard" => BuildHardPool(settings, level),
            "master" => BuildMasterPool(settings, level),
            _ => BuildNormalPool(settings, level)
        };

        if (pool.Count == 0)
            throw new InvalidOperationException("Keine Missionen verfügbar.");

        return pool[rnd.Next(pool.Count)](rnd);
    }

    private static NumberRainDifficultySettings BuildSettings(
        int minValue,
        int baseMax,
        int maxStep,
        int level,
        int spawnIntervalMs,
        double fallSpeed,
        int minTarget,
        int maxTarget,
        int minTimeSeconds,
        int maxTimeSeconds,
        int minCombo,
        int maxCombo)
    {
        int maxValue = Math.Clamp(baseMax + (level - 1) * maxStep, minValue + 10, 999);
        return new NumberRainDifficultySettings(minValue, maxValue, spawnIntervalMs, fallSpeed,
            minTarget, maxTarget, minTimeSeconds, maxTimeSeconds, minCombo, maxCombo);
    }

    private static List<Func<Random, NumberRainQuest>> BuildEasyPool(NumberRainDifficultySettings settings, int level)
    {
        return new List<Func<Random, NumberRainQuest>>
        {
            rnd => {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} gerade Zahlen", "Gerade", (v, _) => NumberRainRuleLibrary.IsEven(v), n);
            },

            rnd => {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} ungerade Zahlen", "Ungerade", (v, _) => NumberRainRuleLibrary.IsOdd(v), n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Vielfache von {k}", $"Vielfache von {k}",
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die auf {d} enden", $"Endet auf {d}",
                    (v, _) => NumberRainRuleLibrary.EndsWithDigit(v, d), n);
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 5);
                int b = rnd.Next(a + 3, Math.Min(a + 20, settings.MaxValue));
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen zwischen {a} und {b}", $"Bereich {a}-{b}",
                    (v, _) => NumberRainRuleLibrary.IsBetween(v, a, b), n);
            },

            rnd =>
            {
                int b = rnd.Next(settings.MinValue + 5, settings.MaxValue);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen < {b}", $"< {b}",
                    (v, _) => v < b, n);
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 5);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen > {a}", $"> {a}",
                    (v, _) => v > a, n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} zweistellige Zahlen", "Zweistellig",
                    (v, _) => NumberRainRuleLibrary.HasDigitCount(v, 2), n);
            },

            rnd =>
            {
                int digits = rnd.Next(1, 4);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen mit genau {digits} Stellen", $"{digits} Stellen",
                    (v, _) => NumberRainRuleLibrary.HasDigitCount(v, digits), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die die Ziffer {d} enthalten", $"Enthält {d}",
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die die Ziffer {d} NICHT enthalten", $"Ohne {d}",
                    (v, _) => !NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen mit gerader Quersumme", "Quersumme gerade",
                    (v, _) => NumberRainRuleLibrary.SumDigits(v) % 2 == 0, n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen mit ungerader Quersumme", "Quersumme ungerade",
                    (v, _) => NumberRainRuleLibrary.SumDigits(v) % 2 != 0, n);
            },

            rnd =>
            {
                int s = rnd.Next(3, 12);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen mit Quersumme = {s}", $"Quersumme {s}",
                    (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die durch 2 ODER 5 teilbar sind", "2 oder 5",
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, 2) || NumberRainRuleLibrary.IsMultipleOf(v, 5), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} gerade Zahlen", t,
                    new NumberRainGoal("Gerade", (v, _) => NumberRainRuleLibrary.IsEven(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int k = rnd.Next(2, 11);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Vielfache von {k}", t,
                    new NumberRainGoal($"Vielfache von {k}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Zahlen, die auf {d} enden", t,
                    new NumberRainGoal($"Endet auf {d}", (v, _) => NumberRainRuleLibrary.EndsWithDigit(v, d), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 8);
                int b = rnd.Next(a + 3, Math.Min(a + 20, settings.MaxValue));
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Zahlen im Bereich {a}–{b}", t,
                    new NumberRainGoal($"{a}-{b}", (v, _) => NumberRainRuleLibrary.IsBetween(v, a, b), n));
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Vielfache von {k}, aber klicke NIE auf Zahlen die auf {d} enden",
                    new NumberRainGoal($"Vielfache von {k}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n),
                    (v, _) => NumberRainRuleLibrary.EndsWithDigit(v, d), $"Endet auf {d}");
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Zahlen mit Ziffer {d}, aber meide gerade Zahlen",
                    new NumberRainGoal($"Enthält {d}", (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n),
                    (v, _) => NumberRainRuleLibrary.IsEven(v), "Gerade");
            },

            rnd =>
            {
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} gerade und {n2} ungerade Zahlen",
                    new NumberRainGoal("Gerade", (v, _) => NumberRainRuleLibrary.IsEven(v), n1),
                    new NumberRainGoal("Ungerade", (v, _) => NumberRainRuleLibrary.IsOdd(v), n2));
            },

            rnd =>
            {
                int k1 = rnd.Next(2, 7);
                int k2 = rnd.Next(3, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} Vielfache von {k1} und {n2} Vielfache von {k2}",
                    new NumberRainGoal($"Vielfache von {k1}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k1), n1),
                    new NumberRainGoal($"Vielfache von {k2}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k2), n2));
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 10);
                int b = rnd.Next(a + 4, Math.Min(a + 20, settings.MaxValue));
                int d = rnd.Next(0, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} Zahlen im Bereich {a}–{b} und {n2} Zahlen mit Ziffer {d}",
                    new NumberRainGoal($"{a}-{b}", (v, _) => NumberRainRuleLibrary.IsBetween(v, a, b), n1),
                    new NumberRainGoal($"Enthält {d}", (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n2));
            }
        };
    }

    private static List<Func<Random, NumberRainQuest>> BuildNormalPool(NumberRainDifficultySettings settings, int level)
    {
        return new List<Func<Random, NumberRainQuest>>
        {
            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Primzahlen", "Primzahlen",
                (v, _) => NumberRainRuleLibrary.IsPrime(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Nicht-Primzahlen", "Nicht-Primzahlen",
                (v, _) => NumberRainRuleLibrary.IsComposite(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Quadratzahlen", "Quadratzahlen",
                (v, _) => NumberRainRuleLibrary.IsSquare(v), Target(rnd, settings, level)),

            rnd =>
            {
                int k = rnd.Next(2, 9);
                int j = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die durch {k} UND {j} teilbar sind", $"{k} & {j}",
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.IsMultipleOf(v, j), n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 9);
                int j = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die durch {k} teilbar sind, aber NICHT durch {j}", $"{k} nicht {j}",
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && !NumberRainRuleLibrary.IsMultipleOf(v, j), n);
            },

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Palindromzahlen", "Palindrom",
                (v, _) => NumberRainRuleLibrary.IsPalindrome(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen mit Quersumme prim", "Quersumme prim",
                (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v), Target(rnd, settings, level)),

            rnd =>
            {
                int s = rnd.Next(10, 20);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen mit Quersumme = {s}", $"Quersumme {s}",
                    (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die Ziffer {d} enthalten UND gerade sind", $"{d} & gerade",
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && NumberRainRuleLibrary.IsEven(v), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int k = rnd.Next(2, 9);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die Ziffer {d} enthalten UND NICHT durch {k} teilbar sind", $"{d} & nicht {k}",
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && !NumberRainRuleLibrary.IsMultipleOf(v, k), n);
            },

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen mit mindestens zwei gleichen Ziffern", "mind. zwei gleich",
                (v, _) => NumberRainRuleLibrary.HasAtLeastTwoEqualDigits(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen mit allen Ziffern verschieden", "alle verschieden",
                (v, _) => NumberRainRuleLibrary.HasAllDigitsDifferent(v), Target(rnd, settings, level)),

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 10);
                int b = rnd.Next(a + 5, Math.Min(a + 25, settings.MaxValue));
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen, die näher an {a} als an {b} sind", $"näher an {a}",
                    (v, _) => NumberRainRuleLibrary.IsCloserTo(v, a, b), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Primzahlen", t,
                    new NumberRainGoal("Primzahlen", (v, _) => NumberRainRuleLibrary.IsPrime(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Quadratzahlen", t,
                    new NumberRainGoal("Quadratzahlen", (v, _) => NumberRainRuleLibrary.IsSquare(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int k = rnd.Next(2, 9);
                int j = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Zahlen (durch {k} teilbar, aber nicht durch {j})", t,
                    new NumberRainGoal($"{k} nicht {j}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && !NumberRainRuleLibrary.IsMultipleOf(v, j), n));
            },

            rnd =>
            {
                int c = ComboTarget(rnd, settings);
                int k = rnd.Next(2, 11);
                return CreateComboQuest($"Erreiche Combo {c} mit Regel: Vielfache von {k}", c,
                    new NumberRainRule($"Vielfache von {k}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k)));
            },

            rnd =>
            {
                int c = ComboTarget(rnd, settings);
                int d = rnd.Next(0, 10);
                return CreateComboQuest($"Erreiche Combo {c} mit Regel: Ziffer {d} enthalten", c,
                    new NumberRainRule($"Enthält {d}", (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d)));
            },

            rnd =>
            {
                int c = ComboTarget(rnd, settings);
                return CreateComboQuest($"Erreiche Combo {c} mit Regel: Quersumme gerade", c,
                    new NumberRainRule("Quersumme gerade", (v, _) => NumberRainRuleLibrary.SumDigits(v) % 2 == 0));
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Quadratzahlen, aber meide Zahlen mit Ziffer {d}",
                    new NumberRainGoal("Quadratzahlen", (v, _) => NumberRainRuleLibrary.IsSquare(v), n),
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), $"Ziffer {d}");
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Primzahlen, aber klicke NIE auf Vielfache von {k}",
                    new NumberRainGoal("Primzahlen", (v, _) => NumberRainRuleLibrary.IsPrime(v), n),
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), $"Vielfache von {k}");
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} Primzahlen und {n2} Vielfache von {k}",
                    new NumberRainGoal("Primzahlen", (v, _) => NumberRainRuleLibrary.IsPrime(v), n1),
                    new NumberRainGoal($"Vielfache von {k}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n2));
            },

            rnd =>
            {
                int s = rnd.Next(8, 20);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} Palindromzahlen und {n2} Zahlen mit Quersumme {s}",
                    new NumberRainGoal("Palindrom", (v, _) => NumberRainRuleLibrary.IsPalindrome(v), n1),
                    new NumberRainGoal($"Quersumme {s}", (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n2));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int k = rnd.Next(3, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Sammle {n1} Quadratzahlen und {n2} ungerade Vielfache von {k}", t,
                    new NumberRainGoal("Quadratzahlen", (v, _) => NumberRainRuleLibrary.IsSquare(v), n1),
                    new NumberRainGoal($"Ungerade von {k}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.IsOdd(v), n2));
            }
        };
    }

    private static List<Func<Random, NumberRainQuest>> BuildHardPool(NumberRainDifficultySettings settings, int level)
    {
        return new List<Func<Random, NumberRainQuest>>
        {
            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen mit Rest {r} bei Division durch {m}", $"mod {m} = {r}",
                    (v, _) => v % m == r, n);
            },

            rnd =>
            {
                int a = rnd.Next(10, settings.MaxValue / 2);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: Prim UND > {a}", $"> {a} & prim",
                    (v, _) => NumberRainRuleLibrary.IsPrime(v) && v > a, n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: Prim UND enthält Ziffer {d}", $"Prim mit {d}",
                    (v, _) => NumberRainRuleLibrary.IsPrime(v) && NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: Quadratzahl ODER Prim", "Quadrat oder Prim",
                    (v, _) => NumberRainRuleLibrary.IsSquare(v) || NumberRainRuleLibrary.IsPrime(v), n);
            },

            rnd =>
            {
                int k = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: (Vielfache von {k}) UND (Quersumme prim)", $"{k} & QS prim",
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.IsSumDigitsPrime(v), n);
            },

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Fibonacci-Zahlen (im erlaubten Range)", "Fibonacci",
                (v, _) => NumberRainRuleLibrary.IsFibonacci(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Potenzen von 2", "Potenzen von 2",
                (v, _) => NumberRainRuleLibrary.IsPowerOfTwo(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen mit streng steigenden Ziffern", "Ziffern steigend",
                (v, _) => NumberRainRuleLibrary.HasStrictlyIncreasingDigits(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen mit streng fallenden Ziffern", "Ziffern fallend",
                (v, _) => NumberRainRuleLibrary.HasStrictlyDecreasingDigits(v), Target(rnd, settings, level)),

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int e = rnd.Next(0, 10);
                while (e == d) e = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: enthält {d}, aber enthält NICHT {e}", $"{d} ohne {e}",
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && !NumberRainRuleLibrary.ContainsDigit(v, e), n);
            },

            rnd =>
            {
                int k = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: durch {k} teilbar, aber Quersumme ungerade", $"{k} & QS ungerade",
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.SumDigits(v) % 2 != 0, n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 9);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: Palindrom UND durch {k} teilbar", $"Palindrom & {k}",
                    (v, _) => NumberRainRuleLibrary.IsPalindrome(v) && NumberRainRuleLibrary.IsMultipleOf(v, k), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Zahlen mit mod {m} = {r}", t,
                    new NumberRainGoal($"mod {m}={r}", (v, _) => v % m == r, n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int a = rnd.Next(10, settings.MaxValue / 2);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Zahlen (Prim UND > {a})", t,
                    new NumberRainGoal($"> {a} & prim", (v, _) => NumberRainRuleLibrary.IsPrime(v) && v > a, n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Wähle {n} Zahlen (Quersumme prim UND enthält {d})", t,
                    new NumberRainGoal($"QS prim & {d}", (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v) && NumberRainRuleLibrary.ContainsDigit(v, d), n));
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Zahlen (mod {m}={r}), aber meide Quadratzahlen",
                    new NumberRainGoal($"mod {m}={r}", (v, _) => v % m == r, n),
                    (v, _) => NumberRainRuleLibrary.IsSquare(v), "Quadratzahlen");
            },

            rnd =>
            {
                int k = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Zahlen (Vielfache von {k}), aber meide Palindrome",
                    new NumberRainGoal($"Vielfache von {k}", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n),
                    (v, _) => NumberRainRuleLibrary.IsPalindrome(v), "Palindrom");
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int s = rnd.Next(8, 20);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} Zahlen (mod {m}={r}) und {n2} Zahlen (Quersumme = {s})",
                    new NumberRainGoal($"mod {m}={r}", (v, _) => v % m == r, n1),
                    new NumberRainGoal($"QS {s}", (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n2));
            },

            rnd =>
            {
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} Potenzen von 2 und {n2} Primzahlen",
                    new NumberRainGoal("Potenzen von 2", (v, _) => NumberRainRuleLibrary.IsPowerOfTwo(v), n1),
                    new NumberRainGoal("Primzahlen", (v, _) => NumberRainRuleLibrary.IsPrime(v), n2));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int d = rnd.Next(0, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Sammle {n1} Fibonacci und {n2} Zahlen mit Ziffer {d}", t,
                    new NumberRainGoal("Fibonacci", (v, _) => NumberRainRuleLibrary.IsFibonacci(v), n1),
                    new NumberRainGoal($"Enthält {d}", (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n2));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings) + 5;
                int n = Target(rnd, settings, level);
                return CreateSurvivalQuest($"Überlebe {t}s und mache mindestens {n} Treffer", t,
                    new NumberRainRule("Schwere Regel", (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v) && NumberRainRuleLibrary.IsOdd(v)),
                    n, maxMisses: 5 + level / 8);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings) + 5;
                int maxMiss = 3 + level / 10;
                return CreateSurvivalQuest($"Überlebe {t}s mit max. {maxMiss} Fehlklicks (Regel: mod/prim)", t,
                    new NumberRainRule("Prim oder mod 3", (v, _) => NumberRainRuleLibrary.IsPrime(v) || v % 3 == 0),
                    minHits: 0, maxMisses: maxMiss);
            }
        };
    }

    private static List<Func<Random, NumberRainQuest>> BuildMasterPool(NumberRainDifficultySettings settings, int level)
    {
        return new List<Func<Random, NumberRainQuest>>
        {
            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Semiprime (Produkt aus genau 2 Primzahlen)", "Semiprime",
                    (v, _) => NumberRainRuleLibrary.IsSemiPrime(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} squarefree Zahlen (kein Primquadrat teilt sie)", "squarefree",
                    (v, _) => NumberRainRuleLibrary.IsSquareFree(v), n);
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: (mod {m}={r}) UND (Quersumme prim)", $"mod {m}={r} & QS prim",
                    (v, _) => v % m == r && NumberRainRuleLibrary.IsSumDigitsPrime(v), n);
            },

            rnd =>
            {
                int s = rnd.Next(10, 25);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: (Prim) UND (Quersumme = {s})", $"Prim & QS {s}",
                    (v, _) => NumberRainRuleLibrary.IsPrime(v) && NumberRainRuleLibrary.SumDigits(v) == s, n);
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: (contains {d}) UND (mod {m}={r}) UND (ungerade)", $"{d} & mod {m}={r} & ungerade",
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && v % m == r && NumberRainRuleLibrary.IsOdd(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: (Harshad) UND (nicht durch 10 teilbar)", "Harshad ohne 10",
                    (v, _) => NumberRainRuleLibrary.IsHarshad(v) && !NumberRainRuleLibrary.IsMultipleOf(v, 10), n);
            },

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen: (pronic n(n+1))", "pronic",
                (v, _) => NumberRainRuleLibrary.IsPronic(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen: (Automorph)", "automorph",
                (v, _) => NumberRainRuleLibrary.IsAutomorph(v), Target(rnd, settings, level)),

            rnd => CreateCountQuest($"Wähle {Target(rnd, settings, level)} Zahlen: (Palindrom) UND (nicht prim)", "Palindrom, nicht prim",
                (v, _) => NumberRainRuleLibrary.IsPalindrome(v) && !NumberRainRuleLibrary.IsPrime(v), Target(rnd, settings, level)),

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateCountQuest($"Wähle {n} Zahlen: (Quadratzahl) UND (enthält {d})", $"Quadrat mit {d}",
                    (v, _) => NumberRainRuleLibrary.IsSquare(v) && NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Sammle {n} (Semiprime)", t,
                    new NumberRainGoal("Semiprime", (v, _) => NumberRainRuleLibrary.IsSemiPrime(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Sammle {n} (squarefree)", t,
                    new NumberRainGoal("squarefree", (v, _) => NumberRainRuleLibrary.IsSquareFree(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: Sammle {n} (mod {m}={r} UND Quersumme prim)", t,
                    new NumberRainGoal($"mod {m}={r} & QS prim", (v, _) => v % m == r && NumberRainRuleLibrary.IsSumDigitsPrime(v), n));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Treffer (Regel R), aber klicke NIE auf Zahlen mit Eigenschaft X (z.B. Prim)",
                    new NumberRainGoal("Regel R", (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, 3), n),
                    (v, _) => NumberRainRuleLibrary.IsPrime(v), "Primzahlen");
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Treffer (Regel R), aber jede Zahl mit Ziffer {d} ist Bombe",
                    new NumberRainGoal("Regel R", (v, _) => NumberRainRuleLibrary.IsOdd(v), n),
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), $"Ziffer {d}");
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateAvoidQuest($"Wähle {n} Treffer (Regel R), aber jede Quadratzahl ist Bombe",
                    new NumberRainGoal("Regel R", (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v), n),
                    (v, _) => NumberRainRuleLibrary.IsSquare(v), "Quadratzahlen");
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int s = rnd.Next(12, 24);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                int n3 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} (Semiprime) + {n2} (mod {m}={r}) + {n3} (Quersumme = {s})",
                    new NumberRainGoal("Semiprime", (v, _) => NumberRainRuleLibrary.IsSemiPrime(v), n1),
                    new NumberRainGoal($"mod {m}={r}", (v, _) => v % m == r, n2),
                    new NumberRainGoal($"QS {s}", (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n3));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateTimedQuest($"In {t}s: {n1} (Prim) + {n2} (Potenzen von 2)", t,
                    new NumberRainGoal("Primzahlen", (v, _) => NumberRainRuleLibrary.IsPrime(v), n1),
                    new NumberRainGoal("Potenzen von 2", (v, _) => NumberRainRuleLibrary.IsPowerOfTwo(v), n2));
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int e = rnd.Next(0, 10);
                while (e == d) e = rnd.Next(0, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                return CreateMultiQuest($"Sammle {n1} (squarefree) und {n2} (enthält {d} aber nicht {e})",
                    new NumberRainGoal("squarefree", (v, _) => NumberRainRuleLibrary.IsSquareFree(v), n1),
                    new NumberRainGoal($"{d} ohne {e}", (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && !NumberRainRuleLibrary.ContainsDigit(v, e), n2));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateDynamicQuest($"Kettenregel: „Wähle {n} Zahlen, die > der letzten Wahl sind“", n,
                    new NumberRainRule("größer als letzte Wahl", (v, last) => last is null || v > last));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateDynamicQuest($"Kettenregel: „Wähle {n} Zahlen, die durch die letzte Wahl teilbar sind“", n,
                    new NumberRainRule("teilbar durch letzte Wahl", (v, last) => last is null || (last != 0 && v % last == 0)));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                return CreateSwitchQuest($"Wechselregel: „Alle 10 Sekunden ändert sich die Regel zwischen R1 und R2“", n, 10,
                    new NumberRainRule("R1: Gerade", (v, _) => NumberRainRuleLibrary.IsEven(v)),
                    new NumberRainRule("R2: Prim", (v, _) => NumberRainRuleLibrary.IsPrime(v)));
            }
        };
    }

    private static int Target(Random rnd, NumberRainDifficultySettings settings, int level)
    {
        int boost = (level - 1) / 2; // Alle 2 Level steigt das Ziel um 1
        int min = settings.MinTarget + boost;
        int max = settings.MaxTarget + boost;
        return rnd.Next(min, max + 1);
    }

    private static int TimeLimit(Random rnd, NumberRainDifficultySettings settings)
        => rnd.Next(settings.MinTimeSeconds, settings.MaxTimeSeconds + 1);

    private static int ComboTarget(Random rnd, NumberRainDifficultySettings settings)
        => rnd.Next(settings.MinCombo, settings.MaxCombo + 1);

    private static NumberRainQuest CreateCountQuest(string description, string label, Func<int, int?, bool> predicate, int target)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Count,
            Goals = new[] { new NumberRainGoal(label, predicate, target) },
            Rules = new[] { new NumberRainRule(label, predicate) }
        };

    private static NumberRainQuest CreateTimedQuest(string description, int timeLimit, params NumberRainGoal[] goals)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Timed,
            Goals = goals,
            TimeLimitSeconds = timeLimit,
            Rules = goals.Select(g => new NumberRainRule(g.Label, g.Predicate)).ToArray()
        };

    private static NumberRainQuest CreateAvoidQuest(string description, NumberRainGoal goal, Func<int, int?, bool> avoid, string avoidLabel)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Avoid,
            Goals = new[] { goal },
            AvoidPredicate = avoid,
            AvoidLabel = avoidLabel,
            Rules = new[] { new NumberRainRule(goal.Label, goal.Predicate) }
        };

    private static NumberRainQuest CreateMultiQuest(string description, params NumberRainGoal[] goals)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Multi,
            Goals = goals,
            Rules = goals.Select(g => new NumberRainRule(g.Label, g.Predicate)).ToArray()
        };

    private static NumberRainQuest CreateComboQuest(string description, int comboTarget, NumberRainRule rule)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Combo,
            ComboTarget = comboTarget,
            Rules = new[] { rule },
            Goals = new[] { new NumberRainGoal(rule.Label, rule.Predicate, comboTarget) }
        };

    private static NumberRainQuest CreateSurvivalQuest(string description, int timeLimit, NumberRainRule rule, int minHits, int maxMisses)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Survival,
            TimeLimitSeconds = timeLimit,
            MinHits = minHits,
            MaxMisses = maxMisses,
            Rules = new[] { rule },
            Goals = new[] { new NumberRainGoal(rule.Label, rule.Predicate, minHits) }
        };

    private static NumberRainQuest CreateDynamicQuest(string description, int target, NumberRainRule rule)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Dynamic,
            Goals = new[] { new NumberRainGoal(rule.Label, rule.Predicate, target) },
            Rules = new[] { rule }
        };

    private static NumberRainQuest CreateSwitchQuest(string description, int target, int intervalSeconds, NumberRainRule ruleA, NumberRainRule ruleB)
        => new()
        {
            Description = description,
            Mode = NumberRainQuestMode.Switch,
            Goals = new[] { new NumberRainGoal("Treffer", (v, last) => ruleA.Predicate(v, last) || ruleB.Predicate(v, last), target) },
            Rules = new[] { ruleA, ruleB },
            SwitchIntervalSeconds = intervalSeconds
        };
}

public sealed record NumberRainDifficultySettings(
    int MinValue,
    int MaxValue,
    int SpawnIntervalMs,
    double FallSpeed,
    int MinTarget,
    int MaxTarget,
    int MinTimeSeconds,
    int MaxTimeSeconds,
    int MinCombo,
    int MaxCombo);
