using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceDeductionTests
{
    [Fact]
    public void EnsureSolvable_DoesNotThrow_ForSimpleEquationShape()
    {
        var service = new MathCrossGeneratorService();
        var game = BuildSingleEquationGame("easy", new[] { "2", "+", "2", "=", "4" }, givenIndices: new[] { 0, 2, 4 });

        var method = typeof(MathCrossGeneratorService).GetMethod("EnsureSolvable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("EnsureSolvable method not found.");

        var ex = Record.Exception(() => method.Invoke(service, new object[] { game, new Random(7) }));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_FinalPuzzleHasHiddenEditableCells(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var game = service.GenerateGame(difficulty, 1337);

        var editable = EnumerateEditable(game).ToList();
        Assert.NotEmpty(editable);
        Assert.Contains(editable, c => !c.IsGiven);
    }

    private static IEnumerable<MathCrossCell> EnumerateEditable(MathCrossGame game)
    {
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type is CellType.Number or CellType.Operator)
                    yield return game.Grid[r, c];
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
                    Cells = Enumerable.Range(0, len).Select(i => (0, i)).ToList()
                }
            ]
        };
    }
}
