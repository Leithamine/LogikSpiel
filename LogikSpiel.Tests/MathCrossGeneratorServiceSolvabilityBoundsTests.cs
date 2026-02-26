using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceSolvabilityBoundsTests
{
    [Fact]
    public void EnsureSolvable_DoesNotThrow_WhenEquationContainsOutOfBoundsCells()
    {
        var service = new MathCrossGeneratorService();

        var game = new MathCrossGame
        {
            Rows = 1,
            Cols = 1,
            Grid = new[,] {
                {
                    new MathCrossCell
                    {
                        Row = 0,
                        Col = 0,
                        Type = CellType.Number,
                        Solution = "1",
                        UserInput = "",
                        IsGiven = false
                    }
                }
            },
            Equations =
            [
                new MathEquation
                {
                    Cells =
                    [
                        (0, 0),
                        (0, 1),
                        (0, 2)
                    ]
                }
            ]
        };

        var method = typeof(MathCrossGeneratorService).GetMethod(
            "EnsureSolvable",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("EnsureSolvable method not found.");

        var ex = Record.Exception(() => method.Invoke(service, new object[] { game, new Random(7) }));
        Assert.Null(ex);
    }
}
