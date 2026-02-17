#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model.NumberRain;
using LogikSpiel.Services.Localization;

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
            return CreateCountQuest("Sammle gerade Zahlen", "Gerade Zahlen", (v, _) => v % 2 == 0, 5);

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
                return CreateCountQuest(SelectCount(n, LabelEvenNumbers()), LabelEvenNumbers(), (v, _) => NumberRainRuleLibrary.IsEven(v), n);
            },

            rnd => {
                int n = Target(rnd, settings, level);
                return CreateCountQuest(SelectCount(n, LabelOddNumbers()), LabelOddNumbers(), (v, _) => NumberRainRuleLibrary.IsOdd(v), n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int n = Target(rnd, settings, level);
                var label = LabelMultiplesOf(k);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelEndsWithDigit(d);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.EndsWithDigit(v, d), n);
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 5);
                int b = rnd.Next(a + 3, Math.Min(a + 20, settings.MaxValue));
                int n = Target(rnd, settings, level);
                var label = LabelRange(a, b);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.IsBetween(v, a, b), n);
            },

            rnd =>
            {
                int b = rnd.Next(settings.MinValue + 5, settings.MaxValue);
                int n = Target(rnd, settings, level);
                var label = LabelLessThan(b);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => v < b, n);
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 5);
                int n = Target(rnd, settings, level);
                var label = LabelGreaterThan(a);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => v > a, n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelTwoDigitNumbers();
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.HasDigitCount(v, 2), n);
            },

            rnd =>
            {
                int digits = rnd.Next(1, 4);
                int n = Target(rnd, settings, level);
                var label = LabelDigitCount(digits);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.HasDigitCount(v, digits), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigit(d);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelNotContainsDigit(d);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => !NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelDigitSumEven();
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.SumDigits(v) % 2 == 0, n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelDigitSumOdd();
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.SumDigits(v) % 2 != 0, n);
            },

            rnd =>
            {
                int s = rnd.Next(3, 12);
                int n = Target(rnd, settings, level);
                var label = LabelDigitSumEquals(s);
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelDivisibleBy2Or5();
                return CreateCountQuest(SelectCount(n, label), label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, 2) || NumberRainRuleLibrary.IsMultipleOf(v, 5), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                var label = LabelEvenNumbers();
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsEven(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int k = rnd.Next(2, 11);
                int n = Target(rnd, settings, level);
                var label = LabelMultiplesOf(k);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelEndsWithDigit(d);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.EndsWithDigit(v, d), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 8);
                int b = rnd.Next(a + 3, Math.Min(a + 20, settings.MaxValue));
                int n = Target(rnd, settings, level);
                var label = LabelRange(a, b);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsBetween(v, a, b), n));
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelMultiplesOf(k);
                var avoidLabel = LabelEndsWithDigit(d);
                return CreateAvoidQuest(Avoid(SelectCount(n, label), avoidLabel),
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n),
                    (v, _) => NumberRainRuleLibrary.EndsWithDigit(v, d), avoidLabel);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigit(d);
                var avoidLabel = LabelEvenNumbers();
                return CreateAvoidQuest(Avoid(SelectCount(n, label), avoidLabel),
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n),
                    (v, _) => NumberRainRuleLibrary.IsEven(v), avoidLabel);
            },

            rnd =>
            {
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var labelEven = LabelEvenNumbers();
                var labelOdd = LabelOddNumbers();
                return CreateMultiQuest(CollectDual(n1, labelEven, n2, labelOdd),
                    new NumberRainGoal(labelEven, (v, _) => NumberRainRuleLibrary.IsEven(v), n1),
                    new NumberRainGoal(labelOdd, (v, _) => NumberRainRuleLibrary.IsOdd(v), n2));
            },

            rnd =>
            {
                int k1 = rnd.Next(2, 7);
                int k2 = rnd.Next(3, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelMultiplesOf(k1);
                var label2 = LabelMultiplesOf(k2);
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k1), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k2), n2));
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 10);
                int b = rnd.Next(a + 4, Math.Min(a + 20, settings.MaxValue));
                int d = rnd.Next(0, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelRange(a, b);
                var label2 = LabelContainsDigit(d);
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsBetween(v, a, b), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n2));
            }
        };
    }

    private static List<Func<Random, NumberRainQuest>> BuildNormalPool(NumberRainDifficultySettings settings, int level)
    {
        return new List<Func<Random, NumberRainQuest>>
        {
            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelPrimes();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPrime(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelNonPrimes();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsComposite(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelSquares();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsSquare(v), n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 9);
                int j = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                var label = LabelDivisibleByBoth(k, j);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.IsMultipleOf(v, j), n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 9);
                int j = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                var label = LabelDivisibleByNot(k, j);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && !NumberRainRuleLibrary.IsMultipleOf(v, j), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelPalindrome();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPalindrome(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelDigitSumPrime();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v), n);
            },

            rnd =>
            {
                int s = rnd.Next(10, 20);
                int n = Target(rnd, settings, level);
                var label = LabelDigitSumEquals(s);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigitAndEven(d);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && NumberRainRuleLibrary.IsEven(v), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int k = rnd.Next(2, 9);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigitAndNotDivisible(d, k);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && !NumberRainRuleLibrary.IsMultipleOf(v, k), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelAtLeastTwoEqualDigits();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.HasAtLeastTwoEqualDigits(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelAllDigitsDifferent();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.HasAllDigitsDifferent(v), n);
            },

            rnd =>
            {
                int a = rnd.Next(settings.MinValue, settings.MaxValue - 10);
                int b = rnd.Next(a + 5, Math.Min(a + 25, settings.MaxValue));
                int n = Target(rnd, settings, level);
                var label = LabelCloserTo(a);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsCloserTo(v, a, b), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                var label = LabelPrimes();
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsPrime(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                var label = LabelSquares();
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsSquare(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int k = rnd.Next(2, 9);
                int j = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                var label = LabelDivisibleByNot(k, j);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && !NumberRainRuleLibrary.IsMultipleOf(v, j), n));
            },

            rnd =>
            {
                int c = ComboTarget(rnd, settings);
                int k = rnd.Next(2, 11);
                var label = LabelMultiplesOf(k);
                return CreateComboQuest(Combo(c, label), c,
                    new NumberRainRule(label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k)));
            },

            rnd =>
            {
                int c = ComboTarget(rnd, settings);
                int d = rnd.Next(0, 10);
                var label = LabelContainsDigit(d);
                return CreateComboQuest(Combo(c, label), c,
                    new NumberRainRule(label, (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d)));
            },

            rnd =>
            {
                int c = ComboTarget(rnd, settings);
                var label = LabelDigitSumEven();
                return CreateComboQuest(Combo(c, label), c,
                    new NumberRainRule(label, (v, _) => NumberRainRuleLibrary.SumDigits(v) % 2 == 0));
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelSquares();
                var avoidLabel = LabelContainsDigit(d);
                return CreateAvoidQuest(Avoid(SelectCount(n, label), avoidLabel),
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsSquare(v), n),
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), avoidLabel);
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int n = Target(rnd, settings, level);
                var label = LabelPrimes();
                var avoidLabel = LabelMultiplesOf(k);
                return CreateAvoidQuest(Avoid(SelectCount(n, label), avoidLabel),
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsPrime(v), n),
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), avoidLabel);
            },

            rnd =>
            {
                int k = rnd.Next(2, 11);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelPrimes();
                var label2 = LabelMultiplesOf(k);
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsPrime(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n2));
            },

            rnd =>
            {
                int s = rnd.Next(8, 20);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelPalindrome();
                var label2 = LabelDigitSumEquals(s);
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsPalindrome(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n2));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int k = rnd.Next(3, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelSquares();
                var label2 = LabelOddMultiplesOf(k);
                return CreateTimedQuest(Timed(t, CollectDual(n1, label1, n2, label2)), t,
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsSquare(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.IsOdd(v), n2));
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
                var label = LabelModulo(m, r);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => v % m == r, n);
            },

            rnd =>
            {
                int a = rnd.Next(10, settings.MaxValue / 2);
                int n = Target(rnd, settings, level);
                var label = LabelPrimeGreaterThan(a);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPrime(v) && v > a, n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelPrimeContainsDigit(d);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPrime(v) && NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelSquareOrPrime();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsSquare(v) || NumberRainRuleLibrary.IsPrime(v), n);
            },

            rnd =>
            {
                int k = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                var label = LabelMultiplesAndDigitSumPrime(k);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.IsSumDigitsPrime(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelFibonacci();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsFibonacci(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelPowersOfTwo();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPowerOfTwo(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelDigitsIncreasing();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.HasStrictlyIncreasingDigits(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelDigitsDecreasing();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.HasStrictlyDecreasingDigits(v), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int e = rnd.Next(0, 10);
                while (e == d) e = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigitNot(d, e);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && !NumberRainRuleLibrary.ContainsDigit(v, e), n);
            },

            rnd =>
            {
                int k = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                var label = LabelDivisibleByButDigitSumOdd(k);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k) && NumberRainRuleLibrary.SumDigits(v) % 2 != 0, n);
            },

            rnd =>
            {
                int k = rnd.Next(2, 9);
                int n = Target(rnd, settings, level);
                var label = LabelPalindromeDivisibleBy(k);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPalindrome(v) && NumberRainRuleLibrary.IsMultipleOf(v, k), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                var label = LabelModulo(m, r);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => v % m == r, n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int a = rnd.Next(10, settings.MaxValue / 2);
                int n = Target(rnd, settings, level);
                var label = LabelPrimeGreaterThan(a);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsPrime(v) && v > a, n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigitAndDigitSumPrime(d);
                return CreateTimedQuest(Timed(t, SelectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v) && NumberRainRuleLibrary.ContainsDigit(v, d), n));
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                var label = LabelModulo(m, r);
                var avoidLabel = LabelSquares();
                return CreateAvoidQuest(Avoid(SelectCount(n, label), avoidLabel),
                    new NumberRainGoal(label, (v, _) => v % m == r, n),
                    (v, _) => NumberRainRuleLibrary.IsSquare(v), avoidLabel);
            },

            rnd =>
            {
                int k = rnd.Next(3, 11);
                int n = Target(rnd, settings, level);
                var label = LabelMultiplesOf(k);
                var avoidLabel = LabelPalindrome();
                return CreateAvoidQuest(Avoid(SelectCount(n, label), avoidLabel),
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, k), n),
                    (v, _) => NumberRainRuleLibrary.IsPalindrome(v), avoidLabel);
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int s = rnd.Next(8, 20);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelModulo(m, r);
                var label2 = LabelDigitSumEquals(s);
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => v % m == r, n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n2));
            },

            rnd =>
            {
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelPowersOfTwo();
                var label2 = LabelPrimes();
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsPowerOfTwo(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.IsPrime(v), n2));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int d = rnd.Next(0, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelFibonacci();
                var label2 = LabelContainsDigit(d);
                return CreateTimedQuest(Timed(t, CollectDual(n1, label1, n2, label2)), t,
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsFibonacci(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), n2));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings) + 5;
                int n = Target(rnd, settings, level);
                var ruleLabel = LabelRuleHeavy();
                return CreateSurvivalQuest(SurviveHits(t, n), t,
                    new NumberRainRule(ruleLabel, (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v) && NumberRainRuleLibrary.IsOdd(v)),
                    n, maxMisses: 5 + level / 8);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings) + 5;
                int maxMiss = 3 + level / 10;
                var ruleLabel = LabelRulePrimeOrMod3();
                return CreateSurvivalQuest(SurviveMaxMisses(t, maxMiss, ruleLabel), t,
                    new NumberRainRule(ruleLabel, (v, _) => NumberRainRuleLibrary.IsPrime(v) || v % 3 == 0),
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
                var label = LabelSemiPrime();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsSemiPrime(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelSquareFree();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsSquareFree(v), n);
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                var label = LabelModuloDigitSumPrime(m, r);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => v % m == r && NumberRainRuleLibrary.IsSumDigitsPrime(v), n);
            },

            rnd =>
            {
                int s = rnd.Next(10, 25);
                int n = Target(rnd, settings, level);
                var label = LabelPrimeDigitSum(s);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPrime(v) && NumberRainRuleLibrary.SumDigits(v) == s, n);
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelContainsDigitModuloOdd(d, m, r);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && v % m == r && NumberRainRuleLibrary.IsOdd(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelHarshadWithoutTen();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsHarshad(v) && !NumberRainRuleLibrary.IsMultipleOf(v, 10), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelPronic();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPronic(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelAutomorphic();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsAutomorph(v), n);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var label = LabelPalindromeNotPrime();
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsPalindrome(v) && !NumberRainRuleLibrary.IsPrime(v), n);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var label = LabelSquareContainsDigit(d);
                return CreateCountQuest(SelectCount(n, label), label,
                    (v, _) => NumberRainRuleLibrary.IsSquare(v) && NumberRainRuleLibrary.ContainsDigit(v, d), n);
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                var label = LabelSemiPrime();
                return CreateTimedQuest(Timed(t, CollectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsSemiPrime(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n = Target(rnd, settings, level);
                var label = LabelSquareFree();
                return CreateTimedQuest(Timed(t, CollectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => NumberRainRuleLibrary.IsSquareFree(v), n));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int n = Target(rnd, settings, level);
                var label = LabelModuloDigitSumPrime(m, r);
                return CreateTimedQuest(Timed(t, CollectCount(n, label)), t,
                    new NumberRainGoal(label, (v, _) => v % m == r && NumberRainRuleLibrary.IsSumDigitsPrime(v), n));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var ruleLabel = LabelRuleR();
                var avoidLabel = LabelPrimes();
                return CreateAvoidQuest(Avoid(SelectCount(n, ruleLabel), avoidLabel),
                    new NumberRainGoal(ruleLabel, (v, _) => NumberRainRuleLibrary.IsMultipleOf(v, 3), n),
                    (v, _) => NumberRainRuleLibrary.IsPrime(v), avoidLabel);
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int n = Target(rnd, settings, level);
                var ruleLabel = LabelRuleR();
                var avoidLabel = LabelBombDigit(d);
                return CreateAvoidQuest(Avoid(SelectCount(n, ruleLabel), avoidLabel),
                    new NumberRainGoal(ruleLabel, (v, _) => NumberRainRuleLibrary.IsOdd(v), n),
                    (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d), avoidLabel);
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var ruleLabel = LabelRuleR();
                var avoidLabel = LabelSquares();
                return CreateAvoidQuest(Avoid(SelectCount(n, ruleLabel), avoidLabel),
                    new NumberRainGoal(ruleLabel, (v, _) => NumberRainRuleLibrary.IsSumDigitsPrime(v), n),
                    (v, _) => NumberRainRuleLibrary.IsSquare(v), avoidLabel);
            },

            rnd =>
            {
                int m = rnd.Next(3, 12);
                int r = rnd.Next(0, m);
                int s = rnd.Next(12, 24);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                int n3 = Target(rnd, settings, level);
                var label1 = LabelSemiPrime();
                var label2 = LabelModulo(m, r);
                var label3 = LabelDigitSumEquals(s);
                return CreateMultiQuest(CollectTriple(n1, label1, n2, label2, n3, label3),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsSemiPrime(v), n1),
                    new NumberRainGoal(label2, (v, _) => v % m == r, n2),
                    new NumberRainGoal(label3, (v, _) => NumberRainRuleLibrary.SumDigits(v) == s, n3));
            },

            rnd =>
            {
                int t = TimeLimit(rnd, settings);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelPrimes();
                var label2 = LabelPowersOfTwo();
                return CreateTimedQuest(Timed(t, CollectDual(n1, label1, n2, label2)), t,
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsPrime(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.IsPowerOfTwo(v), n2));
            },

            rnd =>
            {
                int d = rnd.Next(0, 10);
                int e = rnd.Next(0, 10);
                while (e == d) e = rnd.Next(0, 10);
                int n1 = Target(rnd, settings, level);
                int n2 = Target(rnd, settings, level);
                var label1 = LabelSquareFree();
                var label2 = LabelContainsDigitNot(d, e);
                return CreateMultiQuest(CollectDual(n1, label1, n2, label2),
                    new NumberRainGoal(label1, (v, _) => NumberRainRuleLibrary.IsSquareFree(v), n1),
                    new NumberRainGoal(label2, (v, _) => NumberRainRuleLibrary.ContainsDigit(v, d) && !NumberRainRuleLibrary.ContainsDigit(v, e), n2));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var ruleLabel = LabelGreaterThanLastPick();
                return CreateDynamicQuest(ChainGreater(n), n,
                    new NumberRainRule(ruleLabel, (v, last) => last is null || v > last));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var ruleLabel = LabelDivisibleByLastPick();
                return CreateDynamicQuest(ChainDivisible(n), n,
                    new NumberRainRule(ruleLabel, (v, last) => last is null || (last != 0 && v % last == 0)));
            },

            rnd =>
            {
                int n = Target(rnd, settings, level);
                var ruleA = LabelRuleR1Even();
                var ruleB = LabelRuleR2Prime();
                return CreateSwitchQuest(SwitchRule(10, ruleA, ruleB), n, 10,
                    new NumberRainRule(ruleA, (v, _) => NumberRainRuleLibrary.IsEven(v)),
                    new NumberRainRule(ruleB, (v, _) => NumberRainRuleLibrary.IsPrime(v)));
            }
        };
    }

    private static int Target(Random rnd, NumberRainDifficultySettings settings, int level)
    {
        int boost = Math.Min((level - 1) / 2, 20);
        int min = Math.Min(settings.MinTarget + boost, settings.MaxTarget);
        int max = Math.Min(settings.MaxTarget + boost, 100);
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
            Goals = new[] { new NumberRainGoal(LabelHits(), (v, last) => ruleA.Predicate(v, last) || ruleB.Predicate(v, last), target) },
            Rules = new[] { ruleA, ruleB },
            SwitchIntervalSeconds = intervalSeconds
        };

    private static string SelectCount(int count, string label)
        => LocalizationService.Format("NumberRain_Quest_SelectCountFormat", count, label);

    private static string CollectCount(int count, string label)
        => LocalizationService.Format("NumberRain_Quest_CollectCountFormat", count, label);

    private static string CollectDual(int countA, string labelA, int countB, string labelB)
        => LocalizationService.Format("NumberRain_Quest_CollectDualFormat", countA, labelA, countB, labelB);

    private static string CollectTriple(int countA, string labelA, int countB, string labelB, int countC, string labelC)
        => LocalizationService.Format("NumberRain_Quest_CollectTripleFormat", countA, labelA, countB, labelB, countC, labelC);

    private static string Timed(int seconds, string text)
        => LocalizationService.Format("NumberRain_Quest_TimedFormat", seconds, text);

    private static string Avoid(string text, string avoidLabel)
        => LocalizationService.Format("NumberRain_Quest_AvoidFormat", text, avoidLabel);

    private static string Combo(int target, string ruleLabel)
        => LocalizationService.Format("NumberRain_Quest_ComboFormat", target, ruleLabel);

    private static string SurviveHits(int seconds, int hits)
        => LocalizationService.Format("NumberRain_Quest_SurviveHitsFormat", seconds, hits);

    private static string SurviveMaxMisses(int seconds, int misses, string ruleLabel)
        => LocalizationService.Format("NumberRain_Quest_SurviveMaxMissesFormat", seconds, misses, ruleLabel);

    private static string ChainGreater(int count)
        => LocalizationService.Format("NumberRain_Quest_ChainGreaterFormat", count);

    private static string ChainDivisible(int count)
        => LocalizationService.Format("NumberRain_Quest_ChainDivisibleFormat", count);

    private static string SwitchRule(int intervalSeconds, string ruleA, string ruleB)
        => LocalizationService.Format("NumberRain_Quest_SwitchRuleFormat", intervalSeconds, ruleA, ruleB);

    private static string LabelEvenNumbers() => LocalizationService.GetString("NumberRain_LabelEvenNumbers");
    private static string LabelOddNumbers() => LocalizationService.GetString("NumberRain_LabelOddNumbers");
    private static string LabelMultiplesOf(int value) => LocalizationService.Format("NumberRain_LabelMultiplesOfFormat", value);
    private static string LabelOddMultiplesOf(int value) => LocalizationService.Format("NumberRain_LabelOddMultiplesOfFormat", value);
    private static string LabelEndsWithDigit(int digit) => LocalizationService.Format("NumberRain_LabelEndsWithDigitFormat", digit);
    private static string LabelRange(int min, int max) => LocalizationService.Format("NumberRain_LabelRangeFormat", min, max);
    private static string LabelLessThan(int value) => LocalizationService.Format("NumberRain_LabelLessThanFormat", value);
    private static string LabelGreaterThan(int value) => LocalizationService.Format("NumberRain_LabelGreaterThanFormat", value);
    private static string LabelTwoDigitNumbers() => LocalizationService.GetString("NumberRain_LabelTwoDigitNumbers");
    private static string LabelDigitCount(int digits) => LocalizationService.Format("NumberRain_LabelDigitCountFormat", digits);
    private static string LabelContainsDigit(int digit) => LocalizationService.Format("NumberRain_LabelContainsDigitFormat", digit);
    private static string LabelNotContainsDigit(int digit) => LocalizationService.Format("NumberRain_LabelNotContainsDigitFormat", digit);
    private static string LabelDigitSumEven() => LocalizationService.GetString("NumberRain_LabelDigitSumEven");
    private static string LabelDigitSumOdd() => LocalizationService.GetString("NumberRain_LabelDigitSumOdd");
    private static string LabelDigitSumEquals(int sum) => LocalizationService.Format("NumberRain_LabelDigitSumEqualsFormat", sum);
    private static string LabelDivisibleBy2Or5() => LocalizationService.GetString("NumberRain_LabelDivisibleBy2Or5");
    private static string LabelPrimes() => LocalizationService.GetString("NumberRain_LabelPrimes");
    private static string LabelNonPrimes() => LocalizationService.GetString("NumberRain_LabelNonPrimes");
    private static string LabelSquares() => LocalizationService.GetString("NumberRain_LabelSquares");
    private static string LabelPalindrome() => LocalizationService.GetString("NumberRain_LabelPalindrome");
    private static string LabelDigitSumPrime() => LocalizationService.GetString("NumberRain_LabelDigitSumPrime");
    private static string LabelContainsDigitAndEven(int digit) => LocalizationService.Format("NumberRain_LabelContainsDigitAndEvenFormat", digit);
    private static string LabelContainsDigitAndNotDivisible(int digit, int divisor)
        => LocalizationService.Format("NumberRain_LabelContainsDigitAndNotDivisibleFormat", digit, divisor);
    private static string LabelAtLeastTwoEqualDigits() => LocalizationService.GetString("NumberRain_LabelAtLeastTwoEqualDigits");
    private static string LabelAllDigitsDifferent() => LocalizationService.GetString("NumberRain_LabelAllDigitsDifferent");
    private static string LabelCloserTo(int value) => LocalizationService.Format("NumberRain_LabelCloserToFormat", value);
    private static string LabelDivisibleByBoth(int first, int second)
        => LocalizationService.Format("NumberRain_LabelDivisibleByBothFormat", first, second);
    private static string LabelDivisibleByNot(int first, int second)
        => LocalizationService.Format("NumberRain_LabelDivisibleByNotFormat", first, second);
    private static string LabelModulo(int modulo, int remainder)
        => LocalizationService.Format("NumberRain_LabelModuloFormat", modulo, remainder);
    private static string LabelPrimeGreaterThan(int value)
        => LocalizationService.Format("NumberRain_LabelPrimeGreaterThanFormat", value);
    private static string LabelPrimeContainsDigit(int digit)
        => LocalizationService.Format("NumberRain_LabelPrimeContainsDigitFormat", digit);
    private static string LabelSquareOrPrime() => LocalizationService.GetString("NumberRain_LabelSquareOrPrime");
    private static string LabelMultiplesAndDigitSumPrime(int value)
        => LocalizationService.Format("NumberRain_LabelMultiplesAndDigitSumPrimeFormat", value);
    private static string LabelFibonacci() => LocalizationService.GetString("NumberRain_LabelFibonacci");
    private static string LabelPowersOfTwo() => LocalizationService.GetString("NumberRain_LabelPowersOfTwo");
    private static string LabelDigitsIncreasing() => LocalizationService.GetString("NumberRain_LabelDigitsIncreasing");
    private static string LabelDigitsDecreasing() => LocalizationService.GetString("NumberRain_LabelDigitsDecreasing");
    private static string LabelContainsDigitNot(int digit, int otherDigit)
        => LocalizationService.Format("NumberRain_LabelContainsDigitNotFormat", digit, otherDigit);
    private static string LabelDivisibleByButDigitSumOdd(int value)
        => LocalizationService.Format("NumberRain_LabelDivisibleByButDigitSumOddFormat", value);
    private static string LabelPalindromeDivisibleBy(int value)
        => LocalizationService.Format("NumberRain_LabelPalindromeDivisibleByFormat", value);
    private static string LabelContainsDigitAndDigitSumPrime(int digit)
        => LocalizationService.Format("NumberRain_LabelContainsDigitAndDigitSumPrimeFormat", digit);
    private static string LabelPrimeDigitSum(int sum)
        => LocalizationService.Format("NumberRain_LabelPrimeDigitSumFormat", sum);
    private static string LabelModuloDigitSumPrime(int modulo, int remainder)
        => LocalizationService.Format("NumberRain_LabelModuloDigitSumPrimeFormat", modulo, remainder);
    private static string LabelContainsDigitModuloOdd(int digit, int modulo, int remainder)
        => LocalizationService.Format("NumberRain_LabelContainsDigitModuloOddFormat", digit, modulo, remainder);
    private static string LabelHarshadWithoutTen() => LocalizationService.GetString("NumberRain_LabelHarshadWithoutTen");
    private static string LabelPronic() => LocalizationService.GetString("NumberRain_LabelPronic");
    private static string LabelAutomorphic() => LocalizationService.GetString("NumberRain_LabelAutomorphic");
    private static string LabelPalindromeNotPrime() => LocalizationService.GetString("NumberRain_LabelPalindromeNotPrime");
    private static string LabelSquareContainsDigit(int digit)
        => LocalizationService.Format("NumberRain_LabelSquareContainsDigitFormat", digit);
    private static string LabelSemiPrime() => LocalizationService.GetString("NumberRain_LabelSemiPrime");
    private static string LabelSquareFree() => LocalizationService.GetString("NumberRain_LabelSquareFree");
    private static string LabelHits() => LocalizationService.GetString("NumberRain_LabelHits");
    private static string LabelRuleR() => LocalizationService.GetString("NumberRain_LabelRuleR");
    private static string LabelRuleHeavy() => LocalizationService.GetString("NumberRain_LabelRuleHeavy");
    private static string LabelRulePrimeOrMod3() => LocalizationService.GetString("NumberRain_LabelRulePrimeOrMod3");
    private static string LabelRuleR1Even() => LocalizationService.GetString("NumberRain_LabelRuleR1Even");
    private static string LabelRuleR2Prime() => LocalizationService.GetString("NumberRain_LabelRuleR2Prime");
    private static string LabelGreaterThanLastPick() => LocalizationService.GetString("NumberRain_LabelGreaterThanLastPick");
    private static string LabelDivisibleByLastPick() => LocalizationService.GetString("NumberRain_LabelDivisibleByLastPick");
    private static string LabelBombDigit(int digit)
        => LocalizationService.Format("NumberRain_LabelBombDigitFormat", digit);
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
