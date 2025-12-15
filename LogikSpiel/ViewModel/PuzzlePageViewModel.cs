using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class PuzzlePageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;

    // Coins Property (Wichtig für die Anzeige oben rechts)
    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    // Properties für das Spiel
    public string GameId { get; private set; } = "";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set { if (SetProperty(ref _levelNumber, value)) OnPropertyChanged(nameof(Title)); }
    }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game is null ? "Rätsel" : $"{Game.Title} – {DiffName(DifficultyKey)} – Level {LevelNumber}";

    // Commands
    public AsyncCommand BackCommand { get; }
    public AsyncCommand SolveCommand { get; }
    public AsyncCommand OpenProfileCommand { get; }
    public AsyncCommand HintCommand { get; } // Der neue Hinweis-Button

    public PuzzlePageViewModel(IGameCatalogService catalog, IGameProgressStore progressStore, IDialogService dialog, INavigationService nav, IUserProfileService userService)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        OpenProfileCommand = new AsyncCommand(async () => await _nav.GoToAsync("Profile"));

        // HIER IST DER NEUE HINT COMMAND
        HintCommand = new AsyncCommand(async () =>
        {
            await _dialog.AlertAsync("Hinweis", "Hier würde ein Tipp stehen! (Das könnte später Coins kosten)");
        });

        SolveCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);
            LevelNumber++; // Nächstes Level
        });
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        // Coins laden
        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;

        GameId = gameId;
        DifficultyKey = difficulty;
        LevelNumber = Math.Max(1, level);
        Game = await _catalog.GetGameAsync(GameId);
    }

    private static string DiffName(string key) => key switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "complex" => "Komplex",
        "master" => "Master",
        "god" => "Gott",
        _ => key
    };
}