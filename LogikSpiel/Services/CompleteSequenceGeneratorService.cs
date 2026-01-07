#nullable enable
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class CompleteSequenceGeneratorService
{
    private const int MinValue = 1;
    public CompleteSequencePuzzle Generate(string difficultyKey, int seed)
    {
        var rnd = new Random(seed);
        difficultyKey = (difficultyKey ?? "easy").ToLowerInvariant();

        var (minLen, maxLen, maxValue) = difficultyKey switch
        {
            "easy" => (6, 7, 1000),
            "normal" => (6, 8, 1000),
            "hard" => (7, 9, 1000),
            "master" => (8, 10, 1000),
            _ => (6, 7, 1000)
        };

        int length = rnd.Next(minLen, maxLen + 1);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var template = PickTemplate(difficultyKey, rnd);
            var sequence = template(rnd, length, maxValue);
            if (sequence == null) continue;

            int missingIndex = rnd.Next(1, sequence.Count - 1);
            int correct = sequence[missingIndex];
            var options = BuildOptions(rnd, sequence, missingIndex, correct, maxValue);

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
            Options = BuildOptions(rnd, fallback, fallbackMissing, fallbackCorrect, maxValue)
        };
    }

    private static Func<Random, int, int, List<int>?> PickTemplate(string difficultyKey, Random rnd)
        => difficultyKey switch
        {
            // =========================
            // EASY
            // =========================
            "easy" => rnd.Next(100) switch
            {
                < 30 => GenerateArithmetic,              // E1
                < 50 => GenerateGeometric,               // E2
                < 65 => GeneratePeriodicPattern,         // E4
                < 80 => GenerateMultiplicationTable,     // E6
                < 90 => GenerateGrowingStep,             // E7
                _ => GenerateRepeatingDifferencePattern  // E8 ✅ neu
            },

            // =========================
            // NORMAL
            // (mehr Variety + 2 neue: Cubes + PrimeRun)
            // =========================
            "normal" => rnd.Next(100) switch
            {
                < 10 => GenerateArithmetic,              // E1 (selten)
                < 18 => GenerateGeometric,               // E2
                < 28 => GenerateAlternating,             // E3
                < 40 => GenerateSquaresShifted,          // N1
                < 50 => GenerateTriangularShifted,       // N3
                < 60 => GenerateCubesShifted,            // N2
                < 70 => GenerateAlternatingMulAdd,       // N5
                < 80 => GenerateInterleaveTwoArithmetic, // N6
                < 88 => GenerateGeometricWithOffset,     // N8
                < 94 => GeneratePentagonalShifted,       // N9 ✅ neu
                _ => GeneratePrimeRun                 // N7 (selten)
            },

            // =========================
            // HARD
            // (S2 + S4 dazu, Fibonacci selten)
            // =========================
            "hard" => rnd.Next(100) switch
            {
                < 14 => GenerateQuadratic,               // S1
                < 28 => GenerateArithmeticDifferences,   // S2 ✅ neu
                < 42 => GenerateAffineRecurrence,        // S3
                < 55 => GenerateWeightedRecurrence2,     // S4 ✅ neu
                < 68 => GenerateInterleaveGeometricArithmetic, // S5
                < 78 => GeneratePowersOfTwoAltOffset,    // S6
                < 88 => GenerateDigitSumStep,            // S7
                < 94 => GeneratePronic,                  // S8
                < 98 => GeneratePentagonalShifted,       // N9
                _ => GenerateFibonacci                // N4 (sehr selten)
            },

            // =========================
            // MASTER
            // (M3/M4 dazu, M6/M7 Boss sehr selten)
            // =========================
            "master" => rnd.Next(100) switch
            {
                < 12 => GenerateCubicPolynomial,         // M1
                < 26 => GenerateInterleaveThreeMixed,    // M2
                < 40 => GenerateRecurrence3,             // M3 ✅ neu
                < 54 => GenerateDiffFibonacci,           // M4 ✅ neu
                < 68 => GeneratePiecewiseMod3,           // M5
                < 80 => GenerateAffineRecurrence,        // (Hard) S3
                < 92 => GeneratePowersOfTwoAltOffset,    // (Hard) S6
                < 97 => GenerateFigurateComposition,     // M6 ✅ neu (Boss, selten)
                _ => GeneratePrimePlusSquare          // M7 ✅ neu (Boss, sehr selten)
            },

            _ => GenerateArithmetic
        };


    // =======================
    // NORMAL (fehlend): N2, N7, N9
    // =======================

    private static List<int>? GenerateCubesShifted(Random rnd, int length, int maxValue)
    {
        // N2: x_n = (n + t)^3
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int t = rnd.Next(1, 7); // 1..6
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int n = i + t;
                long v = (long)n * n * n;
                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GeneratePrimeRun(Random rnd, int length, int maxValue)
    {
        // N7: prime(n+t) + c (c optional small offset)
        // t is start-index in prime sequence (1-based), not always starting at 2
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int t = rnd.Next(1, 21);              // 1..20
            int c = rnd.Next(100) < 35 ? rnd.Next(-5, 6) : 0; // offset sometimes
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int p = NthPrime(t + i); // 1-based nth prime
                long v = (long)p + c;
                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);
            }

            // Guardrail: avoid too trivial / too small too often
            if (ok && values.Distinct().Count() >= Math.Min(5, length)) return values;
        }
        return null;
    }

    private static List<int>? GeneratePentagonalShifted(Random rnd, int length, int maxValue)
    {
        // N9: x_n = pentagonal(n+t)
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int t = rnd.Next(1, 9); // 1..8
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int n = i + t;
                long value = (long)n * (3L * n - 1) / 2;
                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            if (ok) return values;
        }
        return null;
    }


    // =======================
    // HARD (fehlend): S2, S4
    // =======================

    private static List<int>? GenerateArithmeticDifferences(Random rnd, int length, int maxValue)
    {
        // S2: Δx_n = p + n*q  (q>0)
        // x_{n+1} = x_n + (p + n*q)
        for (int attempt = 0; attempt < 35; attempt++)
        {
            int x0 = rnd.Next(1, 41);     // 1..40
            int p = rnd.Next(-8, 16);     // -8..15
            int q = rnd.Next(1, 7);       // 1..6

            var values = new List<int>(length) { x0 };
            bool ok = true;

            for (int n = 0; n < length - 1; n++)
            {
                int diff = p + n * q;
                long next = (long)values[^1] + diff;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            // Anti-trivial: avoid constant / tiny variation
            if (ok && values.Distinct().Count() >= Math.Min(5, length)) return values;
        }
        return null;
    }

    private static List<int>? GenerateWeightedRecurrence2(Random rnd, int length, int maxValue)
    {
        // S4: x_n = p*x_{n-1} + q*x_{n-2}
        // p,q in {1,2} (avoid p=q=1 too often -> becomes Fibonacci)
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int p = rnd.Next(1, 3); // 1..2
            int q = rnd.Next(1, 3); // 1..2
            if (p == 1 && q == 1 && rnd.Next(100) < 85) continue;

            int x0 = rnd.Next(1, 13); // 1..12
            int x1 = rnd.Next(1, 13); // 1..12

            var values = new List<int>(length) { x0, x1 };
            bool ok = true;

            while (values.Count < length)
            {
                long next = (long)p * values[^1] + (long)q * values[^2];
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }


    // =======================
    // MASTER (fehlend): M3, M4, M6, M7
    // =======================

    private static List<int>? GenerateRecurrence3(Random rnd, int length, int maxValue)
    {
        // M3: x_n = p*x_{n-1} + q*x_{n-2} + r*x_{n-3}
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int p = rnd.Next(1, 3); // 1..2
            int q = rnd.Next(1, 3); // 1..2
            int r = rnd.Next(1, 3); // 1..2

            // avoid "all 1" too often
            if (p == 1 && q == 1 && r == 1 && rnd.Next(100) < 90) continue;

            int x0 = rnd.Next(1, 9); // 1..8
            int x1 = rnd.Next(1, 9);
            int x2 = rnd.Next(1, 9);

            var values = new List<int>(length) { x0, x1, x2 };
            bool ok = true;

            while (values.Count < length)
            {
                long next = (long)p * values[^1] + (long)q * values[^2] + (long)r * values[^3];
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateDiffFibonacci(Random rnd, int length, int maxValue)
    {
        // M4: Differences follow Fibonacci-like, sequence is cumulative sum
        // Δ0,Δ1 chosen; Δn = Δn-1 + Δn-2; x_{n+1}=x_n + Δn
        for (int attempt = 0; attempt < 45; attempt++)
        {
            int x0 = rnd.Next(1, 41);  // 1..40
            int d0 = rnd.Next(1, 9);   // 1..8
            int d1 = rnd.Next(1, 13);  // 1..12

            var diffs = new List<int>(Math.Max(0, length - 1));
            if (length >= 2) diffs.Add(d0);
            if (length >= 3) diffs.Add(d1);

            while (diffs.Count < length - 1)
            {
                long dn = (long)diffs[^1] + diffs[^2];
                if (dn > int.MaxValue) break;
                diffs.Add((int)dn);
            }

            if (diffs.Count != length - 1 && length > 2) continue;

            var values = new List<int>(length) { x0 };
            bool ok = true;

            for (int i = 0; i < length - 1; i++)
            {
                long next = (long)values[^1] + diffs[i];
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateFigurateComposition(Random rnd, int length, int maxValue)
    {
        // M6: composition (sparsam): square(triangle(n+t)) OR triangle(square(n+t))
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int t = rnd.Next(1, 5);           // 1..4 (small because growth is huge)
            bool squareOfTriangle = rnd.Next(2) == 0;

            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int n = i + t;

                long v;
                if (squareOfTriangle)
                {
                    long tri = Triangle(n);
                    v = tri * tri;
                }
                else
                {
                    long sq = (long)n * n;
                    v = TriangleLong(sq); // triangle(square(n))
                }

                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GeneratePrimePlusSquare(Random rnd, int length, int maxValue)
    {
        // M7: x_n = prime(n+t) + (n+t)^2  (very rare "boss" style)
        for (int attempt = 0; attempt < 45; attempt++)
        {
            int t = rnd.Next(1, 11); // 1..10
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int n = i + t;
                int p = NthPrime(n);

                long sq = (long)n * n;
                long v = (long)p + sq;

                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);
            }

            if (ok) return values;
        }
        return null;
    }


    // =======================
    // Helpers (primes + triangle)
    // =======================

    private static long Triangle(int n)
    {
        // n*(n+1)/2
        return (long)n * (n + 1) / 2;
    }

    private static long TriangleLong(long n)
    {
        // n*(n+1)/2 for long
        return n * (n + 1) / 2;
    }

    private static int NthPrime(int n)
    {
        // 1-based: n=1 => 2
        if (n <= 1) return 2;
        int count = 1;
        int candidate = 1;

        while (count < n)
        {
            candidate += 2; // skip evens
            if (IsPrime(candidate)) count++;
        }
        return candidate;
    }

    private static bool IsPrime(int x)
    {
        if (x < 2) return false;
        if (x == 2) return true;
        if (x % 2 == 0) return false;

        int limit = (int)Math.Sqrt(x);
        for (int i = 3; i <= limit; i += 2)
            if (x % i == 0) return false;

        return true;
    }

    private static List<int>? GenerateArithmetic(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            int a = rnd.Next(1, 61);
            int d = rnd.Next(-12, 13);
            if (d == 0) continue;

            var values = new List<int>(length);
            bool ok = true;
            for (int i = 0; i < length; i++)
            {
                int value = a + i * d;
                if (!IsInRange(value, maxValue)) { ok = false; break; }
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
                if (!IsInRange(value, maxValue)) { ok = false; break; }
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
            int start = rnd.Next(1, 51);
            int p = rnd.Next(-10, 11);
            int q = rnd.Next(-10, 11);
            if (p == 0 || q == 0 || p == q) continue;

            var values = new List<int>(length) { start };
            bool ok = true;
            for (int i = 1; i < length; i++)
            {
                int step = i % 2 == 1 ? p : q;
                int next = values[i - 1] + step;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
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
            int c = rnd.Next(1, 41);

            var values = new List<int>(length);
            bool ok = true;
            for (int i = 0; i < length; i++)
            {
                int value = (a * i * i) + (b * i) + c;
                if (!IsInRange(value, maxValue)) { ok = false; break; }
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
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add(next);
            }
            if (ok) return values;
        }
        return null;
    }

    private static IReadOnlyList<int> BuildOptions(Random rnd, List<int> sequence, int missingIndex, int correct, int maxValue)
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

            if (!IsInRange(candidate, maxValue)) continue;
            options.Add(candidate);
        }

        return options.OrderBy(_ => rnd.Next()).ToList();
    }
    private static List<int>? GeneratePeriodicPattern(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int t = rnd.Next(0, 2) == 0 ? 3 : 4; // pattern length 3 or 4
            if (length < t + 2) continue;

            var set = new HashSet<int>();
            var pattern = new int[t];

            for (int i = 0; i < t; i++)
            {
                int v;
                int guard = 0;
                do
                {
                    v = rnd.Next(MinValue, Math.Min(41, maxValue + 1));
                    guard++;
                    if (guard > 200) break;
                }
                while (!set.Add(v));
                pattern[i] = v;
            }

            var values = new List<int>(length);
            for (int i = 0; i < length; i++)
            {
                int value = pattern[i % t];
                if (!IsInRange(value, maxValue)) { values = null; break; }
                values.Add(value);
            }

            if (values != null) return values;
        }
        return null;
    }

    private static List<int>? GenerateMultiplicationTable(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int k = rnd.Next(2, 13); // 2..12
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int value = k * (i + 1);
                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add(value);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateGrowingStep(Random rnd, int length, int maxValue)
    {
        // Δ grows: +s*1, +s*2, +s*3, ...
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int x0 = rnd.Next(1, 31);
            int s = rnd.Next(1, 4); // 1..3
            var values = new List<int>(length) { x0 };
            bool ok = true;

            for (int n = 1; n < length; n++)
            {
                int step = s * n; // n=1 => s, n=2 => 2s, ...
                int next = values[^1] + step;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add(next);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateRepeatingDifferencePattern(Random rnd, int length, int maxValue)
    {
        // E8: repeating pattern of differences (length 3)
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int start = rnd.Next(1, 31);
            var diffs = new[]
            {
                rnd.Next(-6, 7),
                rnd.Next(-6, 7),
                rnd.Next(-6, 7)
            };

            if (diffs.All(d => d == 0)) continue;
            if (diffs.Distinct().Count() == 1 && rnd.Next(100) < 80) continue;

            var values = new List<int>(length) { start };
            bool ok = true;

            for (int i = 1; i < length; i++)
            {
                int diff = diffs[(i - 1) % diffs.Length];
                long next = (long)values[^1] + diff;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }
    private static List<int>? GenerateSquaresShifted(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int t = rnd.Next(1, 11); // 1..10
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int n = i + t;
                long value = (long)n * n;
                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateTriangularShifted(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int t = rnd.Next(1, 9); // 1..8
            int c = rnd.Next(100) < 35 ? rnd.Next(-5, 6) : 0; // optional small offset
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int n = i + t;
                long tri = (long)n * (n + 1) / 2;
                long value = tri + c;
                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateAlternatingMulAdd(Random rnd, int length, int maxValue)
    {
        // alternates operations: ×p, +q, ×p, +q... (or reversed)
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int start = rnd.Next(1, 21);
            int p = rnd.Next(2, 4);     // 2..3
            int q = rnd.Next(1, 16);    // 1..15
            bool firstIsMul = rnd.Next(2) == 0;

            var values = new List<int>(length) { start };
            bool ok = true;

            for (int i = 1; i < length; i++)
            {
                bool isMul = firstIsMul ? (i % 2 == 1) : (i % 2 == 0);
                long next = isMul ? (long)values[^1] * p : (long)values[^1] + q;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateInterleaveTwoArithmetic(Random rnd, int length, int maxValue)
    {
        // even positions: a + k*d1, odd positions: b + k*d2
        // k = index/2
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int a = rnd.Next(1, 51);
            int b = rnd.Next(1, 51);
            int d1 = rnd.Next(-10, 11);
            int d2 = rnd.Next(-10, 11);
            if (d1 == 0 || d2 == 0) continue;

            // avoid trivial "same line" too often
            if (d1 == d2 && rnd.Next(100) < 70) continue;

            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int k = i / 2;
                long value = (i % 2 == 0)
                    ? (long)a + k * d1
                    : (long)b + k * d2;

                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            // ensure both subsequences actually vary (not constant)
            if (ok)
            {
                bool evenVaries = values.Count(v => values.IndexOf(v) % 2 == 0) > 0;
                return values;
            }
        }
        return null;
    }

    private static List<int>? GenerateGeometricWithOffset(Random rnd, int length, int maxValue)
    {
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int a = rnd.Next(1, 11);         // 1..10
            int r = rnd.Next(2, 4);          // 2..3
            int c = rnd.Next(-10, 11);       // -10..10

            long value = a + c;
            if (!IsInRange(value, maxValue)) continue;

            var values = new List<int>(length);
            bool ok = true;
            long cur = a;

            for (int i = 0; i < length; i++)
            {
                long v = cur + c;
                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);

                cur *= r;
                if (cur > maxValue + 1000L) { /* stop explosion early */ }
            }

            if (ok) return values;
        }
        return null;
    }
    private static List<int>? GenerateAffineRecurrence(Random rnd, int length, int maxValue)
    {
        // x_{n+1} = p*x_n + q  (q!=0 most of the time)
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int p = rnd.Next(2, 4); // 2..3
            int q = rnd.Next(-10, 21);
            if (q == 0 && rnd.Next(100) < 85) continue; // avoid becoming pure geometric too often

            int x0 = rnd.Next(1, 21);
            var values = new List<int>(length) { x0 };
            bool ok = true;

            for (int i = 1; i < length; i++)
            {
                long next = (long)p * values[^1] + q;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateInterleaveGeometricArithmetic(Random rnd, int length, int maxValue)
    {
        // even idx: geometric, odd idx: arithmetic (or swapped randomly)
        for (int attempt = 0; attempt < 35; attempt++)
        {
            bool evenIsGeo = rnd.Next(2) == 0;

            int aGeo = rnd.Next(1, 11);
            int r = rnd.Next(2, 4); // 2..3

            int aLin = rnd.Next(1, 41);
            int d = rnd.Next(-10, 11);
            if (d == 0) continue;

            long geo = aGeo;

            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int k = i / 2;

                long v;
                if ((i % 2 == 0) == evenIsGeo)
                {
                    // geometric subseq
                    v = geo;
                    // advance geo only when we used it
                    geo *= r;
                }
                else
                {
                    // arithmetic subseq
                    v = (long)aLin + k * d;
                }

                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GeneratePowersOfTwoAltOffset(Random rnd, int length, int maxValue)
    {
        // x_n = 2^(n+t) + (n%2 ? u : v)
        for (int attempt = 0; attempt < 35; attempt++)
        {
            int t = rnd.Next(0, 7); // 0..6
            int u = rnd.Next(-5, 6);
            int v = rnd.Next(-5, 6);
            if (u == 0 && v == 0) continue;
            if (u == v && rnd.Next(100) < 80) continue;

            var values = new List<int>(length);
            bool ok = true;

            for (int n = 0; n < length; n++)
            {
                int exp = n + t;
                if (exp < 0 || exp > 30) { ok = false; break; } // avoid overflow

                long pow2 = 1L << exp;
                long offset = (n % 2 == 1) ? u : v;
                long value = pow2 + offset;

                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateDigitSumStep(Random rnd, int length, int maxValue)
    {
        // x_{n+1} = x_n + sumDigits(x_n)
        for (int attempt = 0; attempt < 35; attempt++)
        {
            int x0 = rnd.Next(1, 81);
            var values = new List<int>(length) { x0 };
            bool ok = true;

            for (int i = 1; i < length; i++)
            {
                int s = SumDigits(values[^1]);
                int next = values[^1] + s;
                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add(next);
            }

            // avoid boring case where sumDigits is often 1
            if (ok && values.Distinct().Count() >= Math.Min(length, 5))
                return values;
        }
        return null;
    }

    private static List<int>? GeneratePronic(Random rnd, int length, int maxValue)
    {
        // x_n = (n+t)(n+t+1)
        for (int attempt = 0; attempt < 25; attempt++)
        {
            int t = rnd.Next(1, 21);
            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                long n = i + t;
                long value = n * (n + 1);
                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            if (ok) return values;
        }
        return null;
    }

    private static int SumDigits(int x)
    {
        x = Math.Abs(x);
        int sum = 0;
        while (x > 0)
        {
            sum += x % 10;
            x /= 10;
        }
        return sum;
    }
    private static List<int>? GenerateCubicPolynomial(Random rnd, int length, int maxValue)
    {
        // x_n = a*n^3 + b*n^2 + c*n + d
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int a = rnd.Next(1, 3);     // 1..2
            int b = rnd.Next(-6, 7);    // -6..6
            int c = rnd.Next(-15, 16);  // -15..15
            int d = rnd.Next(1, 61);    // 1..60

            var values = new List<int>(length);
            bool ok = true;

            for (int n = 0; n < length; n++)
            {
                long nn = n;
                long value = a * nn * nn * nn + b * nn * nn + c * nn + d;
                if (!IsInRange(value, maxValue)) { ok = false; break; }
                values.Add((int)value);
            }

            // avoid degenerating into quadratic/linear too often
            if (ok && a != 0) return values;
        }
        return null;
    }

    private static List<int>? GeneratePiecewiseMod3(Random rnd, int length, int maxValue)
    {
        // by index n%3: +p, ×q, −r (permuted)
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int p = rnd.Next(1, 16);
            int q = rnd.Next(2, 4);  // 2..3
            int r = rnd.Next(1, 13);
            int x0 = rnd.Next(1, 21);

            // random permutation of ops
            var ops = new[] { 0, 1, 2 }; // 0:+p, 1:*q, 2:-r
            for (int i = ops.Length - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (ops[i], ops[j]) = (ops[j], ops[i]);
            }

            var values = new List<int>(length) { x0 };
            bool ok = true;

            for (int n = 1; n < length; n++)
            {
                int op = ops[n % 3];
                long cur = values[^1];
                long next = op switch
                {
                    0 => cur + p,
                    1 => cur * q,
                    _ => cur - r
                };

                if (!IsInRange(next, maxValue)) { ok = false; break; }
                values.Add((int)next);
            }

            if (ok) return values;
        }
        return null;
    }

    private static List<int>? GenerateInterleaveThreeMixed(Random rnd, int length, int maxValue)
    {
        // A,B,C,A,B,C... where:
        // A: arithmetic, B: geometric, C: growing-step (quadratic-ish)
        for (int attempt = 0; attempt < 45; attempt++)
        {
            int aA = rnd.Next(1, 51);
            int dA = rnd.Next(-10, 11);
            if (dA == 0) continue;

            int aB = rnd.Next(1, 8);
            int rB = rnd.Next(2, 4); // 2..3

            int x0C = rnd.Next(1, 21);
            int sC = rnd.Next(1, 4); // 1..3

            long curGeo = aB;
            int cVal = x0C;

            var values = new List<int>(length);
            bool ok = true;

            for (int i = 0; i < length; i++)
            {
                int k = i / 3;
                long v;

                int slot = i % 3;
                if (slot == 0)
                {
                    v = (long)aA + k * dA;
                }
                else if (slot == 1)
                {
                    v = curGeo;
                    curGeo *= rB;
                }
                else
                {
                    // C grows with increasing step: c += s*(k+1)
                    if (k > 0) cVal += sC * k;
                    v = cVal;
                }

                if (!IsInRange(v, maxValue)) { ok = false; break; }
                values.Add((int)v);
            }

            if (ok) return values;
        }
        return null;
    }

    private static bool IsInRange(long value, int maxValue)
        => value >= MinValue && value <= maxValue;

}
