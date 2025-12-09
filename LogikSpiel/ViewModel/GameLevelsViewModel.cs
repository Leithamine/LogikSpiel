using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class GameLevelsViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;

    public ObservableCollection<LevelItemViewModel> Levels { get; } = new();

    private GameDefinition? _game;
    public GameDefinition? Game
    {
        get => _game;
        private set
        {
            if (!SetProperty(ref _game, value)) return;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => Game?.Title ?? "Level wählen";

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            PlayLevelCommand.RaiseCanExecuteChanged();
            LearnCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncCommand<LevelItemViewModel> PlayLevelCommand { get; }
    public AsyncCommand LearnCommand { get; }
    public AsyncCommand BackCommand { get; }

    private string? _gameId;
    public string? GameId
    {
        get => _gameId;
        set => SetProperty(ref _gameId, value);
    }

    private int _tileSpan = 3;
    public int TileSpan
    {
        get => _tileSpan;
        set => SetProperty(ref _tileSpan, value);
    }

    public GameLevelsViewModel(IGameCatalogService catalog, INavigationService nav)
    {
        _catalog = catalog;
        _nav = nav;

        PlayLevelCommand = new AsyncCommand<LevelItemViewModel>(
            async (lvl) =>
            {
                if (lvl is null) return;
                await _nav.GoToAsync("GameHost", new Dictionary<string, object>
                {
                    ["spec"] = lvl.Spec
                });
            },
            (lvl) => !IsBusy && lvl is not null
        );

        LearnCommand = new AsyncCommand(
            async () =>
            {
                if (string.IsNullOrWhiteSpace(GameId)) return;
                await _nav.GoToAsync("Learn", new Dictionary<string, object>
                {
                    ["gameId"] = GameId!
                });
            },
            () => !IsBusy && !string.IsNullOrWhiteSpace(GameId)
        );

        BackCommand = new AsyncCommand(
            () => _nav.GoBackAsync(),
            () => !IsBusy
        );
    }

    public void UpdateTileSpan(double width)
    {
        TileSpan = width >= 900 ? 6
               : width >= 650 ? 5
               : width >= 420 ? 4
               : 3;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(GameId)) return;

        IsBusy = true;
        try
        {
            Game = await _catalog.GetGameAsync(GameId!);
            var levels = await _catalog.GetLevelsAsync(GameId!);

            Levels.Clear();
            foreach (var s in levels)
                Levels.Add(new LevelItemViewModel(s));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
