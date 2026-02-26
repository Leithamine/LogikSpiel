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
    public void FallbackGrid_MultipleSeeds_IsValidTopology(string difficulty)
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var game = InvokeGenerateFallbackGrid(difficulty, seed);
            
            Assert.True(game.Equations.Count >= 8 && game.Equations.Count <= 12,
                $"Expected 8-12 equations in fallback for seed={seed}, difficulty={difficulty}, got {game.Equations.Count}.");
                
            Assert.True(HasValidIntersectionTopology(game), 
                $"Fallback Grid topology is invalid (too few intersections) for seed={seed}, difficulty={difficulty}.");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_IsValidTopology(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        for (int seed = 1; seed <= 40; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);
            
            Assert.True(game.Equations.Count >= 8 && game.Equations.Count <= 12,
                $"Expected 8-12 equations in generated game for seed={seed}, difficulty={difficulty}, got {game.Equations.Count}.");
            
            Assert.True(HasValidIntersectionTopology(game), 
                $"Generated Grid topology is invalid for seed={seed}, difficulty={difficulty}.");
        }
    }

    private static bool HasValidIntersectionTopology(MathCrossGame game)
    {
        int n = game.Equations.Count;
        if (n < 8 || n > 12) return false;

        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        int totalIntersections = 0;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (game.Equations[i].Cells.Intersect(game.Equations[j].Cells).Any())
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                    totalIntersections++;
                }
            }
        }

        // Rule 1: No isolated equations
        for (int i = 0; i < n; i++)
        {
            if (adj[i].Count == 0) return false;
        }

        bool hasRichStructure = false;
        for (int i = 0; i < n; i++)
        {
            if (adj[i].Count >= 3)
            {
                hasRichStructure = true;
                break;
            }
        }

        if (!hasRichStructure)
        {
            if (totalIntersections < n - 1 && adj.Max(x => x.Count) <= 2)
            {
                return false;
            }
        }

        return true;
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
