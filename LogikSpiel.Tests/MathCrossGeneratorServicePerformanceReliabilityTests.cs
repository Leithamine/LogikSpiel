using System.Diagnostics;
using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServicePerformanceReliabilityTests
{
    private static (int MinEquations, int MaxEquations) GetEquationRange(string difficulty)
    {
        var type = typeof(MathCrossGeneratorService);
        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");
        var settings = getSettings.Invoke(null, new object[] { difficulty })
            ?? throw new InvalidOperationException("Settings creation failed.");

        int min = (int)settings.GetType().GetProperty("MinEquations")!.GetValue(settings)!;
        int max = (int)settings.GetType().GetProperty("MaxEquations")!.GetValue(settings)!;
        return (min, max);
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    public void GenerateGame_CompletesWithinReasonableBound(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var sw = Stopwatch.StartNew();

        for (int seed = 1; seed <= 6; seed++)
            _ = service.GenerateGame(difficulty, seed);

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"Generation too slow for {difficulty}: {sw.Elapsed}.");
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_HasValidEquationCount(string difficulty)
    {
        var service = new MathCrossGeneratorService();

        for (int seed = 1; seed <= 40; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);
            var (minEq, maxEq) = GetEquationRange(difficulty);
            Assert.InRange(game.Equations.Count, minEq, maxEq);
            Assert.True(game.Rows > 0 && game.Cols > 0);
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_StaysWithinCompactBounds(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        int maxSize = difficulty is "hard" or "master" ? 16 : 14;

        for (int seed = 1; seed <= 30; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);
            Assert.InRange(game.Rows, 1, 24); // GridSize
            Assert.InRange(game.Cols, 1, 24); // GridSize
            Assert.True(Math.Abs(game.Rows - game.Cols) <= 6,
                $"Layout too elongated for {difficulty}, seed={seed}: rows={game.Rows}, cols={game.Cols}.");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_HasMinimumFillRatio(string difficulty)
    {
        var service = new MathCrossGeneratorService();

        for (int seed = 1; seed <= 40; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);
            int occupied = 0;
            for (int r = 0; r < game.Rows; r++)
            {
                for (int c = 0; c < game.Cols; c++)
                {
                    if (game.Grid[r, c].Type != CellType.Empty)
                        occupied++;
                }
            }

            double fillRatio = (double)occupied / Math.Max(1, game.Rows * game.Cols);
            Assert.True(fillRatio >= 0.15,
                $"Layout too sparse for {difficulty}, seed={seed}: fillRatio={fillRatio:0.00}, rows={game.Rows}, cols={game.Cols}.");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("hard")]
    public void FallbackTemplate_IsInBounds_AndFinalizes(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var type = typeof(MathCrossGeneratorService);

        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");
        var settings = getSettings.Invoke(null, new object[] { difficulty })
            ?? throw new InvalidOperationException("Settings creation failed.");

        var fallbackMethod = type.GetMethod("GenerateFallbackGrid", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenerateFallbackGrid method not found.");
        var game = (MathCrossGame)fallbackMethod.Invoke(service, new object[] { settings, new Random(11) })!;

        var (minEq, maxEq) = GetEquationRange(difficulty);
        Assert.InRange(game.Equations.Count, minEq, maxEq);

        var finalizeMethod = type.GetMethod("FinalizeGame", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FinalizeGame method not found.");

        var ex = Record.Exception(() => finalizeMethod.Invoke(service, new object[] { game, new Random(13), settings }));
        Assert.Null(ex);
    }
}
