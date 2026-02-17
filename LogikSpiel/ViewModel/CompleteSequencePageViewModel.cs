#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.ViewModel;

public sealed class CompleteSequencePageViewModel : ObservableObject
{
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly CompleteSequenceGeneratorService _generator;

    private CompleteSequencePuzzle? _puzzle;
    private UserProfile? _userProfile;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public string GameId { get; private set; } = "complete_sequence";
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

    public string Title => LocalizationService.Format("Sequence_TitleFormat", DiffName(DifficultyKey), LevelNumber);

    private int _lives = 3;
    public int Lives
    {
        get => _lives;
        private set
        {
            if (SetProperty(ref _lives, value))
                OnPropertyChanged(nameof(LivesText));
        }
    }

    public string LivesText => LocalizationService.Format("Sequence_LivesFormat", Lives);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }
    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<SequenceCell> SequenceCells { get; } = new();
    public ObservableCollection<int> Options { get; } = new();

    public AsyncCommand BackCommand { get; }
    public AsyncCommand<int> SelectOptionCommand { get; }
    public AsyncCommand HintCommand { get; }

    public CompleteSequencePageViewModel(
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        CompleteSequenceGeneratorService generator)
    {
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _generator = generator;

        BackCommand = new AsyncCommand(ConfirmBackAsync);
        SelectOptionCommand = new AsyncCommand<int>(SelectOptionAsync);
        HintCommand = new AsyncCommand(ShowHintAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "complete_sequence" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "easy" : difficulty;
        LevelNumber = Math.Max(1, level);
        OnPropertyChanged(nameof(Title));

        _userProfile = await _userService.GetUserAsync();
        Coins = _userProfile?.Coins ?? 0;

        await StartNewRoundAsync();
    }

    private Task StartNewRoundAsync()
    {
        Lives = 3;

        int seed = StableHash($"{GameId}:{DifficultyKey}") + (LevelNumber * 17);
        _puzzle = _generator.Generate(DifficultyKey, seed);

        SequenceCells.Clear();
        Options.Clear();

        for (int i = 0; i < _puzzle.Sequence.Count; i++)
        {
            bool missing = i == _puzzle.MissingIndex;
            SequenceCells.Add(new SequenceCell
            {
                Text = missing ? "?" : _puzzle.Sequence[i].ToString(),
                IsMissing = missing
            });
        }

        foreach (var option in _puzzle.Options)
            Options.Add(option);

        return Task.CompletedTask;
    }

    private async Task SelectOptionAsync(int option)
    {
        if (IsBusy || _puzzle == null) return;
        IsBusy = true;

        try
        {
            if (option == _puzzle.CorrectAnswer)
            {
                int reward = RewardForDifficulty(DifficultyKey);

                if (_userProfile != null)
                {
                    _userProfile.Coins += reward;
                    Coins = _userProfile.Coins;
                    await _userService.SaveUserAsync(_userProfile);
                }

                int completedLevel = LevelNumber;
                await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, completedLevel);

                await _dialog.AlertAsync(
                    LocalizationService.GetString("Sequence_CorrectTitle"),
                    LocalizationService.Format("Sequence_CorrectMessageFormat", _puzzle.CorrectAnswer, reward));

                int nextLevel = completedLevel + 1;
                int maxLevel = GameConfig.MaxLevel;
                if (nextLevel > maxLevel)
                {
                    await _dialog.AlertAsync(
                        LocalizationService.GetString("Sequence_CompleteTitle"),
                        LocalizationService.GetString("Sequence_CompleteMessage"),
                        LocalizationService.GetString("Common_Ok"));
                    await _nav.GoBackAsync();
                    return;
                }

                LevelNumber = nextLevel;
                await StartNewRoundAsync();
            }
            else
            {
                Lives = Math.Max(0, Lives - 1);

                if (Lives > 0)
                {
                    await _dialog.AlertAsync(
                        LocalizationService.GetString("Sequence_WrongTitle"),
                        LocalizationService.Format("Sequence_WrongMessageFormat", Lives));
                }
                else
                {
                    bool retry = await _dialog.ConfirmAsync(
                        LocalizationService.GetString("Sequence_GameOverTitle"),
                        LocalizationService.GetString("Sequence_GameOverMessage"),
                        LocalizationService.GetString("Common_Retry"),
                        LocalizationService.GetString("Common_Back"));

                    if (retry)
                    {
                        await StartNewRoundAsync();
                    }
                    else
                    {
                        await _nav.GoBackAsync();
                    }
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowHintAsync()
    {
        if (_puzzle == null || IsBusy) return;

        string hint = BuildHintMessage(_puzzle.Sequence);
        await _dialog.AlertAsync(
            LocalizationService.GetString("Sequence_HintTitle"),
            hint,
            LocalizationService.GetString("Common_Ok"));
    }

    private static int RewardForDifficulty(string difficultyKey) => difficultyKey switch
    {
        "easy" => 3,
        "normal" => 5,
        "hard" => 7,
        "master" => 10,
        _ => 10
    };

    private static string DiffName(string key) => LocalizationService.GetDifficultyLabel(key);

    private static string BuildHintMessage(IReadOnlyList<int> sequence)
    {
        if (sequence.Count < 3)
            return LocalizationService.GetString("Sequence_Hint_Fallback");

        if (IsArithmetic(sequence))
            return LocalizationService.GetString("Sequence_Hint_Arithmetic");

        if (IsGeometric(sequence))
            return LocalizationService.GetString("Sequence_Hint_Geometric");

        if (IsSquareSequence(sequence))
            return LocalizationService.GetString("Sequence_Hint_Squares");

        if (IsTriangularSequence(sequence))
            return LocalizationService.GetString("Sequence_Hint_Triangular");

        if (IsPentagonalSequence(sequence))
            return LocalizationService.GetString("Sequence_Hint_Pentagonal");

        if (IsAlternatingDifference(sequence))
            return LocalizationService.GetString("Sequence_Hint_Alternating");

        if (HasRepeatingDifferences(sequence, 3))
            return LocalizationService.GetString("Sequence_Hint_RepeatingDiff");

        return LocalizationService.GetString("Sequence_Hint_Fallback");
    }

    private async Task ConfirmBackAsync()
    {
        bool leave = await _dialog.ConfirmAsync(
            LocalizationService.GetString("Common_Back"),
            LocalizationService.GetString("Common_LeavePuzzlePrompt"),
            LocalizationService.GetString("Common_Yes"),
            LocalizationService.GetString("Common_No"));
        if (!leave) return;
        await _nav.GoBackAsync();
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
            return (int)Math.Abs((long)hash);
        }
    }

    private static bool IsArithmetic(IReadOnlyList<int> sequence)
    {
        int diff = sequence[1] - sequence[0];
        for (int i = 2; i < sequence.Count; i++)
        {
            if (sequence[i] - sequence[i - 1] != diff)
                return false;
        }
        return true;
    }

    private static bool IsGeometric(IReadOnlyList<int> sequence)
    {
        if (sequence[0] == 0) return false;
        if (sequence[1] % sequence[0] != 0) return false;

        int ratio = sequence[1] / sequence[0];
        if (ratio <= 1) return false;

        for (int i = 2; i < sequence.Count; i++)
        {
            if (sequence[i - 1] * ratio != sequence[i])
                return false;
        }
        return true;
    }

    private static bool IsSquareSequence(IReadOnlyList<int> sequence)
    {
        if (!TrySquareIndex(sequence[0], out int start)) return false;
        for (int i = 1; i < sequence.Count; i++)
        {
            if (!TrySquareIndex(sequence[i], out int n) || n != start + i)
                return false;
        }
        return true;
    }

    private static bool IsTriangularSequence(IReadOnlyList<int> sequence)
    {
        if (!TryTriangularIndex(sequence[0], out int start)) return false;
        for (int i = 1; i < sequence.Count; i++)
        {
            if (!TryTriangularIndex(sequence[i], out int n) || n != start + i)
                return false;
        }
        return true;
    }

    private static bool IsPentagonalSequence(IReadOnlyList<int> sequence)
    {
        if (!TryPentagonalIndex(sequence[0], out int start)) return false;
        for (int i = 1; i < sequence.Count; i++)
        {
            if (!TryPentagonalIndex(sequence[i], out int n) || n != start + i)
                return false;
        }
        return true;
    }

    private static bool IsAlternatingDifference(IReadOnlyList<int> sequence)
    {
        if (sequence.Count < 4) return false;
        int diffA = sequence[1] - sequence[0];
        int diffB = sequence[2] - sequence[1];
        if (diffA == diffB) return false;

        for (int i = 2; i < sequence.Count; i++)
        {
            int diff = sequence[i] - sequence[i - 1];
            int expected = ((i - 1) % 2 == 0) ? diffA : diffB;
            if (diff != expected)
                return false;
        }
        return true;
    }

    private static bool HasRepeatingDifferences(IReadOnlyList<int> sequence, int period)
    {
        if (sequence.Count < period + 2) return false;

        var diffs = new int[sequence.Count - 1];
        for (int i = 1; i < sequence.Count; i++)
            diffs[i - 1] = sequence[i] - sequence[i - 1];

        for (int i = period; i < diffs.Length; i++)
        {
            if (diffs[i] != diffs[i % period])
                return false;
        }

        return diffs.Distinct().Count() > 1;
    }

    private static bool TrySquareIndex(int value, out int n)
    {
        int root = (int)Math.Sqrt(value);
        if (root * root == value)
        {
            n = root;
            return true;
        }
        n = 0;
        return false;
    }

    private static bool TryTriangularIndex(int value, out int n)
    {
        long test = 8L * value + 1;
        long root = (long)Math.Sqrt(test);
        if (root * root == test && (root - 1) % 2 == 0)
        {
            n = (int)((root - 1) / 2);
            return n > 0;
        }
        n = 0;
        return false;
    }

    private static bool TryPentagonalIndex(int value, out int n)
    {
        long test = 24L * value + 1;
        long root = (long)Math.Sqrt(test);
        if (root * root == test && (1 + root) % 6 == 0)
        {
            n = (int)((1 + root) / 6);
            return n > 0;
        }
        n = 0;
        return false;
    }
}
