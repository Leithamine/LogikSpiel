using System.Globalization;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.ViewModel;

public sealed class PascalTrianglePageViewModel : ObservableObject
{
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly PascalTriangleGeneratorService _generator;

    public event Action? RequestRedraw;

    private UserProfile? _userProfile;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public string GameId { get; private set; } = "pascal_triangle";
    public string DifficultyKey { get; private set; } = "easy";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set
        {
            if (SetProperty(ref _levelNumber, value))
                OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => $"Pascal Pfad – {DiffName(DifficultyKey)} (Lv {LevelNumber})";

    private PascalTriangleGame? _game;
    public PascalTriangleGame? Game { get => _game; private set => SetProperty(ref _game, value); }

    private bool _showSolutionOverlay;
    public bool ShowSolutionOverlay
    {
        get => _showSolutionOverlay;
        set
        {
            if (SetProperty(ref _showSolutionOverlay, value))
                RequestRedraw?.Invoke();
        }
    }

    // ✅ Pro Level genau EIN Ziel
    private string _goal = "max"; // "max" | "min"
    public string Goal
    {
        get => _goal;
        private set
        {
            if (SetProperty(ref _goal, value))
            {
                OnPropertyChanged(nameof(GoalChipText));
                OnPropertyChanged(nameof(GoalChipColor));
                RequestRedraw?.Invoke();
            }
        }
    }

    public string GoalChipText => Goal == "max" ? "AUFGABE: MAX" : "AUFGABE: MIN";
    public Color GoalChipColor => Goal == "max"
        ? Color.FromArgb("#2ECC71")
        : Color.FromArgb("#E74C3C");

    public HashSet<(int row, int col)> GoalPathSet =>
        Goal == "max"
            ? (Game?.MaxPath?.ToHashSet() ?? new())
            : (Game?.MinPath?.ToHashSet() ?? new());

    private decimal _userSum;
    public decimal UserSum { get => _userSum; set => SetProperty(ref _userSum, value); }

    private readonly List<(int row, int col)> _selectedPath = new();
    public IReadOnlyList<(int row, int col)> SelectedPath => _selectedPath;

    public AsyncCommand BackCommand { get; }
    public AsyncCommand ToggleSolutionCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand CheckCommand { get; }

    public PascalTrianglePageViewModel(
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        PascalTriangleGeneratorService generator)
    {
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _generator = generator;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        ToggleSolutionCommand = new AsyncCommand(() =>
        {
            ShowSolutionOverlay = !ShowSolutionOverlay;
            return Task.CompletedTask;
        });

        ResetCommand = new AsyncCommand(() =>
        {
            ClearSelection();
            ShowSolutionOverlay = false;
            RequestRedraw?.Invoke();
            return Task.CompletedTask;
        });

        HintCommand = new AsyncCommand(async () =>
        {
            if (Coins < 15)
            {
                await _dialog.AlertAsync("Nicht genug Coins", "Du brauchst 15 Coins!");
                return;
            }

            bool buy = await _dialog.ConfirmAsync("Tipp kaufen?", "Ersten echten Schritt anzeigen für 15 Coins?");
            if (!buy) return;

            RevealFirstMove();

            if (_userProfile != null)
            {
                _userProfile.Coins -= 15;
                Coins = _userProfile.Coins;
                await _userService.SaveUserAsync(_userProfile);
            }
        });

        CheckCommand = new AsyncCommand(CheckAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "pascal_triangle" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "easy" : difficulty;
        LevelNumber = Math.Max(1, level);

        OnPropertyChanged(nameof(Title));

        _userProfile = await _userService.GetUserAsync();
        Coins = _userProfile?.Coins ?? 0;

        await StartNewRoundAsync();
    }

    private async Task StartNewRoundAsync()
    {
        ClearSelection();
        ShowSolutionOverlay = false;

        // deterministischer seed pro Level
        int seed = StableHash($"{GameId}:{DifficultyKey}") + LevelNumber * 17;

        // ✅ Ziel pro Level: max oder min (deterministisch)
        Goal = (seed % 2 == 0) ? "max" : "min";

        Game = await Task.Run(() => _generator.GenerateGame(DifficultyKey, seed));

        RequestRedraw?.Invoke();
    }

    // start anywhere, extend from ends
    public void TapCell(int row, int col)
    {
        if (Game == null) return;

        var key = (row, col);

        if (_selectedPath.Count == 0)
        {
            _selectedPath.Add(key);
            UpdateUserSum();
            RequestRedraw?.Invoke();
            return;
        }

        int idx = _selectedPath.IndexOf(key);
        if (idx >= 0)
        {
            int removeFromStart = idx;
            int removeFromEnd = (_selectedPath.Count - 1) - idx;

            if (removeFromStart <= removeFromEnd)
                _selectedPath.RemoveRange(0, idx);
            else
                _selectedPath.RemoveRange(idx + 1, _selectedPath.Count - (idx + 1));

            UpdateUserSum();
            RequestRedraw?.Invoke();
            return;
        }

        var head = _selectedPath[0];
        var tail = _selectedPath[^1];

        if (IsParentOf(head, key))
        {
            _selectedPath.Insert(0, key);
            UpdateUserSum();
            RequestRedraw?.Invoke();
            return;
        }

        if (IsChildOf(tail, key))
        {
            _selectedPath.Add(key);
            UpdateUserSum();
            RequestRedraw?.Invoke();
            return;
        }
    }

    private static bool IsParentOf((int row, int col) child, (int row, int col) parent)
    {
        if (parent.row != child.row - 1) return false;
        return parent.col == child.col || parent.col == child.col - 1;
    }

    private static bool IsChildOf((int row, int col) parent, (int row, int col) child)
    {
        if (child.row != parent.row + 1) return false;
        return child.col == parent.col || child.col == parent.col + 1;
    }

    private void UpdateUserSum()
    {
        if (Game == null) { UserSum = 0; return; }

        decimal sum = 0;
        foreach (var (r, c) in _selectedPath)
            sum += Game.Triangle[r][c];

        UserSum = sum;
    }

    private void ClearSelection()
    {
        _selectedPath.Clear();
        UserSum = 0;
    }

    private void RevealFirstMove()
    {
        if (Game == null) return;

        var path = Goal == "max" ? Game.MaxPath : Game.MinPath;
        if (path.Count < 2) return;

        ClearSelection();
        _selectedPath.Add(path[0]);
        _selectedPath.Add(path[1]);
        UpdateUserSum();

        RequestRedraw?.Invoke();
    }

    private async Task CheckAsync()
    {
        if (Game == null) return;

        int lastRow = Game.Rows - 1;

        if (_selectedPath.Count != Game.Rows || _selectedPath[0].row != 0 || _selectedPath[^1].row != lastRow)
        {
            await _dialog.AlertAsync(
                "Pfad unvollständig",
                "Dein Pfad muss bis ganz nach OBEN (Zeile 0) und ganz nach UNTEN (letzte Zeile) gehen."
            );
            return;
        }

        decimal target = Goal == "max" ? Game.MaxPathSum : Game.MinPathSum;
        bool ok = UserSum == target;

        ShowSolutionOverlay = true;

        var de = CultureInfo.GetCultureInfo("de-DE");

        if (!ok)
        {
            await _dialog.AlertAsync("Nicht optimal ❌",
                $"Deine Summe: {UserSum.ToString("0.##", de)}\n" +
                $"Beste Summe: {target.ToString("0.##", de)}");
            return;
        }

        int reward = RewardForDifficulty(DifficultyKey);

        if (_userProfile != null)
        {
            _userProfile.Coins += reward;
            Coins = _userProfile.Coins;
            await _userService.SaveUserAsync(_userProfile);
        }

        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);

        await Task.Delay(900);

        LevelNumber++;
        await StartNewRoundAsync();
    }

    private static int RewardForDifficulty(string key) =>
        key.ToLowerInvariant() switch
        {
            "easy" => 8,
            "normal" => 10,
            "hard" => 15,
            "master" => 20,
            _ => 10
        };

    private static string DiffName(string key) => key.ToLowerInvariant() switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => key
    };

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
