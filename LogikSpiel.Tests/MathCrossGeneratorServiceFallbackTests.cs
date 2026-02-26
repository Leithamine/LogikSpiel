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
    public void FallbackGrid_MultipleSeeds_IsFullyConnected(string difficulty)
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var game = InvokeGenerateFallbackGrid(difficulty, seed);
            
            Assert.True(game.Equations.Count >= 8,
                $"Expected at least 8 equations in fallback for seed={seed}, difficulty={difficulty}, got {game.Equations.Count}.");
                
            Assert.True(IsGameFullyConnected(game), 
                $"Fallback Grid is not fully connected for seed={seed}, difficulty={difficulty}.");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_MultipleSeeds_IsFullyConnected(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        for (int seed = 1; seed <= 40; seed++)
        {
            var game = service.GenerateGame(difficulty, seed);
            Assert.True(IsGameFullyConnected(game), 
                $"Generated Grid is not fully connected for seed={seed}, difficulty={difficulty}.");
        }
    }

    private static bool IsGameFullyConnected(MathCrossGame game)
    {
        int startR = -1, startC = -1;
        int totalCells = 0;

        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (game.Grid[r, c].Type != CellType.Empty)
                {
                    totalCells++;
                    if (startR == -1)
                    {
                        startR = r;
                        startC = c;
                    }
                }
            }
        }

        if (totalCells == 0) return true;

        var visited = new bool[game.Rows, game.Cols];
        var queue = new Queue<(int r, int c)>();
        queue.Enqueue((startR, startC));
        visited[startR, startC] = true;
        int visitedCount = 0;

        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            visitedCount++;

            for (int i = 0; i < 4; i++)
            {
                int nr = r + dr[i];
                int nc = c + dc[i];

                if (nr >= 0 && nr < game.Rows && nc >= 0 && nc < game.Cols)
                {
                    if (!visited[nr, nc] && game.Grid[nr, nc].Type != CellType.Empty)
                    {
                        visited[nr, nc] = true;
                        queue.Enqueue((nr, nc));
                    }
                }
            }
        }

        return visitedCount == totalCells;
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
