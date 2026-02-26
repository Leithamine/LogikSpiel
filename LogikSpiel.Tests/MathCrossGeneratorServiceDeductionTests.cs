using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceDeductionTests
{
    [Fact]
    public void EnsureSolvableCore_DetectsOperatorAmbiguity_WhenNoStrategicRevealAllowed()
    {
        var game = BuildSingleEquationGame("normal", new[] { "2", "+", "2", "=", "4" }, givenIndices: new[] { 0, 2, 4 });

        var solvable = InvokeEnsureSolvableCore(game, maxStrategicReveals: 0);

        Assert.False(solvable);
    }

    [Fact]
    public void EnsureSolvableCore_DetectsNumberAmbiguity_WhenNoStrategicRevealAllowed()
    {
        var game = BuildSingleEquationGame("easy", new[] { "2", "+", "2", "=", "4" }, givenIndices: new[] { 1, 3, 4 });

        var solvable = InvokeEnsureSolvableCore(game, maxStrategicReveals: 0);

        Assert.False(solvable);
    }

    [Fact]
    public void EnsureSolvableCore_SolvesExtendedForcedValue_WithoutReveal()
    {
        var game = BuildSingleEquationGame("hard", new[] { "2", "+", "3", "×", "4", "=", "20" }, givenIndices: new[] { 0, 1, 2, 3, 5, 6 });

        var solvable = InvokeEnsureSolvableCore(game, maxStrategicReveals: 0);

        Assert.True(solvable);
    }

    [Fact]
    public void EnsureSolvableCore_SolvesMasterDecimalCase_WithoutReveal()
    {
        var game = BuildSingleEquationGame("master", new[] { "1", "÷", "2", "+", "0.3", "=", "0.8" }, givenIndices: new[] { 0, 1, 2, 3, 4, 5 });

        var solvable = InvokeEnsureSolvableCore(game, maxStrategicReveals: 0);

        Assert.True(solvable);
    }

    [Fact]
    public void EnsureSolvableCore_UsesStrategicReveal_WhenAllowed()
    {
        var game = BuildSingleEquationGame("easy", new[] { "2", "+", "2", "=", "4" }, givenIndices: new[] { 0, 2, 3, 4 });

        Assert.False(InvokeEnsureSolvableCore(game, maxStrategicReveals: 0));
        Assert.True(InvokeEnsureSolvableCore(game, maxStrategicReveals: 1));
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_FinalPuzzleIsDeductivelySolvable(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var game = service.GenerateGame(difficulty, 1337);

        var solvable = InvokeEnsureSolvableCore(game, maxStrategicReveals: 0);

        Assert.True(solvable);
    }


    [Fact]
    public void FallbackGrid_FinalizedPuzzleIsDeductivelySolvable()
    {
        var service = new MathCrossGeneratorService();
        var type = typeof(MathCrossGeneratorService);

        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");
        var settings = getSettings.Invoke(null, new object[] { "hard" })
            ?? throw new InvalidOperationException("Settings could not be created.");

        var fallbackMethod = type.GetMethod("GenerateFallbackGrid", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenerateFallbackGrid method not found.");
        var game = (MathCrossGame)fallbackMethod.Invoke(service, new object[] { settings, new Random(91) })!;

        var finalizeMethod = type.GetMethod("FinalizeGame", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FinalizeGame method not found.");
        var finalized = (bool)finalizeMethod.Invoke(service, new object[] { game, new Random(92), settings, false })!;

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

    private static MathCrossGame BuildSingleEquationGame(string difficulty, string[] symbols, int[] givenIndices)
    {
        var len = symbols.Length;
        var grid = new MathCrossCell[1, len];

        for (int i = 0; i < len; i++)
        {
            var type = i == len - 2
                ? CellType.Equals
                : (i % 2 == 0 ? CellType.Number : CellType.Operator);

            grid[0, i] = new MathCrossCell
            {
                Row = 0,
                Col = i,
                Type = type,
                Solution = symbols[i],
                IsGiven = givenIndices.Contains(i) || type == CellType.Equals,
                UserInput = givenIndices.Contains(i) || type == CellType.Equals ? symbols[i] : ""
            };
        }

        return new MathCrossGame
        {
            Rows = 1,
            Cols = len,
            Grid = grid,
            Difficulty = difficulty,
            UseExtendedEquations = len > 5,
            EquationLength = len,
            Equations =
            [
                new MathEquation
                {
                    StartRow = 0,
                    StartCol = 0,
                    IsHorizontal = true,
                    Cells = Enumerable.Range(0, len).Select(i => (0, i)).ToList(),
                    Operator = len > 1 ? symbols[1] : "",
                    Operator2 = len > 3 ? symbols[3] : "",
                    Operator3 = len > 5 ? symbols[5] : ""
                }
            ]
        };
    }
}
