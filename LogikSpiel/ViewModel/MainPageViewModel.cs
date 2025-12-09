using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class MainPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;

    public string PageTitle { get; } = "Logik Spiele";

    public ObservableCollection<GameCardItemViewModel> Games { get; } = new();

    private int _tileSpan = 2;
    public int TileSpan { get => _tileSpan; set => SetProperty(ref _tileSpan, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (!SetProperty(ref _isBusy, value)) return; OpenGameCommand.RaiseCanExecuteChanged(); }
    }

    public AsyncCommand<GameCardItemViewModel> OpenGameCommand { get; }

    private bool _initialized;

    public MainPageViewModel(IGameCatalogService catalog, INavigationService nav)
    {
        _catalog = catalog;
        _nav = nav;

        OpenGameCommand = new AsyncCommand<GameCardItemViewModel>(
            async game =>
            {
                if (game is null) return;
                await _nav.GoToAsync("GameLevels", new Dictionary<string, object> { ["gameId"] = game.Id });
            },
            game => !IsBusy && game is not null
        );
    }

    public void UpdateTileSpan(double width)
    {
        TileSpan = width >= 900 ? 4
               : width >= 650 ? 3
               : 2;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        IsBusy = true;
        try
        {
            var games = await _catalog.LoadGamesAsync();
            Games.Clear();
            foreach (var g in games) Games.Add(new GameCardItemViewModel(g));
            _initialized = true;
        }
        finally { IsBusy = false; }
    }
}
