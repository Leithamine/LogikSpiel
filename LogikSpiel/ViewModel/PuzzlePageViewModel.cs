using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;

namespace LogikSpiel.ViewModel;

public sealed class PuzzlePageViewModel : ObservableObject
{
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly LockRiddleGeneratorService _riddleGenerator;
    private readonly IRiddleStateStore _riddleState;

    private UserProfile? _userProfile;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public string GameId { get; private set; } = "codebreaker";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set { if (SetProperty(ref _levelNumber, value)) OnPropertyChanged(nameof(Title)); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    private bool _isCelebrating;
    public bool IsCelebrating
    {
        get => _isCelebrating;
        set
        {
            if (SetProperty(ref _isCelebrating, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy && !IsCelebrating;

    private string _rewardText = "";
    public string RewardText
    {
        get => _rewardText;
        set => SetProperty(ref _rewardText, value);
    }

    private string _lockImageSource = "closedlock.png";
    public string LockImageSource
    {
        get => _lockImageSource;
        set => SetProperty(ref _lockImageSource, value);
    }

    private int _genToken = 0;

    public string Title => LocalizationService.Format("Puzzle_TitleFormat", DiffName(DifficultyKey));

    public bool ShowSolutionForDebug => true;
    public string SecretSolution => _secretSolution;

    public ObservableCollection<DigitInputViewModel> InputDigits { get; } = new();
    public ObservableCollection<LockHint> Hints { get; } = new();

    private string _secretSolution = "";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand CheckCommand { get; }
    public AsyncCommand HintCommand { get; }

    public PuzzlePageViewModel(
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        LockRiddleGeneratorService riddleGenerator,
        IRiddleStateStore riddleState)
    {
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _riddleGenerator = riddleGenerator;
        _riddleState = riddleState;

        BackCommand = new AsyncCommand(ConfirmBackAsync);
        CheckCommand = new AsyncCommand(CheckSolutionAsync);

        HintCommand = new AsyncCommand(async () =>
        {
            if (!IsNotBusy) return;

            if (Coins >= 50)
            {
                bool buy = await _dialog.ConfirmAsync(
                    LocalizationService.GetString("Puzzle_BuyHintTitle"),
                    LocalizationService.Format("Puzzle_BuyHintMessage", 50));
                if (buy) await RevealOneDigitAsync();
            }
            else
            {
                await _dialog.AlertAsync(
                    LocalizationService.GetString("Common_NotEnoughCoinsTitle"),
                    LocalizationService.Format("Common_NeedCoinsFormat", 50));
            }
        });
    }

    protected override void OnCultureChanged()
    {
        base.OnCultureChanged();
        RefreshHintDescriptions();
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "codebreaker" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty;
        LevelNumber = Math.Max(1, level);

        OnPropertyChanged(nameof(Title));

        try
        {
            _userProfile = await _userService.GetUserAsync();
            Coins = _userProfile?.Coins ?? 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim User-Laden: {ex.Message}");
        }

        await StartNewRoundAsync();
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

    private async Task StartNewRoundAsync()
    {
        int token = ++_genToken;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsBusy = true;

            IsCelebrating = false;
            RewardText = "";
            LockImageSource = "closedlock.png";

            _secretSolution = "";
            OnPropertyChanged(nameof(SecretSolution));

            Hints.Clear();
            InputDigits.Clear();
        });

        var saved = await _riddleState.TryLoadAsync(GameId, DifficultyKey, LevelNumber);

        LockRiddleGame game;

        // ✅ WICHTIG: gespeicherte Rätsel nur verwenden, wenn sie konsistent + eindeutig sind
        if (saved != null && _riddleGenerator.IsGameValid(saved))
        {
            game = saved;
        }
        else
        {
            if (saved != null)
                await _riddleState.ClearAsync(GameId, DifficultyKey, LevelNumber);

            int seed = SeedHelper.CalculateSeed(GameId, DifficultyKey, LevelNumber);
            game = await Task.Run(() => _riddleGenerator.GenerateGame(DifficultyKey, seed));
            await _riddleState.SaveAsync(GameId, DifficultyKey, LevelNumber, game);
        }

        if (token != _genToken) return;

        _secretSolution = game.SecretCode;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(SecretSolution));

            Hints.Clear();
            foreach (var h in game.Hints.Select(h => NormalizeHint(h, _secretSolution.Length)))
                Hints.Add(LocalizeHint(h));

            InputDigits.Clear();
            for (int i = 0; i < _secretSolution.Length; i++)
                InputDigits.Add(new DigitInputViewModel { Index = i });

            IsBusy = false;
        });

        System.Diagnostics.Debug.WriteLine($"[LockRiddle] diff={DifficultyKey}, level={LevelNumber}, secret={_secretSolution}, hints={game.Hints.Count}");
    }

    private void RefreshHintDescriptions()
    {
        if (Hints.Count == 0) return;
        var updated = Hints.Select(LocalizeHint).ToList();
        Hints.Clear();
        foreach (var hint in updated)
            Hints.Add(hint);
    }

    public void Cleanup()
    {
        _genToken++;
        IsBusy = false;
    }

    private static LockHint LocalizeHint(LockHint hint)
    {
        string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;

        var singular = LocalizationService.GetString("LockRiddle_NumberSingular");
        var plural = LocalizationService.GetString("LockRiddle_NumberPlural");

        string desc = (hint.WellPlaced, hint.WrongPlaced) switch
        {
            (0, 0) => LocalizationService.GetString("LockRiddle_NoDigitCorrect"),
            (1, 0) => LocalizationService.GetString("LockRiddle_OneCorrectWellPlaced"),
            (0, 1) => LocalizationService.GetString("LockRiddle_OneCorrectWrongPlaced"),
            (> 0, 0) => LocalizationService.Format("LockRiddle_HintWellPlacedFormat", hint.WellPlaced, Plural(hint.WellPlaced, singular, plural)),
            (0, > 0) => LocalizationService.Format("LockRiddle_HintWrongPlacedFormat", hint.WrongPlaced, Plural(hint.WrongPlaced, singular, plural)),
            _ => LocalizationService.Format("LockRiddle_HintMixedFormat", hint.WellPlaced + hint.WrongPlaced, Plural(hint.WellPlaced + hint.WrongPlaced, singular, plural), hint.WellPlaced, hint.WrongPlaced)
        };

        return new LockHint
        {
            Slots = hint.Slots.ToList(),
            Code = hint.Code,
            WellPlaced = hint.WellPlaced,
            WrongPlaced = hint.WrongPlaced,
            Icon = hint.Icon,
            Description = desc
        };
    }

    private async Task CheckSolutionAsync()
    {
        if (!IsNotBusy) return;

        if (_secretSolution.Length == 0)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_ErrorTitle"),
                LocalizationService.GetString("Puzzle_NoPuzzleMessage"));
            return;
        }

        if (InputDigits.Any(d => string.IsNullOrWhiteSpace(d.Digit)))
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_IncompleteTitle"),
                LocalizationService.GetString("Puzzle_IncompleteMessage"));
            return;
        }

        string input = string.Concat(InputDigits.Select(d => (d.Digit ?? "").Trim()));

        if (input.Length != _secretSolution.Length)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_IncompleteTitle"),
                LocalizationService.GetString("Puzzle_IncompleteMessage"));
            return;
        }

        if (input.Distinct().Count() != input.Length)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_InvalidTitle"),
                LocalizationService.GetString("Puzzle_InvalidMessage"));
            return;
        }

        if (input != _secretSolution)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_WrongTitle"),
                LocalizationService.Format("Puzzle_WrongMessageFormat", input, _secretSolution));
            return;
        }

        int reward = RewardForDifficulty(DifficultyKey);

        if (_userProfile != null)
        {
            _userProfile.Coins += reward;
            Coins = _userProfile.Coins;
            await _userService.SaveUserAsync(_userProfile);
        }

        int completedLevel = LevelNumber;

        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, completedLevel);
        await _riddleState.ClearAsync(GameId, DifficultyKey, completedLevel);

        await PlaySuccessOverlayAsync(reward);

        LevelNumber = completedLevel + 1;
        await StartNewRoundAsync();
    }

    private async Task PlaySuccessOverlayAsync(int reward)
    {
        int token = ++_genToken;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LockImageSource = "openedlock.png";
            RewardText = reward > 0 ? LocalizationService.Format("Puzzle_RewardFormat", reward) : "";
            IsCelebrating = true;
        });

        await Task.Delay(2600);

        if (token != _genToken) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsCelebrating = false;
            RewardText = "";
            LockImageSource = "closedlock.png";
        });
    }

    private async Task RevealOneDigitAsync()
    {
        for (int i = 0; i < _secretSolution.Length; i++)
        {
            string target = _secretSolution[i].ToString();

            if (InputDigits[i].Digit != target)
            {
                InputDigits[i].Digit = target;
                InputDigits[i].IsLocked = true;

                if (_userProfile != null)
                {
                    _userProfile.Coins -= 50;
                    Coins = _userProfile.Coins;
                    await _userService.SaveUserAsync(_userProfile);
                }
                return;
            }
        }
    }

    private static LockHint NormalizeHint(LockHint hint, int codeLength)
    {
        var slots = hint.Slots ?? new List<string>();
        var normalized = Enumerable.Range(0, codeLength)
            .Select(i => i < slots.Count && !string.IsNullOrWhiteSpace(slots[i]) ? slots[i].Trim() : "")
            .ToList();

        hint.Slots = normalized;
        hint.Code = string.Join(" ", normalized.Select(s => string.IsNullOrEmpty(s) ? "•" : s));
        return hint;
    }

    private static int RewardForDifficulty(string key) =>
        key.ToLowerInvariant() switch
        {
            "easy" => 3,
            "normal" => 5,
            "hard" => 7,
            "master" => 10,
            _ => 5
        };

    private static string DiffName(string key) => LocalizationService.GetDifficultyLabel(key);
}
