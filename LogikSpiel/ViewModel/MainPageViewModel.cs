using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class MainPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService; 
    public ObservableCollection<GameCardViewModel> Games { get; } = new();

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public AsyncCommand<GameCardViewModel> OpenGameCommand { get; }
    public AsyncCommand OpenProfileCommand { get; }

    public MainPageViewModel(IGameCatalogService catalog, INavigationService nav, IUserProfileService userService)
    {
        _catalog = catalog;
        _nav = nav;
        _userService = userService;

        OpenGameCommand = new AsyncCommand<GameCardViewModel>(async g =>
        {
            if (g is null) return;
            await _nav.GoToAsync("GameMap", new Dictionary<string, object>
            {
                ["gameId"] = g.Id
            });
        });
        OpenProfileCommand = new AsyncCommand(async () => await _nav.GoToAsync("Profile"));
    }

    public async Task EnsureLoadedAsync()
    {
        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;
        if (Games.Count > 0) return;

        var games = await _catalog.LoadGamesAsync();
        Games.Clear();
        foreach (var g in games)
            Games.Add(new GameCardViewModel(g));
    }
}
