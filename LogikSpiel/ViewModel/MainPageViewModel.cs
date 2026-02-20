#nullable enable
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.View;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;

namespace LogikSpiel.ViewModel;

public sealed class MainPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IUserProfileService _userService;
    private readonly INavigationService _nav;

    private bool _isLoaded;
    private bool _disposed;

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    public ObservableCollection<GameDefinition> Games { get; } = new();

    public AsyncCommand OpenProfileCommand { get; }
    public AsyncCommand<GameDefinition> OpenGameCommand { get; }

    public MainPageViewModel(
        IGameCatalogService catalog,
        IUserProfileService userService,
        INavigationService nav)
    {
        _catalog = catalog;
        _userService = userService;
        _nav = nav;
        _userService.UserDataChanged += OnUserDataChanged;

        OpenProfileCommand = new AsyncCommand(async () =>
        {
            Debug.WriteLine("[MainPage] OpenProfileCommand executed.");
            await _nav.GoToAsync(nameof(ProfilePage));
        });

        OpenGameCommand = new AsyncCommand<GameDefinition>(async game =>
        {
            Debug.WriteLine($"[MainPage] OpenGameCommand executed. gameId={game?.Id}");
            if (game == null) return;

            // ═══════════════════════════════════════════════════════════
            // WICHTIG: Verwende die Route aus GameDefinition, nicht die ID!
            // Navigiere zur GameMapPage mit der gameId
            // ═══════════════════════════════════════════════════════════
            await _nav.GoToAsync(nameof(GameMapPage), new Dictionary<string, object> { ["gameId"] = game.Id });
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

    private void OnUserDataChanged()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!_disposed)
            {
                await RefreshCoinsAsync();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _userService.UserDataChanged -= OnUserDataChanged;
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
