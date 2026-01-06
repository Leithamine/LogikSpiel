#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model.NumberRain;
using LogikSpiel.Services;
using LogikSpiel.Services.NumberRain;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;

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

    public ObservableCollection<FallingNumberViewModel> ActiveNumbers { get; } = new();

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    private string _questText = "Bereit? Tippe auf Starten!";
    public string QuestText { get => _questText; private set => SetProperty(ref _questText, value); }

    private string _progressText = string.Empty;
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private string _activeRuleText = string.Empty;
    public string ActiveRuleText { get => _activeRuleText; private set => SetProperty(ref _activeRuleText, value); }

    private bool _hasActiveRule;
    public bool HasActiveRule { get => _hasActiveRule; private set => SetProperty(ref _hasActiveRule, value); }

    private int _lives = 3;
    public int Lives { get => _lives; private set { if (SetProperty(ref _lives, value)) OnPropertyChanged(nameof(LivesDisplay)); } }

    public string LivesDisplay => new string('❤', Math.Max(0, Lives));

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) { OnPropertyChanged(nameof(CanStart)); } } }

    public bool CanStart => !IsRunning;

    private int _timeRemaining;
    public int TimeRemaining { get => _timeRemaining; private set { if (SetProperty(ref _timeRemaining, value)) OnPropertyChanged(nameof(ShowTimer)); } }

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

    public string DifficultyLabel => DifficultyKey switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => DifficultyKey
    };

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set { if (SetProperty(ref _levelNumber, value)) OnPropertyChanged(nameof(Title)); }
    }

    public string Title => $"Zahlenregen · {DifficultyLabel}";

    public string? GameId { get; private set; }

    private double _arenaWidth;
    private double _arenaHeight;

    public AsyncCommand BackCommand { get; }
    public AsyncCommand StartCommand { get; }
    public AsyncCommand CancelCommand { get; }
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
        _dispatcher = Application.Current?.Dispatcher ?? throw new InvalidOperationException("Dispatcher not available");

        BackCommand = new AsyncCommand(ConfirmBackAsync);
        StartCommand = new AsyncCommand(StartAsync);
        CancelCommand = new AsyncCommand(CancelAsync);
        NumberTapCommand = new AsyncCommand<FallingNumberViewModel>(HandleNumberTapAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "number_rain" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty;
        LevelNumber = Math.Max(1, level);

        var profile = await _userService.GetUserAsync();
        Coins = profile?.Coins ?? 0;

        _settings = _generator.GetSettings(DifficultyKey);
        PrepareQuest();
    }

    public void UpdateArenaSize(double width, double height)
    {
        _arenaWidth = width;
        _arenaHeight = height;
    }

    private async Task StartAsync()
    {
        if (IsRunning) return;
        _settings = _generator.GetSettings(DifficultyKey);
        PrepareQuest();
        StartTimers();
        await Task.CompletedTask;
    }

    private Task CancelAsync()
    {
        StopTimers();
        ActiveNumbers.Clear();
        IsRunning = false;
        StatusText = "Gestoppt.";
        return Task.CompletedTask;
    }

    private void PrepareQuest()
    {
        _settings ??= _generator.GetSettings(DifficultyKey);
        int seed = StableHash($"{GameId}:{DifficultyKey}:{LevelNumber}");
        _quest = _generator.GenerateQuest(DifficultyKey, LevelNumber, seed);
        QuestText = _quest.Description;
        _goalProgress = _quest.Goals.Select(_ => 0).ToArray();
        _lastSelection = null;
        _combo = 0;
        _misses = 0;
        _elapsedSeconds = 0;
        Lives = 3;
        TimeRemaining = _quest.TimeLimitSeconds;
        ActiveRuleText = string.Empty;
        HasActiveRule = false;
        StatusText = "Bereit für den Start.";
        UpdateProgressText();
        UpdateActiveRule();
    }

    private void StartTimers()
    {
        if (_settings is null || _quest is null) return;

        IsRunning = true;
        StatusText = "Zahlenregen läuft!";

        _spawnTimer?.Stop();
        _spawnTimer = _dispatcher.CreateTimer();
        _spawnTimer.Interval = TimeSpan.FromMilliseconds(_settings.SpawnIntervalMs);
        _spawnTimer.Tick += (_, _) => SpawnNumber();
        _spawnTimer.Start();

        _updateTimer?.Stop();
        _updateTimer = _dispatcher.CreateTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(33);
        _updateTimer.Tick += (_, _) => UpdateNumbers(0.033);
        _updateTimer.Start();

        if (_quest.TimeLimitSeconds > 0 || _quest.Mode == NumberRainQuestMode.Switch)
        {
            _secondTimer?.Stop();
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
        if (!IsRunning || _settings is null) return;
        if (_arenaWidth <= 0 || _arenaHeight <= 0) return;

        int value = Random.Shared.Next(_settings.MinValue, _settings.MaxValue + 1);
        double size = 56;
        double x = Random.Shared.NextDouble() * Math.Max(0, _arenaWidth - size);
        var vm = new FallingNumberViewModel(value, x, -size, size);
        ActiveNumbers.Add(vm);
    }

    private void UpdateNumbers(double deltaSeconds)
    {
        if (!IsRunning || _settings is null || _quest is null) return;

        double speed = _settings.FallSpeed;
        var toRemove = new List<FallingNumberViewModel>();

        foreach (var number in ActiveNumbers)
        {
            number.Y += speed * deltaSeconds;
            if (number.Y > _arenaHeight)
            {
                toRemove.Add(number);
                if (IsTarget(number.Value))
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
            _ = ResolveTimedOutcomeAsync();
        }
    }

    private async Task ResolveTimedOutcomeAsync()
    {
        if (_quest is null) return;
        StopTimers();
        IsRunning = false;

        bool success = _quest.Mode switch
        {
            NumberRainQuestMode.Timed => AreGoalsComplete(),
            NumberRainQuestMode.Survival => _quest.MinHits == 0 ? _misses <= _quest.MaxMisses : TotalHits() >= _quest.MinHits,
            _ => AreGoalsComplete()
        };

        await EndRoundAsync(success);
    }

    private async Task HandleNumberTapAsync(FallingNumberViewModel? number)
    {
        if (number is null || _quest is null || !IsRunning) return;

        ActiveNumbers.Remove(number);
        int? previousLast = _lastSelection;
        bool isAvoid = _quest.AvoidPredicate?.Invoke(number.Value, previousLast) ?? false;
        bool isTarget = IsTarget(number.Value);

        if (isAvoid)
        {
            ApplyMiss();
            UpdateProgressText();
            return;
        }

        if (!isTarget)
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
        {
            return _quest.Goals.Any(g => g.Predicate(value, _lastSelection));
        }

        return rulePredicate?.Invoke(value, _lastSelection) ?? false;
    }

    private void ApplyMiss()
    {
        _misses++;
        _combo = 0;

        Lives = Math.Max(0, Lives - 1);
        if (_quest?.Mode == NumberRainQuestMode.Survival && _quest.MaxMisses > 0 && _misses > _quest.MaxMisses)
        {
            _ = EndRoundAsync(false);
            return;
        }

        if (Lives <= 0)
        {
            _ = EndRoundAsync(false);
        }
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

    private async Task EndRoundAsync(bool success)
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

            await _dialog.AlertAsync("Super! 🎉", $"+{reward} Coins");
            LevelNumber++;
            PrepareQuest();
        }
        else
        {
            await _dialog.AlertAsync("Oops", "Du hast alle Leben verloren.");
            PrepareQuest();
        }
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
            ProgressText = $"Combo: {_combo}/{_quest.ComboTarget}";
            return;
        }

        if (_quest.Mode == NumberRainQuestMode.Survival)
        {
            if (_quest.MinHits > 0)
                ProgressText = $"Treffer: {TotalHits()}/{_quest.MinHits} • Fehler: {_misses}/{_quest.MaxMisses}";
            else
                ProgressText = $"Fehler: {_misses}/{_quest.MaxMisses}";
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
            ActiveRuleText = rule is null ? string.Empty : $"Aktive Regel: {rule.Label}";
            HasActiveRule = !string.IsNullOrWhiteSpace(ActiveRuleText);
            return;
        }

        if (_quest.Mode == NumberRainQuestMode.Dynamic && _quest.Rules.Count > 0)
        {
            ActiveRuleText = $"Regel: {_quest.Rules[0].Label}";
            HasActiveRule = true;
            return;
        }

        ActiveRuleText = string.Empty;
        HasActiveRule = false;
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
        bool leave = await _dialog.ConfirmAsync("Zurück", "Möchtest du das Spiel verlassen?");
        if (!leave) return;
        await _nav.GoBackAsync();
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
}

public sealed class FallingNumberViewModel : ObservableObject
{
    private double _x;
    private double _y;
    private double _size;

    public int Value { get; }

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

    public FallingNumberViewModel(int value, double x, double y, double size)
    {
        Value = value;
        _x = x;
        _y = y;
        _size = size;
    }
}
