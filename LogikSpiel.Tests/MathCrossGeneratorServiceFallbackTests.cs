using System.Reflection;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceFallbackTests
{
    [Theory]
    [InlineData("easy")]
    [InlineData("hard")]
    public void FallbackGrid_MultipleSeeds_HasExpectedCrossIntersections(string difficulty)
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var game = InvokeGenerateFallbackGrid(difficulty, seed);
            int eqLen = game.EquationLength;

            var second = 1 + eqLen + 1;
            var intersections = new (int r, int c)[]
            {
                (1, 1),
                (1, second),
                (second, 1),
                (second, second)
            };

            foreach (var (r, c) in intersections)
            {
                Assert.True(r >= 0 && r < game.Rows && c >= 0 && c < game.Cols,
                    $"Intersection ({r},{c}) out of bounds for seed={seed}, difficulty={difficulty}.");
                Assert.Equal(CellType.Number, game.Grid[r, c].Type);
            }

            Assert.True(game.Equations.Count >= 8,
                $"Expected at least 8 equations in fallback for seed={seed}, difficulty={difficulty}, got {game.Equations.Count}.");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_DoesNotStartFullySolved(string difficulty)
    {
        var service = new MathCrossGeneratorService();

        for (int seed = 1; seed <= 80; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);

            var editable = EnumerateEditable(game).ToList();
            Assert.NotEmpty(editable);
            Assert.Contains(editable, c => !c.IsGiven);
        }
    }

    private static MathCrossGame InvokeGenerateFallbackGrid(string difficulty, int seed)
    {
        var service = new MathCrossGeneratorService();
        var type = typeof(MathCrossGeneratorService);

        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");
        var settings = getSettings.Invoke(null, new object[] { difficulty })
            ?? throw new InvalidOperationException("Settings could not be created.");

        var method = type.GetMethod("GenerateFallbackGrid", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenerateFallbackGrid method not found.");

        return (MathCrossGame)method.Invoke(service, new object[] { settings, new Random(seed) })!;
    }

    private static IEnumerable<MathCrossCell> EnumerateEditable(MathCrossGame game)
    {
        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                var cell = game.Grid[r, c];
                if (cell.Type is CellType.Number or CellType.Operator)
                    yield return cell;
            }
        }
    }
}
