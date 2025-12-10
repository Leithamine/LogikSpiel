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
    public int LevelNumber { get; private set; } = 1;

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title
        => Game is null ? "Rätsel" : $"{Game.Title} – {DiffName(DifficultyKey)} – Level {LevelNumber}";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand SolveCommand { get; }

    public PuzzlePageViewModel(
        IGameCatalogService catalog,
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        // Demo: “Lösen” markiert completed + lädt nächstes Level in derselben Page
        SolveCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;

            var levels = await _catalog.GetLevelsAsync(GameId, DifficultyKey);
            var spec = levels.First(l => l.LevelNumber == LevelNumber);

            var progress = await _progressStore.LoadAsync();
            progress.MarkCompleted(spec);
            await _progressStore.SaveAsync(progress);

            if (LevelNumber < levels.Count)
            {
                LevelNumber++;
                OnPropertyChanged(nameof(LevelNumber));
                OnPropertyChanged(nameof(Title));
                // hier später echte neue Rätsel-Generierung laden
            }
            else
            {
                await _dialog.AlertAsync("Fertig!", "Du hast alle Level dieser Schwierigkeit geschafft!");
            }
        });
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = gameId;
        DifficultyKey = difficulty;
        LevelNumber = Math.Max(1, level);

        Game = await _catalog.GetGameAsync(GameId);
        OnPropertyChanged(nameof(Title));
    }

    private static string DiffName(string key) => key switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "complex" => "Kompliziert",
        "master" => "Master",
        "god" => "Gott",
        _ => key
    };
}
