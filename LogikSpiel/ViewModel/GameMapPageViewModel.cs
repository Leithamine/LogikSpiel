using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class GameMapPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    public ObservableCollection<LevelNodeViewModel> Nodes { get; } = new();

    private string? _gameId;
    public string? GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game?.Title ?? "Karte";

    private string _difficultyKey = "normal";
    public string DifficultyKey
    {
        get => _difficultyKey;
        private set { if (!SetProperty(ref _difficultyKey, value)) return; OnPropertyChanged(nameof(DifficultyLabel)); }
    }

    public string DifficultyLabel => DifficultyKey switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "complex" => "Kompliziert",
        "master" => "Master",
        "god" => "Gott",
        _ => DifficultyKey
    };

    public AsyncCommand BackCommand { get; }
    public AsyncCommand RulesCommand { get; }
    public AsyncCommand SettingsCommand { get; }
    public AsyncCommand<LevelNodeViewModel> OpenLevelCommand { get; }

    public GameMapPageViewModel(
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

        RulesCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _nav.GoToAsync("Learn", new Dictionary<string, object> { ["gameId"] = GameId! });
        });

        SettingsCommand = new AsyncCommand(async () =>
        {
            var pick = await _dialog.PickAsync(
                "Schwierigkeit",
                "Abbrechen",
                "Einfach", "Normal", "Schwer", "Kompliziert", "Master", "Gott");

            if (pick is null) return;

            var key = pick switch
            {
                "Einfach" => "easy",
                "Normal" => "normal",
                "Schwer" => "hard",
                "Kompliziert" => "complex",
                "Master" => "master",
                "Gott" => "god",
                _ => "normal"
            };

            DifficultyKey = key;
            SaveLastDifficulty();
            await BuildMapAsync();
        });

        OpenLevelCommand = new AsyncCommand<LevelNodeViewModel>(async node =>
        {
            if (node is null) return;
            if (!node.IsUnlocked) return;

            await _nav.GoToAsync("Puzzle", new Dictionary<string, object>
            {
                ["gameId"] = node.Spec.GameId,
                ["difficulty"] = node.Spec.DifficultyKey,
                ["level"] = node.Spec.LevelNumber
            });
        });
    }

    public double MapHeight { get; private set; } = 900;
    public string MapLottieSource => DifficultyKey switch
    {
        "easy" => "map_easy.json",
        "normal" => "map_normal.json",
        "hard" => "map_hard.json",
        "complex" => "map_complex.json",
        "master" => "map_master.json",
        "god" => "map_god.json",
        _ => "map_normal.json"
    };

    private static readonly double[] XPattern =
    {
    0.18, 0.50, 0.82, 0.65, 0.35, 0.20, 0.45, 0.75
};

    private void ApplyMapLayout(IReadOnlyList<LevelNodeViewModel> nodes)
    {
        const double nodeSize = 60;
        const double topPad = 80;
        const double stepY = 120;

        MapHeight = topPad + (nodes.Count - 1) * stepY + 200;
        OnPropertyChanged(nameof(MapHeight));
        OnPropertyChanged(nameof(MapLottieSource));

        for (int i = 0; i < nodes.Count; i++)
        {
            var x = XPattern[i % XPattern.Length];           // 0..1
            var yPx = topPad + i * stepY;
            var y = yPx / MapHeight;                         // 0..1

            nodes[i].Bounds = new Rect(x, y, nodeSize, nodeSize); // PositionProportional
        }
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;

        Game = await _catalog.GetGameAsync(GameId!);
        LoadLastDifficulty();
        await BuildMapAsync();
    }

    public async Task RefreshAsync() => await BuildMapAsync();

    private void LoadLastDifficulty()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;
        DifficultyKey = Preferences.Get($"LAST_DIFF_{GameId}", "normal");
    }

    private void SaveLastDifficulty()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;
        Preferences.Set($"LAST_DIFF_{GameId}", DifficultyKey);
    }

    private static double WaveOffset(int index)
    {
        // Candy-Crush-artige Welle (tweakbar)
        // 0, 18, 36, 18, 0, -18, -36, -18, ...
        int m = index % 8;
        return m switch
        {
            0 => 0,
            1 => 18,
            2 => 36,
            3 => 18,
            4 => 0,
            5 => -18,
            6 => -36,
            7 => -18,
            _ => 0
        };
    }

    private async Task BuildMapAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId) || Game is null) return;

        var progress = await _progressStore.LoadAsync();
        var levels = await _catalog.GetLevelsAsync(GameId!, DifficultyKey);

        Nodes.Clear();

        // Build nodes
        for (int i = 0; i < levels.Count; i++)
        {
            var spec = levels[i];
            var node = new LevelNodeViewModel(
                spec,
                showConnector: i < levels.Count - 1,
                yOffset: WaveOffset(i));

            node.IsCompleted = progress.IsCompleted(spec);

            if (spec.LevelNumber == 1) node.IsUnlocked = true;
            else node.IsUnlocked = progress.IsCompleted(spec.GameId, spec.DifficultyKey, spec.LevelNumber - 1);

            Nodes.Add(node);
        }

        // Current marker = erstes unlocked & nicht completed
        var current = Nodes.FirstOrDefault(n => n.IsUnlocked && !n.IsCompleted) ?? Nodes.FirstOrDefault();
        foreach (var n in Nodes) n.IsCurrent = false;
        if (current is not null) current.IsCurrent = true;
    }
}
