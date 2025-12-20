using System.Collections.ObjectModel;
using System.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.ViewModel;

public sealed class GameMapPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;

    public event Action<double>? RequestScrollToY;
    public ObservableCollection<LevelNodeViewModel> Nodes { get; } = new();

    private int _coins;
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
        }
    }

    public string Title => Game?.Title ?? "Karte";

    private bool _isSettingsOpen;
    public bool IsSettingsOpen { get => _isSettingsOpen; set => SetProperty(ref _isSettingsOpen, value); }

    private Rect _cloudBounds;
    public Rect CloudBounds { get => _cloudBounds; set => SetProperty(ref _cloudBounds, value); }

    private string _difficultyKey = "normal";
    public string DifficultyKey
    {
        get => _difficultyKey;
        private set
        {
            if (!SetProperty(ref _difficultyKey, value)) return;
            OnPropertyChanged(nameof(DifficultyLabel));
            OnPropertyChanged(nameof(MapLottieSource));
        }
    }

    public string DifficultyLabel => DifficultyKey switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => DifficultyKey
    };

    private double _mapHeight = 900;
    public double MapHeight { get => _mapHeight; private set => SetProperty(ref _mapHeight, value); }

    public string MapLottieSource => $"map_{DifficultyKey}.json";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand RulesCommand { get; }
    public AsyncCommand ToggleSettingsCommand { get; }
    public AsyncCommand<string> ChangeDifficultyCommand { get; }
    public AsyncCommand<LevelNodeViewModel> OpenLevelCommand { get; }
    public AsyncCommand OpenProfileCommand { get; }

    public GameMapPageViewModel(
        IGameCatalogService catalog,
        IGameProgressStore progressStore,
        INavigationService nav,
        IUserProfileService userService)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _nav = nav;
        _userService = userService;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        OpenProfileCommand = new AsyncCommand(async () =>
            await _nav.GoToAsync("Profile"));

        RulesCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _nav.GoToAsync("Learn", new Dictionary<string, object> { ["gameId"] = GameId! });
        });

        ToggleSettingsCommand = new AsyncCommand(() =>
        {
            IsSettingsOpen = !IsSettingsOpen;
            return Task.CompletedTask;
        });

        ChangeDifficultyCommand = new AsyncCommand<string>(async key =>
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            DifficultyKey = key;
            Preferences.Set($"LAST_DIFF_{GameId}", key);
            IsSettingsOpen = false;
            await BuildMapAsync();
        });

        // ✅ WICHTIG: je nach Spiel zur richtigen Rätsel-Page navigieren
        OpenLevelCommand = new AsyncCommand<LevelNodeViewModel>(async node =>
        {
            if (node is null || !node.IsUnlocked) return;

            string route = node.Spec.GameId switch
            {
                // dein Lock/Code-Spiel
                "codebreaker" => "Puzzle",
                "riddle_lock" => "Puzzle",

                // ✅ neues Spiel
                "pascal_triangle" => "pascaltriangle",

                "math_cross" => "mathcross",

                // Default (bis du weitere Spiele implementierst)
                _ => "Puzzle"
            };

            await _nav.GoToAsync(route, new Dictionary<string, object>
            {
                ["gameId"] = node.Spec.GameId,
                ["difficulty"] = node.Spec.DifficultyKey,
                ["level"] = node.Spec.LevelNumber
            });
        });
    }

    public async Task LoadAsync()
    {
        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;

        if (string.IsNullOrWhiteSpace(GameId)) return;

        await Task.Delay(50);
        Game = await _catalog.GetGameAsync(GameId!);

        DifficultyKey = Preferences.Get($"LAST_DIFF_{GameId}", "normal");

        await BuildMapAsync();
    }

    private static readonly double[] XPattern = { 0.2, 0.5, 0.8, 0.65, 0.35, 0.2, 0.45, 0.75 };

    private async Task BuildMapAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId) || Game is null) return;

        var progress = await _progressStore.LoadAsync();

        var maxLevel = 10000;
        int highestCompleted = 0;

        for (int level = 1; level <= maxLevel; level++)
        {
            if (progress.IsCompleted(GameId!, DifficultyKey, level))
                highestCompleted = level;
            else
                break;
        }

        int currentLevelNumber = highestCompleted + 1;

        int startLevel = 1;
        int endLevel = currentLevelNumber + 50;
        int totalLevels = endLevel - startLevel + 1;

        const double nodeSize = 60;
        const double bottomPad = 120;
        const double stepY = 120;
        const double cloudHeight = 180;
        const double cloudGap = 40;

        double topPad = cloudHeight + cloudGap - stepY;
        if (topPad < 0) topPad = 0;

        double totalHeight = topPad + (totalLevels * stepY) + bottomPad;
        if (totalHeight < 900) totalHeight = 900;

        MapHeight = totalHeight;

        Nodes.Clear();

        for (int i = 0; i < totalLevels; i++)
        {
            int realLevelNum = startLevel + i;
            int baseSeed = StableHash($"{GameId}:{DifficultyKey}") + realLevelNum * 17;

            var spec = new LevelSpec(GameId!, DifficultyKey, realLevelNum, baseSeed);

            var x = XPattern[(realLevelNum - 1) % XPattern.Length];
            var yPx = totalHeight - bottomPad - (i * stepY);
            var yPercent = yPx / totalHeight;

            var node = new LevelNodeViewModel(spec)
            {
                Bounds = new Rect(x, yPercent, nodeSize, nodeSize)
            };

            node.IsCompleted = progress.IsCompleted(spec);

            if (realLevelNum == 1) node.IsUnlocked = true;
            else node.IsUnlocked = progress.IsCompleted(spec.GameId, spec.DifficultyKey, realLevelNum - 1);

            node.IsCurrent = (realLevelNum == currentLevelNumber);

            Nodes.Add(node);
        }

        CloudBounds = new Rect(0, 0, 1, cloudHeight);

        var currentNode = Nodes.FirstOrDefault(n => n.IsCurrent) ?? Nodes.FirstOrDefault();
        if (currentNode is not null)
        {
            double nodeCenterY = (currentNode.Bounds.Y * totalHeight) + (nodeSize / 2);
            await Task.Delay(350);
            RequestScrollToY?.Invoke(nodeCenterY);
        }
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            const int fnvOffset = (int)2166136261;
            const int fnvPrime = 16777619;
            int hash = fnvOffset;

            foreach (var c in s)
            {
                hash ^= c;
                hash *= fnvPrime;
            }

            return Math.Abs(hash);
        }
    }
}
