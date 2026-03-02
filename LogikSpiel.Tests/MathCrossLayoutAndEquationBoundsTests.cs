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
