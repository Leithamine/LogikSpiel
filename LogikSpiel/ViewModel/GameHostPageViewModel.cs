using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Microsoft.Maui.Controls;

namespace LogikSpiel.ViewModel;

public sealed class GameHostPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly INavigationService _nav;

    public string GameId { get; private set; } = "";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber { get => _levelNumber; private set { if (!SetProperty(ref _levelNumber, value)) return; OnPropertyChanged(nameof(Title)); } }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game is null ? "Puzzle" : $"{Game.Title} – {Name(DifficultyKey)} – Level {LevelNumber}";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand RulesCommand { get; }
    public AsyncCommand SolveCommand { get; }

    public GameHostPageViewModel(IGameCatalogService catalog, IGameProgressStore progressStore, INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _nav = nav;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        RulesCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _nav.GoToAsync("Learn", new Dictionary<string, object> { ["gameId"] = GameId });
        });

        SolveCommand = new AsyncCommand(async () =>
        {
            if (Game is null) return;

            var levels = await _catalog.GetLevelsAsync(GameId, DifficultyKey);
            var spec = levels.First(l => l.LevelNumber == LevelNumber);

            var progress = await _progressStore.LoadAsync();
            progress.MarkCompleted(spec);
            await _progressStore.SaveAsync(progress);

            // automatisch nächstes Level
            if (LevelNumber < levels.Count)
                LevelNumber++;
            else
                await Shell.Current.DisplayAlert("Fertig!", "Alle Level dieser Schwierigkeit geschafft!", "OK");
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

    private static string Name(string key) => key switch
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
