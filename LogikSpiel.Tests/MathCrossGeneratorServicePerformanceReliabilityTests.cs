using System.Diagnostics;
using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServicePerformanceReliabilityTests
{
    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    public void GenerateGame_CompletesWithinReasonableBound(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var sw = Stopwatch.StartNew();

        for (int seed = 1; seed <= 6; seed++)
        {
            _ = service.GenerateGame(difficulty, seed);
        }

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"Generation too slow for {difficulty}: {sw.Elapsed}.");
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_NeverReturnsInvalidFallback(string difficulty)
    {
        var service = new MathCrossGeneratorService();

        for (int seed = 20; seed <= 50; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);
            Assert.InRange(game.Equations.Count, 8, 12);
            Assert.True(InvokeEnsureSolvableCore(game, maxStrategicReveals: 0), $"Returned puzzle was not deductively solvable for seed={seed}, diff={difficulty}");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("hard")]
    public void FallbackTemplate_IsSolvableAndInBounds(string difficulty)
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

        Assert.InRange(game.Equations.Count, 8, 12);

        var finalizeMethod = type.GetMethod("FinalizeGame", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FinalizeGame method not found.");
        var finalized = (bool)finalizeMethod.Invoke(service, new object[] { game, new Random(13), settings, false })!;

        Assert.True(finalized);
        Assert.True(InvokeEnsureSolvableCore(game, maxStrategicReveals: 0));
    }

    private static bool InvokeEnsureSolvableCore(MathCrossGame game, int maxStrategicReveals)
    {
        var service = new MathCrossGeneratorService();
        var method = typeof(MathCrossGeneratorService).GetMethod("EnsureSolvableCore", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("EnsureSolvableCore method not found.");

        return (bool)method.Invoke(service, new object[] { game, new Random(7), maxStrategicReveals })!;
    }
}
