#nullable enable
using System.Collections.Generic;
using System.Globalization;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;
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

    public string Title => LocalizationService.Format("Pascal_TitleFormat", DiffName(DifficultyKey), LevelNumber);

    private PascalTriangleGame? _game;
    public PascalTriangleGame? Game
    {
        get => _game;
        private set => SetProperty(ref _game, value);
    }

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

    private string _goal = "max";
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

    public string GoalChipText => Goal == "max"
        ? LocalizationService.GetString("Pascal_GoalMax")
        : LocalizationService.GetString("Pascal_GoalMin");

    public Color GoalChipColor => Goal == "max"
        ? Color.FromArgb("#2ECC71")
        : Color.FromArgb("#E74C3C");

    public HashSet<(int row, int col)> GoalPathSet =>
        Goal == "max"
            ? (Game?.MaxPath?.ToHashSet() ?? new())
            : (Game?.MinPath?.ToHashSet() ?? new());

    private decimal _userSum;
    public decimal UserSum
    {
        get => _userSum;
        set => SetProperty(ref _userSum, value);
    }

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

        BackCommand = new AsyncCommand(ConfirmBackAsync);

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
                await _dialog.AlertAsync(
                    LocalizationService.GetString("Common_NotEnoughCoinsTitle"),
                    LocalizationService.Format("Pascal_NotEnoughCoinsMessage", 15));
                return;
            }

            bool buy = await _dialog.ConfirmAsync(
                LocalizationService.GetString("Pascal_BuyHintTitle"),
                LocalizationService.Format("Pascal_BuyHintMessage", 15));

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

        int baseSeed = StableHash($"{GameId}:{DifficultyKey}") + LevelNumber * 17;

        Goal = (baseSeed % 2 == 0) ? "max" : "min";

        // Zielbereich für Reihenanzahl je Schwierigkeit
        var (minRows, maxRows) = TargetRowRange(DifficultyKey);

        PascalTriangleGame? game = null;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int seed = baseSeed + attempt * 13;
            game = await Task.Run(() => _generator.GenerateGame(DifficultyKey, seed, maxRows));

            if (game != null && game.Rows >= minRows && game.Rows <= maxRows)
                break;
        }

        Game = game;
        RequestRedraw?.Invoke();
    }

    private static (int minRows, int maxRows) TargetRowRange(string difficultyKey)
        => difficultyKey.ToLowerInvariant() switch
        {
            "easy" => (5, 7),
            "normal" => (6, 8),
            "hard" => (8, 10),
            "master" => (10, 13),
            _ => (5, 8)
        };

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

        _selectedPath.Clear();
        _selectedPath.Add(key);
        UpdateUserSum();
        RequestRedraw?.Invoke();
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
        if (Game == null)
        {
            UserSum = 0;
            return;
        }

        decimal sum = 0;
        foreach (var (r, c) in _selectedPath)
        {
            if (r >= 0 && r < Game.Triangle.Count && c >= 0 && c < Game.Triangle[r].Count)
                sum += Game.Triangle[r][c];
        }

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

        if (_selectedPath.Count != Game.Rows)
        {
            await _dialog.AlertAsync(
                "Pfad unvollständig",
                $"Dein Pfad hat {_selectedPath.Count} Zellen, aber das Dreieck hat {Game.Rows} Zeilen.\n" +
                "Der Pfad muss von der Spitze bis zur Basis gehen.");
            return;
        }

        if (_selectedPath[0].row != 0)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Pascal_PathIncompleteTitle"),
                LocalizationService.GetString("Pascal_PathStartMessage"));
            return;
        }

        if (_selectedPath[^1].row != lastRow)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Pascal_PathIncompleteTitle"),
                LocalizationService.Format("Pascal_PathEndMessageFormat", lastRow));
            return;
        }

        decimal target = Goal == "max" ? Game.MaxPathSum : Game.MinPathSum;
        bool isOptimal = UserSum == target;

        ShowSolutionOverlay = true;

        var culture = CultureInfo.CurrentCulture;

        if (!isOptimal)
        {
            string goalText = Goal == "max"
                ? LocalizationService.GetString("Pascal_OptimalWordMax")
                : LocalizationService.GetString("Pascal_OptimalWordMin");
            await _dialog.AlertAsync(
                LocalizationService.GetString("Pascal_NotOptimalTitle"),
                LocalizationService.Format(
                    "Pascal_NotOptimalMessageFormat",
                    UserSum.ToString("0.##", culture),
                    goalText,
                    target.ToString("0.##", culture)));
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

        await _dialog.AlertAsync(
            "Perfekt! 🎉",
            $"Du hast den optimalen Pfad gefunden!\n+{reward} Coins");

        await Task.Delay(500);

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

    private static string DiffName(string key) => LocalizationService.GetDifficultyLabel(key);

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

    private async Task ConfirmBackAsync()
    {
        bool leave = await _dialog.ConfirmAsync(
            LocalizationService.GetString("Common_Back"),
            LocalizationService.GetString("Common_LeavePuzzlePrompt"),
            LocalizationService.GetString("Common_Yes"),
            LocalizationService.GetString("Common_No"));

        if (!leave) return;

        var parameters = new Dictionary<string, object>
        {
            ["gameId"] = GameId
        };

        await _nav.GoToAsync(nameof(GameMapPage), parameters);
    }
}
