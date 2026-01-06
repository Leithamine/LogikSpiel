#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model.NumberRain;
using LogikSpiel.Services;
using LogikSpiel.Services.NumberRain;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace LogikSpiel.ViewModel;

public sealed class NumberRainPageViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly IDialogService _dialog;
    private readonly IUserProfileService _userService;
    private readonly IGameProgressStore _progressStore;
    private readonly NumberRainQuestGeneratorService _generator;
    private readonly IDispatcher _dispatcher;

    private NumberRainDifficultySettings? _settings;
    private NumberRainQuest? _quest;
    private IDispatcherTimer? _spawnTimer;
    private IDispatcherTimer? _updateTimer;
    private IDispatcherTimer? _secondTimer;

    private int? _lastSelection;
    private double _elapsedSeconds;
    private int _combo;
    private int _misses;
    private int[] _goalProgress = Array.Empty<int>();

    private bool _ending; // verhindert mehrfachen Dialog / mehrfaches EndRound
    private bool _endRoundScheduled;
    private bool _timedResolutionPending;
    private bool _questPrepared;

    public ObservableCollection<FallingNumberViewModel> ActiveNumbers { get; } = new();

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    private string _questText = LocalizationService.GetString("NumberRain_QuestReady");
    public string QuestText { get => _questText; private set => SetProperty(ref _questText, value); }

    private string _questModeLabel = LocalizationService.GetString("NumberRain_ModeLabel");
    public string QuestModeLabel { get => _questModeLabel; private set => SetProperty(ref _questModeLabel, value); }

    private string _progressText = string.Empty;
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private string _activeRuleText = string.Empty;
    public string ActiveRuleText { get => _activeRuleText; private set => SetProperty(ref _activeRuleText, value); }

    private bool _hasActiveRule;
    public bool HasActiveRule { get => _hasActiveRule; private set => SetProperty(ref _hasActiveRule, value); }

    private int _lives = 3;
    public int Lives
    {
        get => _lives;
        private set
        {
            if (SetProperty(ref _lives, value))
                OnPropertyChanged(nameof(LivesDisplay));
        }
    }

    public string LivesDisplay => new string('❤', Math.Max(0, Lives));

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
                OnPropertyChanged(nameof(CanStart));
        }
    }

    public bool CanStart => !IsRunning;

    private int _timeRemaining;
    public int TimeRemaining
    {
        get => _timeRemaining;
        private set
        {
            if (SetProperty(ref _timeRemaining, value))
                OnPropertyChanged(nameof(ShowTimer));
        }
    }

    public bool ShowTimer => _quest is not null && _quest.TimeLimitSeconds > 0;

    private string _difficultyKey = "normal";
    public string DifficultyKey
    {
        get => _difficultyKey;
        private set
        {
            if (!SetProperty(ref _difficultyKey, value)) return;
            OnPropertyChanged(nameof(DifficultyLabel));
        }
    }

    public string DifficultyLabel => LocalizationService.GetDifficultyLabel(DifficultyKey);

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set => SetProperty(ref _levelNumber, value);
    }

    public string? GameId { get; private set; }

    private double _arenaWidth;
    private double _arenaHeight;

    public AsyncCommand BackCommand { get; }
    public AsyncCommand StartCommand { get; }
    public AsyncCommand CancelCommand { get; }
    public AsyncCommand ExplainCommand { get; }
    public AsyncCommand<FallingNumberViewModel> NumberTapCommand { get; }

    public NumberRainPageViewModel(
        INavigationService nav,
        IDialogService dialog,
        IUserProfileService userService,
        IGameProgressStore progressStore,
        NumberRainQuestGeneratorService generator)
    {
        _nav = nav;
        _dialog = dialog;
        _userService = userService;
        _progressStore = progressStore;
        _generator = generator;

        _dispatcher = Application.Current?.Dispatcher
                      ?? throw new InvalidOperationException("Dispatcher not available");

        BackCommand = new AsyncCommand(ConfirmBackAsync);
        StartCommand = new AsyncCommand(StartAsync);
        CancelCommand = new AsyncCommand(CancelAsync);
        ExplainCommand = new AsyncCommand(ExplainQuestAsync);
        NumberTapCommand = new AsyncCommand<FallingNumberViewModel>(HandleNumberTapAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "number_rain" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty;
        LevelNumber = Math.Max(1, level);

        var profile = await _userService.GetUserAsync();
        Coins = profile?.Coins ?? 0;

        _settings = _generator.GetSettings(DifficultyKey, LevelNumber);
        PrepareQuest();
    }

    public void UpdateArenaSize(double width, double height)
    {
        _arenaWidth = width;
        _arenaHeight = height;
    }

    private Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        _settings = _generator.GetSettings(DifficultyKey, LevelNumber);
        if (!_questPrepared)
            PrepareQuest();
        StartTimers();

        return Task.CompletedTask;
    }

    private Task CancelAsync()
    {
        StopTimers();
        ActiveNumbers.Clear();
        IsRunning = false;
        PrepareQuest();
        return Task.CompletedTask;
    }

    private Task ExplainQuestAsync()
    {
        var message = BuildQuestExplanation();
        return MainThread.InvokeOnMainThreadAsync(async () =>
            await _dialog.AlertAsync(LocalizationService.GetString("NumberRain_ExplainTitle"), message));
    }

    private void PrepareQuest()
    {
        _ending = false;
        _endRoundScheduled = false;
        _timedResolutionPending = false;
        _questPrepared = true;

        _settings ??= _generator.GetSettings(DifficultyKey, LevelNumber);
        int seed = StableHash($"{GameId}:{DifficultyKey}:{LevelNumber}");
        _quest = _generator.GenerateQuest(DifficultyKey, LevelNumber, seed);

        QuestText = _quest.Description;
        QuestModeLabel = QuestModeToLabel(_quest.Mode);

        _goalProgress = _quest.Goals.Select(_ => 0).ToArray();
        _lastSelection = null;
        _combo = 0;
        _misses = 0;
        _elapsedSeconds = 0;

        Lives = 3;
        TimeRemaining = _quest.TimeLimitSeconds;

        ActiveRuleText = string.Empty;
        HasActiveRule = false;

        StatusText = LocalizationService.GetString("NumberRain_StatusReady");
        UpdateProgressText();
        UpdateActiveRule();
    }

    private static string QuestModeToLabel(NumberRainQuestMode mode) => mode switch
    {
        NumberRainQuestMode.Count => LocalizationService.GetString("NumberRain_Mode_Count"),
        NumberRainQuestMode.Timed => LocalizationService.GetString("NumberRain_Mode_Timed"),
        NumberRainQuestMode.Avoid => LocalizationService.GetString("NumberRain_Mode_Avoid"),
        NumberRainQuestMode.Multi => LocalizationService.GetString("NumberRain_Mode_Multi"),
        NumberRainQuestMode.Combo => LocalizationService.GetString("NumberRain_Mode_Combo"),
        NumberRainQuestMode.Survival => LocalizationService.GetString("NumberRain_Mode_Survival"),
        NumberRainQuestMode.Dynamic => LocalizationService.GetString("NumberRain_Mode_Dynamic"),
        NumberRainQuestMode.Switch => LocalizationService.GetString("NumberRain_Mode_Switch"),
        _ => LocalizationService.GetString("NumberRain_ModeLabel")
    };

    private void StartTimers()
    {
        if (_settings is null || _quest is null) return;

        IsRunning = true;
        StatusText = LocalizationService.GetString("NumberRain_StatusRunning");

        StopTimers();

        _spawnTimer = _dispatcher.CreateTimer();
        _spawnTimer.Interval = TimeSpan.FromMilliseconds(_settings.SpawnIntervalMs);
        _spawnTimer.Tick += (_, _) => SpawnNumber();
        _spawnTimer.Start();

        // Sofort-Spawn
        SpawnNumber();

        _updateTimer = _dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(33);
        _updateTimer.Tick += (_, _) => UpdateNumbers(0.033);
        _updateTimer.Start();

        if (_quest.TimeLimitSeconds > 0 || _quest.Mode == NumberRainQuestMode.Switch)
        {
            _secondTimer = _dispatcher.CreateTimer();
            _secondTimer.Interval = TimeSpan.FromSeconds(1);
            _secondTimer.Tick += (_, _) => TickSecond();
            _secondTimer.Start();
        }
    }

    private void StopTimers()
    {
        _spawnTimer?.Stop();
        _updateTimer?.Stop();
        _secondTimer?.Stop();
    }

    private void SpawnNumber()
    {
        // FIX: Kein Spawn, wenn die Arena noch keine Größe hat
        if (!IsRunning || _settings is null || _arenaWidth <= 0 || _arenaHeight <= 0) return;

        int value = PickSpawnValue();

        // Individuelle Geschwindigkeit (70% bis 100% der Basisgeschwindigkeit)
        double speedVariation = 0.7 + (Random.Shared.NextDouble() * 0.3);
        double individualSpeed = _settings.FallSpeed * speedVariation;

        double size = 56;
        double x = Random.Shared.NextDouble() * Math.Max(0, _arenaWidth - size);

        var vm = new FallingNumberViewModel(value, x, -size, size, individualSpeed);
        ActiveNumbers.Add(vm);
    }

    private void UpdateNumbers(double deltaSeconds)
    {
        // FIX: Prüfe _arenaHeight. Wenn 0, bewegen wir nichts und prüfen keine Fehler.
        if (!IsRunning || _settings is null || _quest is null || _arenaHeight <= 0) return;

        var toRemove = new List<FallingNumberViewModel>();

        foreach (var number in ActiveNumbers)
        {
            number.Y += number.Speed * deltaSeconds;

            if (number.Y > _arenaHeight)
            {
                toRemove.Add(number);
                // Nur wenn das Spiel wirklich aktiv läuft und die Arena bereit ist
                if (ShouldCountMissOnFallNumber(number.Value))
                    ApplyMiss();
            }
        }

        foreach (var number in toRemove)
            ActiveNumbers.Remove(number);
    }

    private void TickSecond()
    {
        if (!IsRunning || _quest is null) return;

        _elapsedSeconds += 1;

        if (_quest.TimeLimitSeconds > 0)
            TimeRemaining = Math.Max(0, TimeRemaining - 1);

        UpdateActiveRule();

        if (TimeRemaining <= 0 && _quest.TimeLimitSeconds > 0)
        {
            if (_timedResolutionPending) return;
            _timedResolutionPending = true;
            IsRunning = false;
            _ = ResolveTimedOutcomeAsync();
        }
    }

    private async Task ResolveTimedOutcomeAsync()
    {
        if (_quest is null)
        {
            _timedResolutionPending = false;
            return;
        }

        StopTimers();
        IsRunning = false;

        bool success = _quest.Mode switch
        {
            NumberRainQuestMode.Timed => AreGoalsComplete(),
            NumberRainQuestMode.Survival => _quest.MinHits == 0
                ? _misses <= _quest.MaxMisses
                : TotalHits() >= _quest.MinHits,
            _ => AreGoalsComplete()
        };

        await EndRoundAsync(success);
        _timedResolutionPending = false;
    }

    private async Task HandleNumberTapAsync(FallingNumberViewModel? number)
    {
        if (number is null || _quest is null || !IsRunning) return;
        if (_quest.TimeLimitSeconds > 0 && TimeRemaining <= 0) return;

        ActiveNumbers.Remove(number);

        int? previousLast = _lastSelection;

        bool isAvoid = _quest.AvoidPredicate?.Invoke(number.Value, previousLast) ?? false;
        bool isTarget = IsTarget(number.Value);

        if (isAvoid || !isTarget)
        {
            ApplyMiss();
            UpdateProgressText();
            return;
        }

        _lastSelection = number.Value;

        if (_quest.Mode == NumberRainQuestMode.Combo)
        {
            _combo++;
            if (_combo >= _quest.ComboTarget)
            {
                await EndRoundAsync(true);
                return;
            }
        }
        else
        {
            _combo = 0;
        }

        if (_quest.Mode == NumberRainQuestMode.Multi || _quest.Goals.Count > 1)
        {
            for (int i = 0; i < _quest.Goals.Count; i++)
            {
                var goal = _quest.Goals[i];
                if (_goalProgress[i] >= goal.TargetCount) continue;

                if (goal.Predicate(number.Value, previousLast))
                {
                    _goalProgress[i]++;
                    break;
                }
            }
        }
        else if (_quest.Goals.Count > 0)
        {
            _goalProgress[0]++;
        }

        UpdateProgressText();

        if (AreGoalsComplete())
            await EndRoundAsync(true);
    }

    private bool IsTarget(int value)
    {
        if (_quest is null) return false;

        if (_quest.Mode == NumberRainQuestMode.Switch)
        {
            var rule = CurrentSwitchRule();
            return rule?.Predicate(value, _lastSelection) ?? false;
        }

        var rulePredicate = _quest.Rules.FirstOrDefault()?.Predicate;

        if (_quest.Mode == NumberRainQuestMode.Dynamic && rulePredicate is not null)
            return rulePredicate(value, _lastSelection);

        if (_quest.Goals.Count > 0)
            return _quest.Goals.Any(g => g.Predicate(value, _lastSelection));

        return rulePredicate?.Invoke(value, _lastSelection) ?? false;
    }

    private bool ShouldCountMissOnFall(int value)
    {
        if (_quest is null) return false;

        bool isAvoid = _quest.AvoidPredicate?.Invoke(value, _lastSelection) ?? false;
        if (isAvoid)
            return false;

        return IsTarget(value);
    }

    private void ApplyMiss()
    {
        if (_ending) return;

        _misses++;
        _combo = 0;

        Lives = Math.Max(0, Lives - 1);

        // Survival Miss-Limit
        if (_quest?.Mode == NumberRainQuestMode.Survival && _quest.MaxMisses > 0 && _misses > _quest.MaxMisses)
        {
            ScheduleEndRound(false);
            return;
        }

        // Game Over
        if (Lives <= 0)
            ScheduleEndRound(false);
    }

    private int TotalHits() => _goalProgress.Sum();

    private bool AreGoalsComplete()
    {
        if (_quest is null) return false;

        if (_quest.Mode == NumberRainQuestMode.Combo)
            return _combo >= _quest.ComboTarget;

        for (int i = 0; i < _quest.Goals.Count; i++)
        {
            if (_goalProgress[i] < _quest.Goals[i].TargetCount)
                return false;
        }
        return true;
    }

    private void ScheduleEndRound(bool success)
    {
        if (_ending || _endRoundScheduled) return;
        _endRoundScheduled = true;
        _dispatcher.Dispatch(async () =>
        {
            await EndRoundAsync(success);
            _endRoundScheduled = false;
        });
    }

    private Task EndRoundAsync(bool success)
    {
        if (_ending) return Task.CompletedTask;
        _ending = true;
        return EndRoundInternalAsync(success);
    }

    private async Task EndRoundInternalAsync(bool success)
    {

        StopTimers();
        IsRunning = false;
        ActiveNumbers.Clear();

        if (success)
        {
            int reward = DifficultyKey switch
            {
                "easy" => 5,
                "normal" => 8,
                "hard" => 12,
                "master" => 18,
                _ => 5
            };

            var user = await _userService.GetUserAsync();
            if (user is not null)
            {
                user.Coins += reward;
                Coins = user.Coins;
                await _userService.SaveUserAsync(user);
            }

            if (!string.IsNullOrWhiteSpace(GameId))
                await _progressStore.MarkLevelCompleteAsync(GameId!, DifficultyKey, LevelNumber);

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await _dialog.AlertAsync(
                    LocalizationService.GetString("NumberRain_RewardTitle"),
                    LocalizationService.Format("Common_CoinsRewardFormat", reward)));

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await NavigateToGameMapAsync());
            return;
        }

        // ✅ GameOver-Dialog: Wiederholen / Abbrechen
        bool retry = await MainThread.InvokeOnMainThreadAsync(async () =>
            await _dialog.ConfirmAsync(
                LocalizationService.GetString("NumberRain_GameOverTitle"),
                LocalizationService.GetString("NumberRain_GameOverMessage"),
                LocalizationService.GetString("Common_Retry"),
                LocalizationService.GetString("Common_Back")));

        if (retry)
        {
            PrepareQuest();
            StartTimers(); // sofort neu starten
            return;
        }

        // ✅ Zur Karte zurück
        await MainThread.InvokeOnMainThreadAsync(async () =>
            await NavigateToGameMapAsync());
    }

    private void UpdateProgressText()
    {
        if (_quest is null)
        {
            ProgressText = string.Empty;
            return;
        }

        if (_quest.Mode == NumberRainQuestMode.Combo)
        {
            ProgressText = LocalizationService.Format("NumberRain_ComboProgressFormat", _combo, _quest.ComboTarget);
            return;
        }

        if (_quest.Mode == NumberRainQuestMode.Survival)
        {
            ProgressText = _quest.MinHits > 0
                ? LocalizationService.Format("NumberRain_HitsMissesFormat", TotalHits(), _quest.MinHits, _misses, _quest.MaxMisses)
                : LocalizationService.Format("NumberRain_MissesFormat", _misses, _quest.MaxMisses);
            return;
        }

        if (_quest.Goals.Count == 1)
        {
            ProgressText = $"{_quest.Goals[0].Label}: {_goalProgress[0]}/{_quest.Goals[0].TargetCount}";
            return;
        }

        var parts = new List<string>();
        for (int i = 0; i < _quest.Goals.Count; i++)
        {
            var g = _quest.Goals[i];
            parts.Add($"{g.Label}: {_goalProgress[i]}/{g.TargetCount}");
        }

        ProgressText = string.Join(" • ", parts);
    }

    private void UpdateActiveRule()
    {
        if (_quest is null)
        {
            ActiveRuleText = string.Empty;
            HasActiveRule = false;
            return;
        }

        if (_quest.Mode == NumberRainQuestMode.Switch)
        {
            var rule = CurrentSwitchRule();
            ActiveRuleText = rule is null
                ? string.Empty
                : LocalizationService.Format("NumberRain_ActiveRuleFormat", rule.Label);
            HasActiveRule = !string.IsNullOrWhiteSpace(ActiveRuleText);
            return;
        }

        if (_quest.Mode == NumberRainQuestMode.Dynamic && _quest.Rules.Count > 0)
        {
            ActiveRuleText = LocalizationService.Format("NumberRain_RuleFormat", _quest.Rules[0].Label);
            HasActiveRule = true;
            return;
        }

        ActiveRuleText = string.Empty;
        HasActiveRule = false;
    }

    private string BuildQuestExplanation()
    {
        if (_quest is null)
            return LocalizationService.GetString("NumberRain_NoQuestLoaded");

        var lines = new List<string>
        {
            _quest.Description
        };

        if (_quest.Goals.Count > 0)
        {
            var goals = _quest.Goals.Select(g => $"{g.Label} ({g.TargetCount})");
            lines.Add(LocalizationService.Format("NumberRain_GoalsFormat", string.Join(" / ", goals)));
        }

        if (_quest.Mode == NumberRainQuestMode.Avoid && !string.IsNullOrWhiteSpace(_quest.AvoidLabel))
            lines.Add(LocalizationService.Format("NumberRain_AvoidFormat", _quest.AvoidLabel));

        if (_quest.Rules.Count > 0)
        {
            var rules = string.Join(", ", _quest.Rules.Select(r => r.Label));
            lines.Add(LocalizationService.Format("NumberRain_RulesFormat", rules));
        }

        if (_quest.TimeLimitSeconds > 0)
            lines.Add(LocalizationService.Format("NumberRain_TimeLimitFormat", _quest.TimeLimitSeconds));

        return string.Join(Environment.NewLine, lines);
    }

    private NumberRainRule? CurrentSwitchRule()
    {
        if (_quest is null || _quest.Mode != NumberRainQuestMode.Switch || _quest.Rules.Count < 2)
            return null;

        int interval = Math.Max(1, _quest.SwitchIntervalSeconds);
        int index = ((int)_elapsedSeconds / interval) % _quest.Rules.Count;
        return _quest.Rules[index];
    }

    private async Task ConfirmBackAsync()
    {
        bool leave = await _dialog.ConfirmAsync(
            LocalizationService.GetString("Common_Back"),
            LocalizationService.GetString("NumberRain_LeavePrompt"),
            LocalizationService.GetString("Common_Yes"),
            LocalizationService.GetString("Common_No"));

        if (!leave) return;

        await NavigateToGameMapAsync();
    }

    private Task NavigateToGameMapAsync()
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["gameId"] = GameId ?? "number_rain"
        };

        return _nav.GoToAsync(nameof(GameMapPage), navigationParameters);
    }

    private bool ShouldCountMissOnFallNumber(int value)
    {
        if (_quest is null) return false;

        if (_quest.Mode == NumberRainQuestMode.Avoid)
            return false;

        bool isAvoid = _quest.AvoidPredicate?.Invoke(value, _lastSelection) ?? false;
        if (isAvoid) return false;

        return IsTarget(value);
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (var c in s)
            {
                h ^= c;
                h *= 16777619;
            }
            return Math.Abs(h);
        }
    }

    private int PickSpawnValue()
    {
        if (_settings is null)
            return 0;

        int min = _settings.MinValue;
        int max = _settings.MaxValue;
        if (_quest is null)
            return Random.Shared.Next(min, max + 1);

        Func<int, int?, bool>? predicate = null;

        if (_quest.Mode == NumberRainQuestMode.Switch)
            predicate = CurrentSwitchRule()?.Predicate;
        else if (_quest.Mode == NumberRainQuestMode.Dynamic)
            predicate = _quest.Rules.FirstOrDefault()?.Predicate;
        else if (_quest.Goals.Count > 0)
            predicate = (v, last) => _quest.Goals.Any(g => g.Predicate(v, last));
        else
            predicate = _quest.Rules.FirstOrDefault()?.Predicate;

        if (predicate is null)
            return Random.Shared.Next(min, max + 1);

        var targets = new List<int>();
        for (int v = min; v <= max; v++)
        {
            if (predicate(v, _lastSelection))
                targets.Add(v);
        }

        if (targets.Count == 0)
            return Random.Shared.Next(min, max + 1);

        int rangeCount = max - min + 1;
        double ratio = targets.Count / (double)rangeCount;
        double targetChance = Math.Clamp(0.35 + (0.8 - ratio), 0.35, 0.9);

        if (Random.Shared.NextDouble() < targetChance)
            return targets[Random.Shared.Next(targets.Count)];

        return Random.Shared.Next(min, max + 1);
    }
}
public sealed class FallingNumberViewModel : ObservableObject
{
    private double _x;
    private double _y;
    private double _size;

    public int Value { get; }
    public double Speed { get; }

    public double X
    {
        get => _x;
        set
        {
            if (SetProperty(ref _x, value))
                OnPropertyChanged(nameof(Bounds));
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            if (SetProperty(ref _y, value))
                OnPropertyChanged(nameof(Bounds));
        }
    }

    public double Size
    {
        get => _size;
        set
        {
            if (SetProperty(ref _size, value))
                OnPropertyChanged(nameof(Bounds));
        }
    }

    public Rect Bounds => new(X, Y, Size, Size);

    public FallingNumberViewModel(int value, double x, double y, double size, double speed)
    {
        Value = value;
        _x = x;
        _y = y;
        _size = size;
        Speed = speed;
    }
}
