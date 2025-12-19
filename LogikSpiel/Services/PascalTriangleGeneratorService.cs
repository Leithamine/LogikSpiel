#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class PascalTriangleGeneratorService
{
    public PascalTriangleGame GenerateGame(string difficultyKey, int seed)
    {
        var rnd = new Random(seed);

        var (rows, generator) = GetSettings(difficultyKey, rnd);

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

    private static (int rows, Func<decimal> generator) GetSettings(string key, Random rnd)
    {
        key = (key ?? "easy").ToLowerInvariant();

        return key switch
        {
            "easy" => (rnd.Next(5, 8), () => rnd.Next(1, 11)), // 1..10

            "normal" => (rnd.Next(5, 8), () =>
            {
                // 50% int 1..50, 50% Komma 1.1..9.9
                if (rnd.Next(2) == 0) return rnd.Next(1, 51);

                int whole = rnd.Next(1, 10);      // 1..9
                int frac = rnd.Next(1, 10);       // 1..9
                return whole + (frac / 10m);
            }
            ),

            "hard" => (rnd.Next(7, 11), () =>
            {
                // 50% +1..100, 50% -1..-100
                int v = rnd.Next(1, 101);
                return rnd.Next(2) == 0 ? v : -v;
            }
            ),

            "master" => (rnd.Next(10, 16), () =>
            {
                // Mischung -100..100, teils Komma
                int whole = rnd.Next(-100, 101);

                if (rnd.Next(10) < 7) return whole; // 70% int

                int frac = rnd.Next(1, 10); // 0.1..0.9
                return whole >= 0 ? whole + frac / 10m : whole - frac / 10m;
            }
            ),

            _ => (rnd.Next(5, 8), () => rnd.Next(1, 11))
        };
    }

    private static (decimal sum, List<(int row, int col)> path) FindMaxPath(List<List<decimal>> triangle)
    {
        int n = triangle.Count;
        if (n == 0) return (0, new());

        var dp = new decimal[n][];
        for (int i = 0; i < n; i++) dp[i] = new decimal[i + 1];

        dp[0][0] = triangle[0][0];

        for (int row = 1; row < n; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                decimal cur = triangle[row][col];

                if (col == 0) dp[row][col] = dp[row - 1][0] + cur;
                else if (col == row) dp[row][col] = dp[row - 1][col - 1] + cur;
                else dp[row][col] = Math.Max(dp[row - 1][col - 1], dp[row - 1][col]) + cur;
            }
        }

        decimal maxSum = dp[n - 1].Max();
        int maxCol = Array.IndexOf(dp[n - 1], maxSum);

        var path = new List<(int row, int col)>();
        int c = maxCol;

        for (int row = n - 1; row >= 0; row--)
        {
            path.Add((row, c));
            if (row == 0) break;

            if (c == 0) c = 0;
            else if (c == row) c--;
            else if (dp[row - 1][c - 1] > dp[row - 1][c]) c--;
        }

        path.Reverse();
        return (maxSum, path);
    }

    private static (decimal sum, List<(int row, int col)> path) FindMinPath(List<List<decimal>> triangle)
    {
        int n = triangle.Count;
        if (n == 0) return (0, new());

        var dp = new decimal[n][];
        for (int i = 0; i < n; i++) dp[i] = new decimal[i + 1];

        dp[0][0] = triangle[0][0];

        for (int row = 1; row < n; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                decimal cur = triangle[row][col];

                if (col == 0) dp[row][col] = dp[row - 1][0] + cur;
                else if (col == row) dp[row][col] = dp[row - 1][col - 1] + cur;
                else dp[row][col] = Math.Min(dp[row - 1][col - 1], dp[row - 1][col]) + cur;
            }
        }

        decimal minSum = dp[n - 1].Min();
        int minCol = Array.IndexOf(dp[n - 1], minSum);

        var path = new List<(int row, int col)>();
        int c = minCol;

        for (int row = n - 1; row >= 0; row--)
        {
            path.Add((row, c));
            if (row == 0) break;

            if (c == 0) c = 0;
            else if (c == row) c--;
            else if (dp[row - 1][c - 1] < dp[row - 1][c]) c--;
        }

        path.Reverse();
        return (minSum, path);
    }
}
