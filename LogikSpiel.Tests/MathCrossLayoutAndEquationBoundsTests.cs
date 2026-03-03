using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.ViewModel;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossLayoutAndEquationBoundsTests
{
    [Fact]
    public async Task UpdateLayoutConstraints_DoesNotRegenerateCurrentPuzzle()
    {
        var vm = new MathCrossPageViewModel(
            progressStore: new StubGameProgressStore(),
            dialog: new StubDialogService(),
            nav: new StubNavigationService(),
            userService: new StubUserProfileService(),
            generator: new MathCrossGeneratorService(),
            catalog: new StubGameCatalogService());

        await vm.LoadAsync("math_cross", "easy", 1);

        var beforeGame = vm.Game;
        var beforeCellCount = vm.FlatCells.Count;

        await vm.UpdateLayoutConstraintsAsync(6, 6);

        Assert.Same(beforeGame, vm.Game);
        Assert.Equal(beforeCellCount, vm.FlatCells.Count);
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_WithExtremeLayoutEquationLimits_StaysWithin10To15(string difficulty)
    {
        var generator = new MathCrossGeneratorService();
        var constraints = new MathCrossGeneratorService.LayoutConstraints(16, 16, MinEquations: 1, MaxEquations: 48);

        for (int seed = 1; seed <= 40; seed++)
        {
            var game = generator.GenerateGame(difficulty, seed, constraints);
            Assert.InRange(game.Equations.Count, 10, 15);
        }
    }

    [Theory]
    [InlineData("easy", 9, 9)]
    [InlineData("normal", 9, 9)]
    [InlineData("hard", 9, 9)]
    [InlineData("master", 9, 9)]
    public void GenerateGame_WithViewportConstraints_FillsBoundsAndAvoidsLargeEmptyRegions(string difficulty, int rows, int cols)
    {
        var generator = new MathCrossGeneratorService();
        var constraints = new MathCrossGeneratorService.LayoutConstraints(rows, cols);

        for (int seed = 1; seed <= 25; seed++)
        {
            var game = generator.GenerateGame(difficulty, seed, constraints);

            Assert.True(game.Equations.Any(e => e.IsHorizontal), $"Missing horizontal equations for {difficulty}, seed={seed}.");
            Assert.True(game.Equations.Any(e => !e.IsHorizontal), $"Missing vertical equations for {difficulty}, seed={seed}.");

            int occupied = CountOccupied(game);
            double fillRatio = (double)occupied / Math.Max(1, game.Rows * game.Cols);
            Assert.True(fillRatio >= 0.68,
                $"Grid too sparse for {difficulty}, seed={seed}: fill={fillRatio:0.00}, size={game.Rows}x{game.Cols}.");

            double largestEmptyRegionRatio = LargestEmptyRegionRatio(game);
            Assert.True(largestEmptyRegionRatio <= 0.20,
                $"Large connected empty area for {difficulty}, seed={seed}: emptyRegionRatio={largestEmptyRegionRatio:0.00}.");
        }
    }


    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_CreatesLogicalCrossingsWithLimitedDeadEnds(string difficulty)
    {
        var generator = new MathCrossGeneratorService();
        var constraints = new MathCrossGeneratorService.LayoutConstraints(12, 12);

        for (int seed = 100; seed <= 130; seed++)
        {
            var game = generator.GenerateGame(difficulty, seed, constraints);
            var adjacency = BuildEquationAdjacency(game);

            Assert.All(adjacency, neighbors => Assert.NotEmpty(neighbors));

            int leafCount = adjacency.Count(neighbors => neighbors.Count == 1);
            Assert.True(leafCount <= 4,
                $"Too many dead-end equations for {difficulty}, seed={seed}: leafCount={leafCount}.");

            int intersectionCount = CountIntersections(adjacency);
            Assert.True(intersectionCount >= game.Equations.Count,
                $"Not enough crossings for {difficulty}, seed={seed}: intersections={intersectionCount}, equations={game.Equations.Count}.");
        }
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_HasNoCompletelyEmptyRowsOrColsInsideBounds(string difficulty)
    {
        var generator = new MathCrossGeneratorService();
        var constraints = new MathCrossGeneratorService.LayoutConstraints(12, 12);

        for (int seed = 131; seed <= 165; seed++)
        {
            var game = generator.GenerateGame(difficulty, seed, constraints);

            for (int r = 0; r < game.Rows; r++)
            {
                bool allEmpty = true;
                for (int c = 0; c < game.Cols; c++)
                {
                    if (game.Grid[r, c].Type != CellType.Empty)
                    {
                        allEmpty = false;
                        break;
                    }
                }

                Assert.False(allEmpty, $"Found fully empty row for {difficulty}, seed={seed}, row={r}.");
            }

            for (int c = 0; c < game.Cols; c++)
            {
                bool allEmpty = true;
                for (int r = 0; r < game.Rows; r++)
                {
                    if (game.Grid[r, c].Type != CellType.Empty)
                    {
                        allEmpty = false;
                        break;
                    }
                }

                Assert.False(allEmpty, $"Found fully empty column for {difficulty}, seed={seed}, col={c}.");
            }
        }
    }

    private static int CountOccupied(MathCrossGame game)
    {
        int occupied = 0;
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type != CellType.Empty)
                    occupied++;

        return occupied;
    }

    private static double LargestEmptyRegionRatio(MathCrossGame game)
    {
        if (game.Rows == 0 || game.Cols == 0)
            return 1.0;

        var visited = new bool[game.Rows, game.Cols];
        int largest = 0;

        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (visited[r, c] || game.Grid[r, c].Type != CellType.Empty)
                    continue;

                int size = Flood(game, visited, r, c);
                if (size > largest)
                    largest = size;
            }
        }

        return (double)largest / Math.Max(1, game.Rows * game.Cols);
    }

    private static int Flood(MathCrossGame game, bool[,] visited, int startR, int startC)
    {
        var queue = new Queue<(int r, int c)>();
        queue.Enqueue((startR, startC));
        visited[startR, startC] = true;
        int size = 0;

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            size++;
            TryVisit(r - 1, c);
            TryVisit(r + 1, c);
            TryVisit(r, c - 1);
            TryVisit(r, c + 1);
        }

        return size;

        void TryVisit(int nr, int nc)
        {
            if (nr < 0 || nr >= game.Rows || nc < 0 || nc >= game.Cols)
                return;
            if (visited[nr, nc] || game.Grid[nr, nc].Type != CellType.Empty)
                return;

            visited[nr, nc] = true;
            queue.Enqueue((nr, nc));
        }
    }


    private static List<HashSet<int>> BuildEquationAdjacency(MathCrossGame game)
    {
        var adjacency = Enumerable.Range(0, game.Equations.Count)
            .Select(_ => new HashSet<int>())
            .ToList();

        for (int i = 0; i < game.Equations.Count; i++)
        {
            for (int j = i + 1; j < game.Equations.Count; j++)
            {
                if (!game.Equations[i].Cells.Intersect(game.Equations[j].Cells).Any())
                    continue;

                adjacency[i].Add(j);
                adjacency[j].Add(i);
            }
        }

        return adjacency;
    }

    private static int CountIntersections(IReadOnlyList<HashSet<int>> adjacency)
    {
        int count = 0;
        for (int i = 0; i < adjacency.Count; i++)
            count += adjacency[i].Count(neighbor => neighbor > i);

        return count;
    }

    private sealed class StubGameProgressStore : IGameProgressStore
    {
        public Task<GameProgress> LoadAsync(CancellationToken ct = default) => Task.FromResult(new GameProgress());
        public Task SaveAsync(GameProgress progress, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkLevelCompleteAsync(string gameId, string diff, int level) => Task.CompletedTask;
        public Task ClearAllAsync() => Task.CompletedTask;
        public Task<bool> IsLevelCompleteAsync(string gameId, string diff, int level) => Task.FromResult(false);
        public Task<int> GetCompletedCountAsync(string gameId, string diff) => Task.FromResult(0);
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<string?> PickAsync(string title, string cancel, params string[] options) => Task.FromResult<string?>(null);
        public Task AlertAsync(string title, string message, string? ok = null) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string? accept = null, string? cancel = null) => Task.FromResult(true);
    }

    private sealed class StubNavigationService : INavigationService
    {
        public Task GoBackAsync() => Task.CompletedTask;
        public Task GoToAsync(string route, IDictionary<string, object>? parameters = null) => Task.CompletedTask;
    }

    private sealed class StubUserProfileService : IUserProfileService
    {
        public event Action? UserDataChanged;
        public Task<UserProfile?> GetUserAsync() => Task.FromResult<UserProfile?>(new UserProfile());
        public Task SaveUserAsync(UserProfile user)
        {
            UserDataChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task<bool> HasProfileAsync() => Task.FromResult(true);
    }

    private sealed class StubGameCatalogService : IGameCatalogService
    {
        public Task<IReadOnlyList<GameDefinition>> LoadGamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GameDefinition>>(new List<GameDefinition>());

        public Task<GameDefinition?> GetGameAsync(string gameId, CancellationToken ct = default)
            => Task.FromResult<GameDefinition?>(new GameDefinition { Id = gameId, LevelCount = 100 });

        public Task<IReadOnlyList<LevelSpec>> GetLevelsAsync(string gameId, string difficultyKey, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LevelSpec>>(new List<LevelSpec>());
    }
}
