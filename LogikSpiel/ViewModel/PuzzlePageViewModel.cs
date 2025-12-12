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

    public string GameId { get; private set; } = "";
    public string DifficultyKey { get; private set; } = "normal";

    // Setter für LevelNumber, damit UI sich aktualisiert
    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set { if (SetProperty(ref _levelNumber, value)) OnPropertyChanged(nameof(Title)); }
    }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game is null ? "Rätsel" : $"{Game.Title} – {DiffName(DifficultyKey)} – Level {LevelNumber}";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand SolveCommand { get; }

    public PuzzlePageViewModel(IGameCatalogService catalog, IGameProgressStore progressStore, IDialogService dialog, INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        SolveCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;

            // 1. Speichern in DB
            await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);

            // 2. Einfach zum nächsten Level weitergehen (ENDLOS)
            // KEINE Prüfung auf Game.LevelCount mehr!
            LevelNumber++;

            // Optional: Kleines Feedback, dass es geklappt hat?
            // await _dialog.AlertAsync("Gelöst!", "Weiter geht's!"); 
            // Oder einfach stumm das nächste Rätsel laden (besserer Flow)
        });
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
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