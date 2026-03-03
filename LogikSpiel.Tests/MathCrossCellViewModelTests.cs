using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.ViewModel;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossCellViewModelTests
{
    [Fact]
    public void EditableText_WhenCellBecomesGiven_ShowsSolution()
    {
        var parent = BuildVm();
        var cell = new MathCrossCell { Type = CellType.Number, Solution = "42", UserInput = "", IsGiven = false };
        var vm = new MathCrossCellViewModel(cell, parent);

        cell.IsGiven = true;
        vm.UpdateDisplay();

        Assert.Equal("42", vm.EditableText);
    }

    [Fact]
    public async Task Hint_FillsOnlySelectedEditableNumberCell()
    {
        var userService = new StubUserProfileService { User = new UserProfile { Coins = 50 } };
        var parent = new MathCrossPageViewModel(new StubGameProgressStore(), new StubDialogService(), new StubNavigationService(), userService, new MathCrossGeneratorService(), new StubGameCatalogService());
        await parent.LoadAsync("math_cross", "easy", 1);

        var target = parent.FlatCells.First(c => c.IsEditable);
        parent.OnCellTapped(target);
        parent.HintCommand.Execute(null);
        await Task.Delay(50);

        Assert.True(target.Cell.IsGiven);
        Assert.Equal(target.Cell.Solution, target.Cell.UserInput);
        Assert.Equal(40, parent.Coins);
    }

    private static MathCrossPageViewModel BuildVm() => new(new StubGameProgressStore(), new StubDialogService(), new StubNavigationService(), new StubUserProfileService(), new MathCrossGeneratorService(), new StubGameCatalogService());

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
        public UserProfile User { get; set; } = new();
        public Task<UserProfile?> GetUserAsync() => Task.FromResult<UserProfile?>(User);
        public Task SaveUserAsync(UserProfile user) { User = user; UserDataChanged?.Invoke(); return Task.CompletedTask; }
        public Task<bool> HasProfileAsync() => Task.FromResult(true);
    }

    private sealed class StubGameCatalogService : IGameCatalogService
    {
        public Task<IReadOnlyList<GameDefinition>> LoadGamesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<GameDefinition>>(new List<GameDefinition>());
        public Task<GameDefinition?> GetGameAsync(string gameId, CancellationToken ct = default) => Task.FromResult<GameDefinition?>(new GameDefinition { Id = gameId, LevelCount = 100 });
        public Task<IReadOnlyList<LevelSpec>> GetLevelsAsync(string gameId, string difficultyKey, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LevelSpec>>(new List<LevelSpec>());
    }
}
