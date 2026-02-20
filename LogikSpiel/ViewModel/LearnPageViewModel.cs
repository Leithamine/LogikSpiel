using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;

namespace LogikSpiel.ViewModel;

public sealed class LearnPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService; // NEU

    private int _coins; // NEU
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    private string? _gameId;
    public string? GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private GameDefinition? _game;
    public GameDefinition? Game
    {
        get => _game;
        private set
        {
            if (!SetProperty(ref _game, value)) return;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Rules));
        }
    }

    public string Title => Game?.Title ?? LocalizationService.GetString("Learn_TitleDefault");
    public IReadOnlyList<string> Rules => Game?.Rules ?? new List<string>();

    public AsyncCommand BackCommand { get; }
    public AsyncCommand OpenProfileCommand { get; } // NEU

    public LearnPageViewModel(IGameCatalogService catalog, INavigationService nav, IUserProfileService userService)
    {
        _catalog = catalog;
        _nav = nav;
        _userService = userService;
        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
        OpenProfileCommand = new AsyncCommand(async () => await _nav.GoToAsync(nameof(ProfilePage))); // NEU
    }

    public async Task LoadAsync()
    {
        // NEU
        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;

        if (string.IsNullOrWhiteSpace(GameId)) return;
        Game = await _catalog.GetGameAsync(GameId!);
    }
}
