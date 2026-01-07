#nullable enable
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class CompleteSequenceGeneratorService
{
    public CompleteSequencePuzzle Generate(string difficultyKey, int seed)
    {
        var rnd = new Random(seed);
        difficultyKey = (difficultyKey ?? "easy").ToLowerInvariant();

        var (minLen, maxLen, maxValue) = difficultyKey switch
        {
            "easy" => (6, 7, 200),
            "normal" => (6, 8, 800),
            "hard" => (7, 9, 2000),
            "master" => (8, 10, 5000),
            _ => (6, 7, 200)
        };

        int length = rnd.Next(minLen, maxLen + 1);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var template = PickTemplate(difficultyKey, rnd);
            var sequence = template(rnd, length, maxValue);
            if (sequence == null) continue;

            int missingIndex = rnd.Next(1, sequence.Count - 1);
            int correct = sequence[missingIndex];
            var options = BuildOptions(rnd, sequence, missingIndex, correct);

            return new CompleteSequencePuzzle
            {
                Sequence = sequence,
                MissingIndex = missingIndex,
                CorrectAnswer = correct,
                Options = options
            };
        }

        var fallback = GenerateArithmetic(rnd, length, maxValue) ?? new List<int> { 1, 2, 3, 4, 5, 6 };
        int fallbackMissing = Math.Clamp(length / 2, 1, fallback.Count - 2);
        int fallbackCorrect = fallback[fallbackMissing];

        return new CompleteSequencePuzzle
        {
            Sequence = fallback,
            MissingIndex = fallbackMissing,
            CorrectAnswer = fallbackCorrect,
            Options = BuildOptions(rnd, fallback, fallbackMissing, fallbackCorrect)
        };
    }

    private static Func<Random, int, int, List<int>?> PickTemplate(string difficultyKey, Random rnd)
        => difficultyKey switch
        {
            "easy" => rnd.Next(100) < 70 ? GenerateArithmetic : GenerateGeometric,
            "normal" => rnd.Next(100) switch
            {
                < 50 => GenerateArithmetic,
                < 75 => GenerateGeometric,
                _ => GenerateAlternating
            },
            "hard" => rnd.Next(100) switch
            {
                < 40 => GenerateQuadratic,
                < 70 => GenerateAlternating,
                _ => GenerateFibonacci
            },
            "master" => rnd.Next(100) < 55 ? GenerateQuadratic : GenerateFibonacci,
            _ => GenerateArithmetic
        };

    private static List<int>? GenerateArithmetic(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            int a = rnd.Next(0, 60);
            int d = rnd.Next(-12, 13);
            if (d == 0) continue;

            var values = new List<int>(length);
            bool ok = true;
            for (int i = 0; i < length; i++)
            {
                int value = a + i * d;
                if (value < 0 || value > maxValue) { ok = false; break; }
                values.Add(value);
            }
            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateGeometric(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            int a = rnd.Next(1, 8);
            int r = rnd.Next(2, 5);
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                double value = a * Math.Pow(r, i);
                if (value > maxValue) { ok = false; break; }
                values.Add((int)value);
            }
            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateAlternating(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int start = rnd.Next(0, 50);
            int p = rnd.Next(-10, 11);
            int q = rnd.Next(-10, 11);
            if (p == 0 || q == 0 || p == q) continue;

            var values = new List<int>(length) { start };
            bool ok = true;
            for (int i = 1; i < length; i++)
            {
                int step = i % 2 == 1 ? p : q;
                int next = values[i - 1] + step;
                if (next < 0 || next > maxValue) { ok = false; break; }
                values.Add(next);
            }
            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateQuadratic(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int a = rnd.Next(1, 4);
            int b = rnd.Next(-6, 7);
            int c = rnd.Next(0, 40);

            var values = new List<int>(length);
            bool ok = true;
            for (int i = 0; i < length; i++)
            {
                int value = (a * i * i) + (b * i) + c;
                if (value < 0 || value > maxValue) { ok = false; break; }
                values.Add(value);
            }
            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateFibonacci(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int x0 = rnd.Next(1, 10);
            int x1 = rnd.Next(1, 12);
            var values = new List<int>(length) { x0, x1 };

            bool ok = true;
            while (values.Count < length)
            {
                int next = values[^1] + values[^2];
                if (next > maxValue) { ok = false; break; }
                values.Add(next);
            }
            if (ok) return values;
        }
        return null;
    }

    private static IReadOnlyList<int> BuildOptions(Random rnd, List<int> sequence, int missingIndex, int correct)
    {
        var options = new HashSet<int> { correct };
        var candidates = new List<int>();

        if (missingIndex > 0) candidates.Add(sequence[missingIndex - 1]);
        if (missingIndex < sequence.Count - 1) candidates.Add(sequence[missingIndex + 1]);

        candidates.Add(correct + rnd.Next(1, 4));
        candidates.Add(correct - rnd.Next(1, 4));

        while (options.Count < 4)
        {
            int candidate;
            if (candidates.Count > 0)
            {
                int idx = rnd.Next(candidates.Count);
                candidate = candidates[idx];
                candidates.RemoveAt(idx);
            }
            else
            {
                candidate = correct + rnd.Next(-10, 11);
            }

            if (candidate < 0) continue;
            options.Add(candidate);
        }

        return options.OrderBy(_ => rnd.Next()).ToList();
    }
}
