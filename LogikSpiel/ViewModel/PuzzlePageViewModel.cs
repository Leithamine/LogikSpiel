using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

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

    public string GameId { get; private set; } = "riddle_lock";
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
    public bool IsNotBusy => !IsBusy;

    private int _genToken = 0;

    public string Title => $"Code Knacker – {DiffName(DifficultyKey)}";

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

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
        CheckCommand = new AsyncCommand(CheckSolutionAsync);

        HintCommand = new AsyncCommand(async () =>
        {
            if (Coins >= 10)
            {
                bool buy = await _dialog.ConfirmAsync("Tipp kaufen?", "Eine Zahl aufdecken für 10 Coins?");
                if (buy) RevealOneDigit();
            }
            else
            {
                await _dialog.AlertAsync("Nicht genug Coins", "Du brauchst 10 Coins!");
            }
        });
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "riddle_lock" : gameId;
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

    private async Task StartNewRoundAsync()
    {
        int token = ++_genToken;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsBusy = true;

            _secretSolution = "";
            OnPropertyChanged(nameof(SecretSolution));

            Hints.Clear();
            InputDigits.Clear();
        });

        // 1) erst Cache laden
        var saved = await _riddleState.TryLoadAsync(GameId, DifficultyKey, LevelNumber);

        LockRiddleGame game;
        if (saved != null)
        {
            game = saved;
        }
        else
        {
            game = await Task.Run(() => _riddleGenerator.GenerateGame(DifficultyKey));
            await _riddleState.SaveAsync(GameId, DifficultyKey, LevelNumber, game);
        }

        if (token != _genToken) return;

        _secretSolution = game.SecretCode;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(SecretSolution));

            Hints.Clear();
            foreach (var h in game.Hints)
                Hints.Add(h);

            InputDigits.Clear();
            for (int i = 0; i < _secretSolution.Length; i++)
                InputDigits.Add(new DigitInputViewModel { Index = i });

            IsBusy = false;
        });

        System.Diagnostics.Debug.WriteLine($"[LockRiddle] diff={DifficultyKey}, level={LevelNumber}, secret={_secretSolution}, hints={game.Hints.Count}");
    }

    private async Task CheckSolutionAsync()
    {
        if (_secretSolution.Length == 0)
        {
            await _dialog.AlertAsync("Fehler", "Kein Rätsel geladen.");
            return;
        }

        if (InputDigits.Any(d => string.IsNullOrWhiteSpace(d.Digit)))
        {
            await _dialog.AlertAsync("Unvollständig", "Bitte fülle alle Felder aus.");
            return;
        }

        string input = string.Concat(InputDigits.Select(d => (d.Digit ?? "").Trim()));

        if (input.Length != _secretSolution.Length)
        {
            await _dialog.AlertAsync("Unvollständig", "Bitte fülle alle Felder aus.");
            return;
        }

        if (input.Distinct().Count() != input.Length)
        {
            await _dialog.AlertAsync("Ungültig", "Jede Zahl darf nur einmal vorkommen.");
            return;
        }

        if (input != _secretSolution)
        {
            await _dialog.AlertAsync("Falsch ❌",
                $"Code stimmt nicht.\n\nDein Code: {input}\nLösung (Debug): {_secretSolution}");
            return;
        }

        // ✅ korrekt
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

        // ✅ Success-Page öffnen (mit Parametern)
        var nextLevel = completedLevel + 1;

        await Shell.Current.GoToAsync(nameof(LogikSpiel.View.UnlockSuccessPage), new Dictionary<string, object>
        {
            ["gameId"] = GameId,
            ["difficulty"] = DifficultyKey,
            ["completedLevel"] = completedLevel,
            ["nextLevel"] = nextLevel,
            ["reward"] = reward
        });
    }

    private void RevealOneDigit()
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
                    _userProfile.Coins -= 10;
                    Coins = _userProfile.Coins;
                    _ = _userService.SaveUserAsync(_userProfile);
                }
                return;
            }
        }
    }

    private static int RewardForDifficulty(string key) =>
        key.ToLowerInvariant() switch
        {
            "easy" => 6,
            "normal" => 8,
            "hard" => 10,
            "master" => 12,
            _ => 8
        };

    private static string DiffName(string key) => key.ToLowerInvariant() switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => key
    };
}
