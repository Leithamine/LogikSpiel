#nullable enable
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class MainPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IUserProfileService _userService;

    private bool _isLoaded;

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    public ObservableCollection<GameDefinition> Games { get; } = new();

    public AsyncCommand OpenProfileCommand { get; }
    public AsyncCommand<GameDefinition> OpenGameCommand { get; }

    public MainPageViewModel(
        IGameCatalogService catalog,
        IUserProfileService userService)
    {
        _catalog = catalog;
        _userService = userService;

        OpenProfileCommand = new AsyncCommand(async () =>
        {
            await Shell.Current.GoToAsync("ProfilePage");
        });

        OpenGameCommand = new AsyncCommand<GameDefinition>(async game =>
        {
            if (game == null) return;

            // ═══════════════════════════════════════════════════════════
            // WICHTIG: Verwende die Route aus GameDefinition, nicht die ID!
            // Navigiere zur GameMapPage mit der gameId
            // ═══════════════════════════════════════════════════════════
            await Shell.Current.GoToAsync($"GameMapPage?gameId={game.Id}");
        });
    }

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        _isLoaded = true;

        await RefreshCoinsAsync();
        await LoadGamesAsync();
    }

    public async Task RefreshCoinsAsync()
    {
        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;
    }

    private async Task LoadGamesAsync()
    {
        Games.Clear();

        var games = await _catalog.LoadGamesAsync();
        foreach (var game in games)
        {
            Games.Add(game);
        }
    }
}
