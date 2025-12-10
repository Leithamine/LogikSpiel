using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class MainPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;

    public ObservableCollection<GameCardViewModel> Games { get; } = new();

    public AsyncCommand<GameCardViewModel> OpenGameCommand { get; }

    public MainPageViewModel(IGameCatalogService catalog, INavigationService nav)
    {
        _catalog = catalog;
        _nav = nav;

        OpenGameCommand = new AsyncCommand<GameCardViewModel>(async g =>
        {
            if (g is null) return;
            await _nav.GoToAsync("GameLevels", new Dictionary<string, object>
            {
                ["gameId"] = g.Id
            });
        });
    }

    public async Task EnsureLoadedAsync()
    {
        if (Games.Count > 0) return;

        var games = await _catalog.LoadGamesAsync();
        Games.Clear();
        foreach (var g in games)
            Games.Add(new GameCardViewModel(g));
    }
}
