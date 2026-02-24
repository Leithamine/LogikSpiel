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

    // ✅ NEU: Cache für das aktuelle Rätsel, um Retry zu ermöglichen
    private LockRiddleGame? _currentGame;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public string GameId { get; private set; } = "codebreaker";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set
        {
            if (SetProperty(ref _levelNumber, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(LevelDisplayText));
            }
        }
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

    private bool _showProfessor;
    public bool ShowProfessor
    {
        get => _showProfessor;
        set => SetProperty(ref _showProfessor, value);
    }

    private string _professorMessage = "";
    public string ProfessorMessage
    {
        get => _professorMessage;
        set => SetProperty(ref _professorMessage, value);
    }

    public string ProfessorImageSource => "professor.png";

    private int _genToken = 0;

    public string Title => LocalizationService.Format("Puzzle_TitleFormat", DiffName(DifficultyKey));

    public string LevelDisplayText => LocalizationService.Format("Common_LevelFormat", LevelNumber);

    public bool ShowSolutionForDebug => true;
    public string SecretSolution => _secretSolution;

    public ObservableCollection<DigitInputViewModel> InputDigits { get; } = new();
    public ObservableCollection<LockHint> Hints { get; } = new();

    private string _secretSolution = "";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand CheckCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand ShowSolutionCommand { get; }

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

        ShowSolutionCommand = new AsyncCommand(async () =>
        {
            if (!IsNotBusy) return;
            if (_secretSolution.Length == 0) return;

            for (int i = 0; i < _secretSolution.Length; i++)
            {
                if (i < InputDigits.Count)
                {
                    InputDigits[i].Digit = _secretSolution[i].ToString();
                }
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

        int requestedLevel = Math.Max(1, level);
        
        // ✅ Prüfen, ob wir bereits ein Rätsel für dieses Level im Cache haben
        if (_currentGame != null && LevelNumber == requestedLevel && GameId == gameId && DifficultyKey == difficulty)
        {
            // Wir haben bereits ein Rätsel für dieses Level, verwende es
            System.Diagnostics.Debug.WriteLine($"[LoadAsync] Verwende gecachtes Rätsel für Level {requestedLevel}");
            await DisplayGameAsync(_currentGame);
            return;
        }

        int highestCompleted = 0;

        var progress = await _progressStore.LoadAsync();
        for (int candidate = 1; candidate <= GameConfig.MaxLevel; candidate++)
        {
            if (progress.IsCompleted(GameId, DifficultyKey, candidate))
                highestCompleted = candidate;
            else
                break;
        }

        int maxUnlockedLevel = Math.Min(GameConfig.MaxLevel, highestCompleted + 1);
        
        // WICHTIG: Wenn der angeforderte Level bereits freigeschaltet ist, verwenden wir ihn
        if (requestedLevel <= maxUnlockedLevel)
        {
            LevelNumber = requestedLevel;
        }
        else
        {
            LevelNumber = maxUnlockedLevel;
        }

        System.Diagnostics.Debug.WriteLine($"[LoadAsync] Level gesetzt auf: {LevelNumber} (requested: {requestedLevel}, maxUnlocked: {maxUnlockedLevel})");

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

        await NavigateToMapAsync();
    }

    private async Task StartNewRoundAsync()
    {
        int token = ++_genToken;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsBusy = true;
            IsCelebrating = false;
            ShowProfessor = false;
            ProfessorMessage = "";
            RewardText = "";
            LockImageSource = "closedlock.png";
            _secretSolution = "";
            OnPropertyChanged(nameof(SecretSolution));
            Hints.Clear();
            InputDigits.Clear();
        });

        LockRiddleGame? game = null;

        // ✅ 1. Versuche, das gecachte Rätsel zu verwenden (für Retry)
        if (_currentGame != null)
        {
            System.Diagnostics.Debug.WriteLine($"[StartNewRoundAsync] Verwende gecachtes Rätsel");
            game = _currentGame;
        }
        else
        {
            // ✅ 2. Versuche aus dem Store zu laden
            var saved = await _riddleState.TryLoadAsync(GameId, DifficultyKey, LevelNumber);
            
            if (saved != null && _riddleGenerator.IsGameValid(saved))
            {
                System.Diagnostics.Debug.WriteLine($"[StartNewRoundAsync] Rätsel aus Store geladen");
                game = saved;
            }
        }

        // ✅ 3. Wenn kein Rätsel gefunden, generiere ein neues
        if (game == null)
        {
            System.Diagnostics.Debug.WriteLine($"[StartNewRoundAsync] Generiere neues Rätsel für Level {LevelNumber}");
            
            // Lösche evtl. vorhandenes ungültiges Rätsel aus dem Store
            await _riddleState.ClearAsync(GameId, DifficultyKey, LevelNumber);
            
            game = await Task.Run(() => _riddleGenerator.GenerateGame(DifficultyKey, LevelNumber));
            
            // Speichere das neue Rätsel
            await _riddleState.SaveAsync(GameId, DifficultyKey, LevelNumber, game);
        }

        if (token != _genToken) return;

        // ✅ WICHTIG: Speichere das Rätsel im Cache für Retry
        _currentGame = game;
        _secretSolution = game.SecretCode;

        await DisplayGameAsync(game, token);
    }

    private async Task DisplayGameAsync(LockRiddleGame game, int token = 0)
    {
        if (token != 0 && token != _genToken) return;

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

        System.Diagnostics.Debug.WriteLine($"[DisplayGameAsync] Level={LevelNumber}, Secret={_secretSolution}");
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
        // ✅ NICHT _currentGame löschen, damit Retry funktioniert!
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

        // Visuellen Prefix aufbauen (✔️ = gut platziert, 🟡 = falsch platziert)
        string visualPrefix = "";
        for (int i = 0; i < hint.WellPlaced; i++) visualPrefix += "✔️";
        for (int i = 0; i < hint.WrongPlaced; i++) visualPrefix += "🟡";
        if (!string.IsNullOrEmpty(visualPrefix))
            desc = $"{visualPrefix} {desc}";

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
            // Falsche Lösung - Professor anzeigen statt Dialog
            int wrongToken = _genToken;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ShowProfessor = true;
                ProfessorMessage = LocalizationService.GetString("Puzzle_Professor_WrongMessage");
            });

            // Professor nach 3 Sekunden ausblenden
            await Task.Delay(3000);

            if (wrongToken == _genToken)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ShowProfessor = false;
                    ProfessorMessage = "";
                });
            }

            return;
        }

        // ✅ RICHTIGE LÖSUNG
        System.Diagnostics.Debug.WriteLine($"[CheckSolutionAsync] Richtige Lösung für Level {LevelNumber}");
        
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

        // ✅ WICHTIG: Cache löschen, da dieses Rätsel abgeschlossen ist
        _currentGame = null;

        await PlaySuccessOverlayAsync(reward);

        // Nächstes Level laden
        int nextLevel = completedLevel + 1;
        
        if (nextLevel > GameConfig.MaxLevel)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_AllLevelsCompleteTitle"),
                LocalizationService.GetString("Puzzle_AllLevelsCompleteMessage"));
            await NavigateToMapAsync();
            return;
        }

        LevelNumber = nextLevel;
        await StartNewRoundAsync();
    }

    private async Task NavigateToMapAsync()
    {
        var parameters = new Dictionary<string, object>
        {
            ["gameId"] = GameId,
            ["difficulty"] = DifficultyKey,
            ["level"] = LevelNumber
        };

        await _nav.GoToAsync(nameof(GameMapPage), parameters);
    }

    private async Task PlaySuccessOverlayAsync(int reward)
    {
        int token = ++_genToken;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LockImageSource = "openedlock.png";
            RewardText = reward > 0 ? LocalizationService.Format("Puzzle_RewardFormat", reward) : "";
            ProfessorMessage = LocalizationService.GetString("Puzzle_Professor_SuccessMessage");
            ShowProfessor = true;
            IsCelebrating = true;
        });

        await Task.Delay(2600);

        if (token != _genToken) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsCelebrating = false;
            ShowProfessor = false;
            ProfessorMessage = "";
            RewardText = "";
            LockImageSource = "closedlock.png";
        });
    }

    private async Task RevealOneDigitAsync()
    {
        // Prüfen ob überhaupt eine Ziffer noch fehlt/falsch ist
        bool anyWrong = false;
        for (int i = 0; i < _secretSolution.Length; i++)
        {
            if (InputDigits[i].Digit != _secretSolution[i].ToString())
            {
                anyWrong = true;
                break;
            }
        }

        if (!anyWrong)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Puzzle_HintTitle"),
                LocalizationService.GetString("Puzzle_AllDigitsCorrect"));
            return;
        }

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
