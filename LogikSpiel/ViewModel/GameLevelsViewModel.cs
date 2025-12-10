using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class GameLevelsViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly INavigationService _nav;

    public ObservableCollection<LevelNodeViewModel> LevelNodes { get; } = new();

    private string? _gameId;
    public string? GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game?.Title ?? "Level wählen";

    private string _selectedDifficultyKey = "normal";
    public string SelectedDifficultyKey
    {
        get => _selectedDifficultyKey;
        set
        {
            if (!SetProperty(ref _selectedDifficultyKey, value)) return;
            OnPropertyChanged(nameof(MapLottieSource));
        }
    }

    public string MapLottieSource => SelectedDifficultyKey switch
    {
        "easy" => "map_easy.json",
        "normal" => "map_normal.json",
        "hard" => "map_hard.json",
        "complex" => "map_complex.json",
        "master" => "map_master.json",
        "god" => "map_god.json",
        _ => "map_normal.json"
    };

    private bool _isSettingsOpen;
    public bool IsSettingsOpen { get => _isSettingsOpen; set => SetProperty(ref _isSettingsOpen, value); }

    private double _mapHeight = 1200;
    public double MapHeight { get => _mapHeight; private set => SetProperty(ref _mapHeight, value); }

    public AsyncCommand BackCommand { get; }
    public AsyncCommand LearnCommand { get; }
    public AsyncCommand OpenSettingsCommand { get; }
    public AsyncCommand CloseSettingsCommand { get; }
    public AsyncCommand<string> SetDifficultyCommand { get; }
    public AsyncCommand<LevelNodeViewModel> SelectLevelCommand { get; }

    public GameLevelsViewModel(IGameCatalogService catalog, IGameProgressStore progressStore, INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _nav = nav;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        LearnCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _nav.GoToAsync("Learn", new Dictionary<string, object> { ["gameId"] = GameId! });
        });

        OpenSettingsCommand = new AsyncCommand(() =>
        {
            IsSettingsOpen = true;
            return Task.CompletedTask;
        });

        CloseSettingsCommand = new AsyncCommand(() =>
        {
            IsSettingsOpen = false;
            return Task.CompletedTask;
        });

        SetDifficultyCommand = new AsyncCommand<string>(async key =>
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            SelectedDifficultyKey = key;
            Preferences.Set($"LAST_DIFF_{GameId}", key);
            IsSettingsOpen = false;
            await ReloadLevelsAsync();
        });

        SelectLevelCommand = new AsyncCommand<LevelNodeViewModel>(async node =>
        {
            if (node is null || !node.IsUnlocked) return;

            await _nav.GoToAsync("GameHost", new Dictionary<string, object>
            {
                ["gameId"] = node.Spec.GameId,
                ["difficulty"] = node.Spec.DifficultyKey,
                ["level"] = node.Spec.LevelNumber
            });
        });
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;

        Game = await _catalog.GetGameAsync(GameId!);
        SelectedDifficultyKey = Preferences.Get($"LAST_DIFF_{GameId}", "normal");
        await ReloadLevelsAsync();
    }

    public async Task ReloadLevelsAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;

        var progress = await _progressStore.LoadAsync();
        var specs = await _catalog.GetLevelsAsync(GameId!, SelectedDifficultyKey);

        LevelNodes.Clear();

        for (int i = 0; i < specs.Count; i++)
        {
            var s = specs[i];
            var node = new LevelNodeViewModel(s);

            node.IsCompleted = progress.IsCompleted(s);

            if (s.LevelNumber == 1) node.IsUnlocked = true;
            else node.IsUnlocked = progress.IsCompleted(s.GameId, s.DifficultyKey, s.LevelNumber - 1);

            LevelNodes.Add(node);
        }

        // Current: erstes unlocked & nicht abgeschlossen
        var current = LevelNodes.FirstOrDefault(n => n.IsUnlocked && !n.IsCompleted) ?? LevelNodes.FirstOrDefault();
        foreach (var n in LevelNodes) n.IsCurrent = false;
        if (current is not null) current.IsCurrent = true;

        ApplyMapLayout(LevelNodes);
    }

    // Candy-Crush Zick-Zack-Pattern (0..1 Positionen)
    private static readonly double[] XPattern = { 0.18, 0.50, 0.82, 0.65, 0.35, 0.20, 0.45, 0.75 };

    private void ApplyMapLayout(IList<LevelNodeViewModel> nodes)
    {
        const double nodeSize = 60;
        const double topPad = 120;
        const double stepY = 120;

        MapHeight = topPad + (nodes.Count - 1) * stepY + 260;

        for (int i = 0; i < nodes.Count; i++)
        {
            var x = XPattern[i % XPattern.Length];
            var yPx = topPad + i * stepY;
            var y = yPx / MapHeight;

            nodes[i].Bounds = new Microsoft.Maui.Graphics.Rect(x, y, nodeSize, nodeSize);
        }
    }
}
