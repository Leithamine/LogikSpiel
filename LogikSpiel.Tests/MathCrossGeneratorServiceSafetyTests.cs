using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceSafetyTests
{
    [Theory]
    [InlineData("hard")]
    [InlineData("master")]
    public void Reverse_EdgeTargets_DoesNotThrowAndReturnsBoundedPairs(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var type = typeof(MathCrossGeneratorService);

        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");
        var settings = getSettings.Invoke(null, new object[] { difficulty })
            ?? throw new InvalidOperationException("Settings could not be created.");

        var reverse = type.GetMethod("Reverse", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Reverse method not found.");

        var rnd = new Random(321);
        foreach (var c in new[] { -1, 0, 1, 2, 12 })
        {
            foreach (var op in new[] { "+", "-", "×", "÷" })
            {
                var result = reverse.Invoke(service, new object[] { c, op, settings, rnd });
                if (result is null) continue;

                dynamic pair = result;
                int a = pair.Item1;
                int b = pair.Item2;
                Assert.InRange(a, -1, 99);
                Assert.InRange(b, -1, 99);
            }
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_NoAdjacentEquationGlueInRowsOrColumns(string difficulty)
    {
        var service = new MathCrossGeneratorService();

        for (int seed = 1; seed <= 60; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);

            for (int r = 0; r < game.Rows; r++)
            {
                int runEquals = 0;
                for (int c = 0; c < game.Cols; c++)
                {
                    if (game.Grid[r, c].Type == CellType.Equals)
                    {
                        runEquals++;
                        Assert.True(runEquals <= 1, $"Row {r} has glued equations (seed={seed}, diff={difficulty}).");
                    }
                    else if (game.Grid[r, c].Type == CellType.Empty)
                    {
                        runEquals = 0;
                    }
                }
            }

            for (int c = 0; c < game.Cols; c++)
            {
                int runEquals = 0;
                for (int r = 0; r < game.Rows; r++)
                {
                    if (game.Grid[r, c].Type == CellType.Equals)
                    {
                        runEquals++;
                        Assert.True(runEquals <= 1, $"Column {c} has glued equations (seed={seed}, diff={difficulty}).");
                    }
                    else if (game.Grid[r, c].Type == CellType.Empty)
                    {
                        runEquals = 0;
                    }
                }
            }
        }
    }
}
