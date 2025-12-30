#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class PascalTriangleGeneratorService
{
    public PascalTriangleGame GenerateGame(string difficultyKey, int seed, int? maxRows = null)
    {
        var rnd = new Random(seed);

        var (rows, generator) = GetSettings(difficultyKey, rnd, maxRows);

        var triangle = new List<List<decimal>>();
        for (int row = 0; row < rows; row++)
        {
            var rowValues = new List<decimal>();
            for (int col = 0; col <= row; col++)
                rowValues.Add(generator());
            triangle.Add(rowValues);
        }

        var (maxSum, maxPath) = FindMaxPath(triangle);
        var (minSum, minPath) = FindMinPath(triangle);

        return new PascalTriangleGame
        {
            Triangle = triangle,
            Difficulty = difficultyKey,
            MaxPathSum = maxSum,
            MinPathSum = minSum,
            MaxPath = maxPath,
            MinPath = minPath
        };
    }

    private static (int rows, Func<decimal> generator) GetSettings(string key, Random rnd, int? maxRows)
    {
        key = (key ?? "easy").ToLowerInvariant();

        // ✅ REDUZIERTE Reihenanzahl für bessere Darstellung auf Mobilgeräten
        (int minR, int maxR, Func<decimal> gen) settings = key switch
        {
            "easy" => (5, 7, () => rnd.Next(1, 11)),

            "normal" => (6, 8, () =>
            {
                if (rnd.Next(2) == 0) return rnd.Next(1, 51);
                int whole = rnd.Next(1, 10);
                int frac = rnd.Next(1, 10);
                return whole + (frac / 10m);
            }
            ),

            "hard" => (7, 10, () =>
            {
                int v = rnd.Next(1, 101);
                return rnd.Next(2) == 0 ? v : -v;
            }
            ),

            // ✅ Master: maximal 12 Reihen statt 15
            "master" => (8, 12, () =>
            {
                int whole = rnd.Next(-100, 101);
                if (rnd.Next(10) < 7) return whole;
                int frac = rnd.Next(1, 10);
                return whole >= 0 ? whole + frac / 10m : whole - frac / 10m;
            }
            ),

            _ => (5, 7, () => rnd.Next(1, 11))
        };

        int minRows = settings.minR;
        int maxBase = settings.maxR;

        // Device-Limit anwenden
        int maxAllowed = maxRows.HasValue ? Math.Max(3, maxRows.Value) : maxBase;

        int actualMin = Math.Min(minRows, maxAllowed);
        int actualMax = Math.Min(maxBase, maxAllowed);

        if (actualMax < actualMin) actualMax = actualMin;

        int rows = rnd.Next(actualMin, actualMax + 1);
        return (rows, settings.gen);
    }

    private static (decimal sum, List<(int row, int col)> path) FindMaxPath(List<List<decimal>> tri)
    {
        int n = tri.Count;
        var dp = new decimal[n][];
        var parent = new int[n][];

        for (int r = 0; r < n; r++)
        {
            dp[r] = new decimal[r + 1];
            parent[r] = new int[r + 1];
            for (int c = 0; c <= r; c++) parent[r][c] = -1;
        }

        dp[0][0] = tri[0][0];

        for (int r = 1; r < n; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                decimal best = decimal.MinValue;
                int bestParent = -1;

                if (c - 1 >= 0)
                {
                    var cand = dp[r - 1][c - 1] + tri[r][c];
                    if (cand > best)
                    {
                        best = cand;
                        bestParent = c - 1;
                    }
                }

                if (c <= r - 1)
                {
                    var cand = dp[r - 1][c] + tri[r][c];
                    if (cand > best)
                    {
                        best = cand;
                        bestParent = c;
                    }
                }

                dp[r][c] = best;
                parent[r][c] = bestParent;
            }
        }

        int last = n - 1;
        int bestCol = 0;
        decimal bestSum = dp[last][0];
        for (int c = 1; c <= last; c++)
        {
            if (dp[last][c] > bestSum)
            {
                bestSum = dp[last][c];
                bestCol = c;
            }
        }

        var path = new List<(int row, int col)>();
        int colCur = bestCol;
        for (int r = last; r >= 0; r--)
        {
            path.Add((r, colCur));
            colCur = parent[r][colCur];
            if (r == 0) break;
        }
        path.Reverse();

        return (bestSum, path);
    }

    private static (decimal sum, List<(int row, int col)> path) FindMinPath(List<List<decimal>> tri)
    {
        int n = tri.Count;
        var dp = new decimal[n][];
        var parent = new int[n][];

        for (int r = 0; r < n; r++)
        {
            dp[r] = new decimal[r + 1];
            parent[r] = new int[r + 1];
            for (int c = 0; c <= r; c++) parent[r][c] = -1;
        }

        dp[0][0] = tri[0][0];

        for (int r = 1; r < n; r++)
        {
            for (int c = 0; c <= r; c++)
            {
                decimal best = decimal.MaxValue;
                int bestParent = -1;

                if (c - 1 >= 0)
                {
                    var cand = dp[r - 1][c - 1] + tri[r][c];
                    if (cand < best)
                    {
                        best = cand;
                        bestParent = c - 1;
                    }
                }

                if (c <= r - 1)
                {
                    var cand = dp[r - 1][c] + tri[r][c];
                    if (cand < best)
                    {
                        best = cand;
                        bestParent = c;
                    }
                }

                dp[r][c] = best;
                parent[r][c] = bestParent;
            }
        }

        int last = n - 1;
        int bestCol = 0;
        decimal bestSum = dp[last][0];
        for (int c = 1; c <= last; c++)
        {
            if (dp[last][c] < bestSum)
            {
                bestSum = dp[last][c];
                bestCol = c;
            }
        }

        var path = new List<(int row, int col)>();
        int colCur = bestCol;
        for (int r = last; r >= 0; r--)
        {
            path.Add((r, colCur));
            colCur = parent[r][colCur];
            if (r == 0) break;
        }
        path.Reverse();

        return (bestSum, path);
    }
}
