#nullable enable
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Services;
using Microsoft.Maui.Controls;

namespace LogikSpiel.ViewModel;

public sealed class NumberRainPageViewModel : ObservableObject
{
    private const int LivesMax = 3;
    private readonly IUserProfileService _userService;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    private readonly Dictionary<string, NumberRainSpawn> _activeSpawns = new();
    private readonly ObservableCollection<NumberRainObjectiveProgress> _objectives = new();
    private IDispatcherTimer? _spawnTimer;
    private IDispatcherTimer? _countdownTimer;
    private IDispatcherTimer? _switchTimer;

    private NumberRainTask? _task;
    private DifficultySettings _settings;
    private Random _random = new();

    private int? _lastSelectedValue;
    private int _hits;

    public event Action<NumberRainSpawn>? Spawned;
    public event Action? ClearRequested;

    public string GameId { get; private set; } = "number_rain";
    public string DifficultyKey { get; private set; } = "easy";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set => SetProperty(ref _levelNumber, value);
    }

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    private int _lives = LivesMax;
    public int Lives
    {
        get => _lives;
        private set
        {
            if (SetProperty(ref _lives, value))
                OnPropertyChanged(nameof(LivesText));
        }
    }

    public string LivesText => new string('❤', Math.Max(0, Lives));

    private string _taskTitle = "";
    public string TaskTitle { get => _taskTitle; private set => SetProperty(ref _taskTitle, value); }

    private string _taskDetail = "";
    public string TaskDetail { get => _taskDetail; private set => SetProperty(ref _taskDetail, value); }

    private int _timeRemaining;
    public int TimeRemaining
    {
        get => _timeRemaining;
        private set
        {
            if (SetProperty(ref _timeRemaining, value))
                OnPropertyChanged(nameof(TimerText));
        }
    }

    public string TimerText => TimeRemaining > 0 ? $"⏱ {TimeRemaining}s" : "";

    private int _comboCurrent;
    public int ComboCurrent
    {
        get => _comboCurrent;
        private set
        {
            if (SetProperty(ref _comboCurrent, value))
                OnPropertyChanged(nameof(ComboText));
        }
    }

    public string ComboText => _task?.ComboTarget > 0 ? $"Combo {ComboCurrent}/{_task.ComboTarget}" : "";

    public ObservableCollection<NumberRainObjectiveProgress> Objectives => _objectives;
    public bool HasObjectives => Objectives.Count > 0;

    public string DifficultyLabel => DifficultyKey switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => DifficultyKey
    };

    public bool IsTimed => _task?.TimeLimitSeconds > 0 || _task?.SurvivalSeconds > 0;
    public bool HasCombo => _task?.ComboTarget > 0;

    public AsyncCommand BackCommand { get; }

    public NumberRainPageViewModel(
        IUserProfileService userService,
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav)
    {
        _userService = userService;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;

        BackCommand = new AsyncCommand(ConfirmBackAsync);
    }

    public async Task LoadAsync(string gameId, string difficultyKey, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "number_rain" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficultyKey) ? "easy" : difficultyKey;
        LevelNumber = Math.Max(1, level);

        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;

        await StartLevelAsync();
    }

    public void Stop()
    {
        StopTimers();
        _activeSpawns.Clear();
        ClearRequested?.Invoke();
    }

    private async Task StartLevelAsync()
    {
        StopTimers();
        _activeSpawns.Clear();
        _objectives.Clear();
        ClearRequested?.Invoke();

        Lives = LivesMax;
        ComboCurrent = 0;
        _hits = 0;
        _lastSelectedValue = null;

        _settings = DifficultySettings.For(DifficultyKey);
        _random = new Random(StableHash($"{GameId}:{DifficultyKey}:{LevelNumber}"));

        BuildTask();
        StartTimers();
    }

    private void BuildTask()
    {
        _task = NumberRainTaskFactory.Create(_random, _settings, DifficultyKey);
        TaskTitle = _task.Title;
        TaskDetail = _task.Detail;

        _objectives.Clear();
        foreach (var obj in _task.Objectives)
            _objectives.Add(new NumberRainObjectiveProgress(obj.Title, obj.Target, obj.Predicate));

        OnPropertyChanged(nameof(HasObjectives));
        OnPropertyChanged(nameof(IsTimed));
        OnPropertyChanged(nameof(HasCombo));
        OnPropertyChanged(nameof(ComboText));
    }

    private void StartTimers()
    {
        StopTimers();

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        _spawnTimer = dispatcher.CreateTimer();
        _spawnTimer.Interval = TimeSpan.FromMilliseconds(_settings.SpawnIntervalMs);
        _spawnTimer.Tick += OnSpawnTick;
        _spawnTimer.Start();

        if (_task?.TimeLimitSeconds > 0)
        {
            TimeRemaining = _task.TimeLimitSeconds;
            _countdownTimer = dispatcher.CreateTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += OnCountdownTick;
            _countdownTimer.Start();
        }
        else if (_task?.SurvivalSeconds > 0)
        {
            TimeRemaining = _task.SurvivalSeconds;
            _countdownTimer = dispatcher.CreateTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += OnSurvivalTick;
            _countdownTimer.Start();
        }

        if (_task?.SwitchRule is not null)
        {
            _switchTimer = dispatcher.CreateTimer();
            _switchTimer.Interval = TimeSpan.FromSeconds(_task.SwitchRule.SwitchSeconds);
            _switchTimer.Tick += OnSwitchTick;
            _switchTimer.Start();
        }
    }

    private void StopTimers()
    {
        if (_spawnTimer is not null)
        {
            _spawnTimer.Stop();
            _spawnTimer.Tick -= OnSpawnTick;
            _spawnTimer = null;
        }

        if (_countdownTimer is not null)
        {
            _countdownTimer.Stop();
            _countdownTimer.Tick -= OnCountdownTick;
            _countdownTimer.Tick -= OnSurvivalTick;
            _countdownTimer = null;
        }

        if (_switchTimer is not null)
        {
            _switchTimer.Stop();
            _switchTimer.Tick -= OnSwitchTick;
            _switchTimer = null;
        }
    }

    private void OnSpawnTick(object? sender, EventArgs e) => SpawnNumber();

    private void OnCountdownTick(object? sender, EventArgs e) => TickCountdown();

    private void OnSurvivalTick(object? sender, EventArgs e) => TickSurvival();

    private void OnSwitchTick(object? sender, EventArgs e) => ToggleSwitchRule();

    private void TickCountdown()
    {
        if (_task is null || _task.TimeLimitSeconds <= 0) return;

        TimeRemaining = Math.Max(0, TimeRemaining - 1);
        if (TimeRemaining > 0) return;

        _countdownTimer?.Stop();
        _countdownTimer = null;

        _ = HandleTimeExpiredAsync();
    }

    private void TickSurvival()
    {
        if (_task is null || _task.SurvivalSeconds <= 0) return;

        TimeRemaining = Math.Max(0, TimeRemaining - 1);
        if (TimeRemaining > 0) return;

        _countdownTimer?.Stop();
        _countdownTimer = null;

        _ = HandleSurvivalCompleteAsync();
    }

    private async Task HandleTimeExpiredAsync()
    {
        if (IsCompleted())
        {
            await CompleteLevelAsync();
            return;
        }

        await ApplyMistakeAsync("⏱ Zeit abgelaufen!");
        if (Lives > 0)
        {
            ClearRequested?.Invoke();
            _activeSpawns.Clear();
            BuildTask();
            StartTimers();
        }
    }

    private async Task HandleSurvivalCompleteAsync()
    {
        if (_task is null) return;

        if (_hits >= _task.MinimumHits)
        {
            await CompleteLevelAsync();
            return;
        }

        await ApplyMistakeAsync("Nicht genug Treffer im Survival-Level!");
        if (Lives > 0)
        {
            ClearRequested?.Invoke();
            _activeSpawns.Clear();
            BuildTask();
            StartTimers();
        }
    }

    private void ToggleSwitchRule()
    {
        if (_task?.SwitchRule is null) return;

        _task.SwitchRule.Toggle();
        TaskDetail = _task.SwitchRule.CurrentDescription;
    }

    private void SpawnNumber()
    {
        if (_task is null) return;

        int value = GenerateNumber();
        var spawn = new NumberRainSpawn(
            Guid.NewGuid().ToString(),
            value,
            _settings.FallDurationMs);

        _activeSpawns[spawn.Id] = spawn;
        Spawned?.Invoke(spawn);
    }

    private int GenerateNumber()
    {
        if (_task is null) return _random.Next(_settings.MinValue, _settings.MaxValue + 1);

        bool preferTarget = _random.NextDouble() < 0.65;
        if (preferTarget)
        {
            var predicate = PickTargetPredicate();
            for (int i = 0; i < 25; i++)
            {
                int candidate = _random.Next(_settings.MinValue, _settings.MaxValue + 1);
                if (predicate(candidate)) return candidate;
            }
        }

        return _random.Next(_settings.MinValue, _settings.MaxValue + 1);
    }

    private Func<int, bool> PickTargetPredicate()
    {
        if (_task is null) return _ => false;

        if (_task.Mode is NumberRainTaskMode.ChainGreater)
            return n => _lastSelectedValue is null || n > _lastSelectedValue.Value;

        if (_task.Mode is NumberRainTaskMode.ChainDivisible)
            return n => _lastSelectedValue is null || (_lastSelectedValue.Value != 0 && n % _lastSelectedValue.Value == 0);

        if (_task.Mode is NumberRainTaskMode.Switch && _task.SwitchRule is not null)
            return _task.SwitchRule.CurrentPredicate;

        if (_task.Mode is NumberRainTaskMode.Combo && _task.ComboPredicate is not null)
            return _task.ComboPredicate;

        var pending = _objectives.FirstOrDefault(o => !o.IsCompleted) ?? _objectives.FirstOrDefault();
        return pending?.Predicate ?? (_ => false);
    }

    public async Task SelectNumberAsync(string id)
    {
        if (!_activeSpawns.TryGetValue(id, out var spawn)) return;
        _activeSpawns.Remove(id);

        if (_task is null) return;

        if (_task.BombPredicate?.Invoke(spawn.Value) == true)
        {
            await ApplyMistakeAsync($"💣 Bombe! {spawn.Value} war tabu.");
            return;
        }

        bool isTarget = IsTargetValue(spawn.Value);
        if (!isTarget)
        {
            ComboCurrent = 0;
            await ApplyMistakeAsync($"❌ {spawn.Value} war falsch.");
            return;
        }

        RegisterHit(spawn.Value);
    }

    public async Task HandleMissAsync(string id)
    {
        if (!_activeSpawns.TryGetValue(id, out var spawn)) return;
        _activeSpawns.Remove(id);

        if (_task is null) return;

        if (IsTargetValue(spawn.Value))
        {
            ComboCurrent = 0;
            await ApplyMistakeAsync($"⌛ {spawn.Value} verpasst.");
        }
    }

    private bool IsTargetValue(int value)
    {
        if (_task is null) return false;

        return _task.Mode switch
        {
            NumberRainTaskMode.ChainGreater => _lastSelectedValue is null || value > _lastSelectedValue.Value,
            NumberRainTaskMode.ChainDivisible => _lastSelectedValue is null || (_lastSelectedValue.Value != 0 && value % _lastSelectedValue.Value == 0),
            NumberRainTaskMode.Switch when _task.SwitchRule is not null => _task.SwitchRule.CurrentPredicate(value),
            NumberRainTaskMode.Combo when _task.ComboPredicate is not null => _task.ComboPredicate(value),
            _ => _objectives.Any(o => o.Predicate(value))
        };
    }

    private void RegisterHit(int value)
    {
        if (_task is null) return;

        _hits++;

        if (_task.Mode == NumberRainTaskMode.Combo)
        {
            ComboCurrent++;
            if (_task.ComboTarget > 0 && ComboCurrent >= _task.ComboTarget)
            {
                _ = CompleteLevelAsync();
            }
            return;
        }

        if (_task.Mode is NumberRainTaskMode.ChainGreater or NumberRainTaskMode.ChainDivisible)
        {
            _lastSelectedValue = value;
            TaskDetail = $"Letzte Wahl: {value}";
        }

        if (_objectives.Count == 0) return;

        var target = _objectives.FirstOrDefault(o => !o.IsCompleted && o.Predicate(value))
                     ?? _objectives.FirstOrDefault(o => o.Predicate(value));

        if (target is not null)
        {
            target.Increment();
        }

        if (IsCompleted())
        {
            _ = CompleteLevelAsync();
        }
    }

    private bool IsCompleted()
    {
        if (_task is null) return false;

        if (_task.Mode == NumberRainTaskMode.Combo)
            return _task.ComboTarget > 0 && ComboCurrent >= _task.ComboTarget;

        if (_task.Mode == NumberRainTaskMode.Survival)
            return TimeRemaining == 0 && _hits >= _task.MinimumHits;

        return _objectives.All(o => o.IsCompleted);
    }

    private async Task CompleteLevelAsync()
    {
        if (_task is null) return;

        StopTimers();
        ClearRequested?.Invoke();
        _activeSpawns.Clear();

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
            await _userService.SaveUserAsync(user);
            Coins = user.Coins;
        }

        int completedLevel = LevelNumber;
        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, completedLevel);

        await _dialog.AlertAsync("Level geschafft!", $"Super! +{reward} Coins");

        LevelNumber = completedLevel + 1;
        await StartLevelAsync();
    }

    private async Task ApplyMistakeAsync(string message)
    {
        Lives = Math.Max(0, Lives - 1);

        if (Lives <= 0)
        {
            StopTimers();
            ClearRequested?.Invoke();
            _activeSpawns.Clear();
            await _dialog.AlertAsync("Game Over", "Keine Leben mehr. Versuch es erneut!");
            await StartLevelAsync();
            return;
        }

        await _dialog.AlertAsync("Achtung", message);
    }

    private async Task ConfirmBackAsync()
    {
        bool leave = await _dialog.ConfirmAsync("Zurück", "Möchtest du das Spiel verlassen?");
        if (!leave) return;
        Stop();
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

            return Math.Abs(hash);
        }
    }

    private sealed class DifficultySettings
    {
        public int MinValue { get; init; }
        public int MaxValue { get; init; }
        public int SpawnIntervalMs { get; init; }
        public int FallDurationMs { get; init; }
        public int MinTarget { get; init; }
        public int MaxTarget { get; init; }
        public int MinTime { get; init; }
        public int MaxTime { get; init; }
        public int MinCombo { get; init; }
        public int MaxCombo { get; init; }
        public int MinSurvivalHits { get; init; }
        public int MaxSurvivalHits { get; init; }
        public int MinSurvivalSeconds { get; init; }
        public int MaxSurvivalSeconds { get; init; }

        public static DifficultySettings For(string key) => key switch
        {
            "easy" => new DifficultySettings
            {
                MinValue = 1,
                MaxValue = 80,
                SpawnIntervalMs = 750,
                FallDurationMs = 5200,
                MinTarget = 6,
                MaxTarget = 12,
                MinTime = 15,
                MaxTime = 25,
                MinCombo = 5,
                MaxCombo = 9,
                MinSurvivalHits = 6,
                MaxSurvivalHits = 10,
                MinSurvivalSeconds = 16,
                MaxSurvivalSeconds = 26
            },
            "normal" => new DifficultySettings
            {
                MinValue = 10,
                MaxValue = 150,
                SpawnIntervalMs = 650,
                FallDurationMs = 4800,
                MinTarget = 8,
                MaxTarget = 14,
                MinTime = 16,
                MaxTime = 26,
                MinCombo = 6,
                MaxCombo = 10,
                MinSurvivalHits = 7,
                MaxSurvivalHits = 12,
                MinSurvivalSeconds = 18,
                MaxSurvivalSeconds = 28
            },
            "hard" => new DifficultySettings
            {
                MinValue = 20,
                MaxValue = 260,
                SpawnIntervalMs = 560,
                FallDurationMs = 4400,
                MinTarget = 10,
                MaxTarget = 16,
                MinTime = 18,
                MaxTime = 30,
                MinCombo = 7,
                MaxCombo = 11,
                MinSurvivalHits = 9,
                MaxSurvivalHits = 14,
                MinSurvivalSeconds = 20,
                MaxSurvivalSeconds = 32
            },
            "master" => new DifficultySettings
            {
                MinValue = 30,
                MaxValue = 420,
                SpawnIntervalMs = 520,
                FallDurationMs = 4200,
                MinTarget = 10,
                MaxTarget = 18,
                MinTime = 20,
                MaxTime = 32,
                MinCombo = 8,
                MaxCombo = 12,
                MinSurvivalHits = 10,
                MaxSurvivalHits = 16,
                MinSurvivalSeconds = 22,
                MaxSurvivalSeconds = 36
            },
            _ => new DifficultySettings
            {
                MinValue = 1,
                MaxValue = 80,
                SpawnIntervalMs = 750,
                FallDurationMs = 5200,
                MinTarget = 6,
                MaxTarget = 12,
                MinTime = 15,
                MaxTime = 25,
                MinCombo = 5,
                MaxCombo = 9,
                MinSurvivalHits = 6,
                MaxSurvivalHits = 10,
                MinSurvivalSeconds = 16,
                MaxSurvivalSeconds = 26
            }
        };
    }

    private enum NumberRainTaskMode
    {
        Standard,
        Combo,
        Survival,
        ChainGreater,
        ChainDivisible,
        Switch
    }

    private sealed class NumberRainObjectiveDefinition
    {
        public string Title { get; }
        public int Target { get; }
        public Func<int, bool> Predicate { get; }

        public NumberRainObjectiveDefinition(string title, int target, Func<int, bool> predicate)
        {
            Title = title;
            Target = target;
            Predicate = predicate;
        }
    }

    private sealed class NumberRainTask
    {
        public string Title { get; init; } = "";
        public string Detail { get; init; } = "";
        public List<NumberRainObjectiveDefinition> Objectives { get; init; } = new();
        public Func<int, bool>? BombPredicate { get; init; }
        public int TimeLimitSeconds { get; init; }
        public int ComboTarget { get; init; }
        public Func<int, bool>? ComboPredicate { get; init; }
        public int SurvivalSeconds { get; init; }
        public int MinimumHits { get; init; }
        public NumberRainTaskMode Mode { get; init; }
        public SwitchRule? SwitchRule { get; init; }
    }

    private sealed class SwitchRule
    {
        private readonly string _descA;
        private readonly string _descB;
        private readonly Func<int, bool> _ruleA;
        private readonly Func<int, bool> _ruleB;
        private bool _useA = true;

        public int SwitchSeconds { get; }

        public SwitchRule(string descA, Func<int, bool> ruleA, string descB, Func<int, bool> ruleB, int switchSeconds)
        {
            _descA = descA;
            _descB = descB;
            _ruleA = ruleA;
            _ruleB = ruleB;
            SwitchSeconds = switchSeconds;
        }

        public Func<int, bool> CurrentPredicate => _useA ? _ruleA : _ruleB;
        public string CurrentDescription => _useA ? _descA : _descB;

        public void Toggle() => _useA = !_useA;
    }

    public sealed class NumberRainSpawn
    {
        public string Id { get; }
        public int Value { get; }
        public int FallDurationMs { get; }

        public NumberRainSpawn(string id, int value, int fallDurationMs)
        {
            Id = id;
            Value = value;
            FallDurationMs = fallDurationMs;
        }
    }

    private static class NumberRainTaskFactory
    {
        public static NumberRainTask Create(Random rnd, DifficultySettings settings, string difficulty)
        {
            return difficulty switch
            {
                "easy" => CreateEasy(rnd, settings),
                "normal" => CreateNormal(rnd, settings),
                "hard" => CreateHard(rnd, settings),
                "master" => CreateMaster(rnd, settings),
                _ => CreateEasy(rnd, settings)
            };
        }

        private static NumberRainTask CreateEasy(Random rnd, DifficultySettings settings)
        {
            int n = rnd.Next(settings.MinTarget, settings.MaxTarget + 1);
            int t = rnd.Next(settings.MinTime, settings.MaxTime + 1);
            int k = rnd.Next(2, 11);
            int d = rnd.Next(0, 10);
            int x = rnd.Next(1, 3);
            int a = rnd.Next(settings.MinValue, settings.MaxValue - 10);
            int b = rnd.Next(a + 5, settings.MaxValue + 1);
            int s = rnd.Next(4, 15);
            int n1 = rnd.Next(3, 7);
            int n2 = rnd.Next(3, 7);

            var options = new List<Func<NumberRainTask>>
            {
                () => Simple(n, $"Wähle {n} gerade Zahlen", IsEven),
                () => Simple(n, $"Wähle {n} ungerade Zahlen", IsOdd),
                () => Simple(n, $"Wähle {n} Vielfache von {k}", value => value % k == 0),
                () => Simple(n, $"Wähle {n} Zahlen, die auf {d} enden", value => value % 10 == d),
                () => Simple(n, $"Wähle {n} Zahlen zwischen {a} und {b}", value => value >= a && value <= b),
                () => Simple(n, $"Wähle {n} Zahlen < {b}", value => value < b),
                () => Simple(n, $"Wähle {n} Zahlen > {a}", value => value > a),
                () => Simple(n, $"Wähle {n} zweistellige Zahlen", value => value is >= 10 and <= 99),
                () => Simple(n, $"Wähle {n} Zahlen mit genau {x} Stellen", value => DigitCount(value) == x),
                () => Simple(n, $"Wähle {n} Zahlen, die die Ziffer {d} enthalten", value => ContainsDigit(value, d)),
                () => Simple(n, $"Wähle {n} Zahlen, die die Ziffer {d} NICHT enthalten", value => !ContainsDigit(value, d)),
                () => Simple(n, $"Wähle {n} Zahlen mit gerader Quersumme", value => DigitSum(value) % 2 == 0),
                () => Simple(n, $"Wähle {n} Zahlen mit ungerader Quersumme", value => DigitSum(value) % 2 == 1),
                () => Simple(n, $"Wähle {n} Zahlen mit Quersumme = {s}", value => DigitSum(value) == s),
                () => Simple(n, $"Wähle {n} Zahlen, die durch 2 ODER 5 teilbar sind", value => value % 2 == 0 || value % 5 == 0),
                () => Timed(n, t, $"In {t}s: Wähle {n} gerade Zahlen", IsEven),
                () => Timed(n, t, $"In {t}s: Wähle {n} Vielfache von {k}", value => value % k == 0),
                () => Timed(n, t, $"In {t}s: Wähle {n} Zahlen, die auf {d} enden", value => value % 10 == d),
                () => Timed(n, t, $"In {t}s: Wähle {n} Zahlen im Bereich {a}–{b}", value => value >= a && value <= b),
                () => Avoid(n, $"Wähle {n} Vielfache von {k}, aber klicke NIE auf Zahlen die auf {d} enden",
                    value => value % k == 0, value => value % 10 == d),
                () => Avoid(n, $"Wähle {n} Zahlen mit Ziffer {d}, aber meide gerade Zahlen",
                    value => ContainsDigit(value, d), IsEven),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition($"Gerade Zahlen", n1, IsEven),
                        new NumberRainObjectiveDefinition($"Ungerade Zahlen", n2, IsOdd)
                    },
                    $"Sammle {n1} gerade und {n2} ungerade Zahlen"),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition($"Vielfache von {k}", n1, value => value % k == 0),
                        new NumberRainObjectiveDefinition($"Vielfache von {k + 1}", n2, value => value % (k + 1) == 0)
                    },
                    $"Sammle {n1} Vielfache von {k} und {n2} Vielfache von {k + 1}"),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition($"Bereich {a}-{b}", n1, value => value >= a && value <= b),
                        new NumberRainObjectiveDefinition($"Ziffer {d}", n2, value => ContainsDigit(value, d))
                    },
                    $"Sammle {n1} Zahlen im Bereich {a}–{b} und {n2} Zahlen mit Ziffer {d}")
            };

            return options[rnd.Next(options.Count)]();
        }

        private static NumberRainTask CreateNormal(Random rnd, DifficultySettings settings)
        {
            int n = rnd.Next(settings.MinTarget, settings.MaxTarget + 2);
            int t = rnd.Next(settings.MinTime, settings.MaxTime + 2);
            int k = rnd.Next(2, 11);
            int j = rnd.Next(2, 11);
            int d = rnd.Next(0, 10);
            int s = rnd.Next(6, 25);
            int a = rnd.Next(settings.MinValue, settings.MaxValue - 30);
            int b = rnd.Next(a + 10, settings.MaxValue + 1);
            int c = rnd.Next(settings.MinCombo, settings.MaxCombo + 1);
            int n1 = rnd.Next(4, 8);
            int n2 = rnd.Next(4, 8);

            var options = new List<Func<NumberRainTask>>
            {
                () => Simple(n, $"Wähle {n} Primzahlen", IsPrime),
                () => Simple(n, $"Wähle {n} Nicht-Primzahlen", value => value > 1 && !IsPrime(value)),
                () => Simple(n, $"Wähle {n} Quadratzahlen", IsSquare),
                () => Simple(n, $"Wähle {n} Zahlen, die durch {k} UND {j} teilbar sind", value => value % k == 0 && value % j == 0),
                () => Simple(n, $"Wähle {n} Zahlen, die durch {k} teilbar sind, aber NICHT durch {j}", value => value % k == 0 && value % j != 0),
                () => Simple(n, $"Wähle {n} Palindromzahlen", IsPalindrome),
                () => Simple(n, $"Wähle {n} Zahlen mit Quersumme prim", value => IsPrime(DigitSum(value))),
                () => Simple(n, $"Wähle {n} Zahlen mit Quersumme = {s}", value => DigitSum(value) == s),
                () => Simple(n, $"Wähle {n} Zahlen, die Ziffer {d} enthalten UND gerade sind", value => ContainsDigit(value, d) && IsEven(value)),
                () => Simple(n, $"Wähle {n} Zahlen, die Ziffer {d} enthalten UND NICHT durch {k} teilbar sind", value => ContainsDigit(value, d) && value % k != 0),
                () => Simple(n, $"Wähle {n} Zahlen mit mindestens zwei gleichen Ziffern", value => HasDuplicateDigits(value)),
                () => Simple(n, $"Wähle {n} Zahlen mit allen Ziffern verschieden", value => AllDigitsDistinct(value)),
                () => Simple(n, $"Wähle {n} Zahlen, die näher an {a} als an {b} sind", value => Math.Abs(value - a) < Math.Abs(value - b)),
                () => Timed(n, t, $"In {t}s: Wähle {n} Primzahlen", IsPrime),
                () => Timed(n, t, $"In {t}s: Wähle {n} Quadratzahlen", IsSquare),
                () => Timed(n, t, $"In {t}s: Wähle {n} Zahlen (durch {k} teilbar, aber nicht durch {j})", value => value % k == 0 && value % j != 0),
                () => Combo(c, $"Erreiche Combo {c} mit Regel: Vielfache von {k}", value => value % k == 0),
                () => Combo(c, $"Erreiche Combo {c} mit Regel: Ziffer {d} enthalten", value => ContainsDigit(value, d)),
                () => Combo(c, $"Erreiche Combo {c} mit Regel: Quersumme gerade", value => DigitSum(value) % 2 == 0),
                () => Avoid(n, $"Wähle {n} Quadratzahlen, aber meide Zahlen mit Ziffer {d}", IsSquare, value => ContainsDigit(value, d)),
                () => Avoid(n, $"Wähle {n} Primzahlen, aber klicke NIE auf Vielfache von {k}", IsPrime, value => value % k == 0),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition("Primzahlen", n1, IsPrime),
                        new NumberRainObjectiveDefinition($"Vielfache von {k}", n2, value => value % k == 0)
                    },
                    $"Sammle {n1} Primzahlen und {n2} Vielfache von {k}"),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition("Palindromzahlen", n1, IsPalindrome),
                        new NumberRainObjectiveDefinition($"Quersumme {s}", n2, value => DigitSum(value) == s)
                    },
                    $"Sammle {n1} Palindromzahlen und {n2} Zahlen mit Quersumme {s}"),
                () => TimedMulti(n1, n2, t,
                    $"In {t}s: Sammle {n1} Quadratzahlen und {n2} ungerade Vielfache von {k}",
                    IsSquare,
                    value => value % k == 0 && IsOdd(value))
            };

            return options[rnd.Next(options.Count)]();
        }

        private static NumberRainTask CreateHard(Random rnd, DifficultySettings settings)
        {
            int n = rnd.Next(settings.MinTarget + 2, settings.MaxTarget + 4);
            int t = rnd.Next(settings.MinTime + 2, settings.MaxTime + 4);
            int k = rnd.Next(2, 13);
            int d = rnd.Next(0, 10);
            int e = (d + rnd.Next(1, 9)) % 10;
            int m = rnd.Next(3, 12);
            int r = rnd.Next(0, m);
            int a = rnd.Next(settings.MinValue, settings.MaxValue - 40);
            int s = rnd.Next(10, 30);
            int n1 = rnd.Next(5, 9);
            int n2 = rnd.Next(5, 9);
            int survivalT = rnd.Next(settings.MinSurvivalSeconds, settings.MaxSurvivalSeconds + 1);
            int survivalHits = rnd.Next(settings.MinSurvivalHits, settings.MaxSurvivalHits + 1);
            int survivalMod = rnd.Next(3, 11);
            int survivalRest = rnd.Next(0, survivalMod);

            var options = new List<Func<NumberRainTask>>
            {
                () => Simple(n, $"Wähle {n} Zahlen mit Rest {r} bei Division durch {m}", value => value % m == r),
                () => Simple(n, $"Wähle {n} Zahlen: Prim UND > {a}", value => IsPrime(value) && value > a),
                () => Simple(n, $"Wähle {n} Zahlen: Prim UND enthält Ziffer {d}", value => IsPrime(value) && ContainsDigit(value, d)),
                () => Simple(n, $"Wähle {n} Zahlen: Quadratzahl ODER Prim", value => IsSquare(value) || IsPrime(value)),
                () => Simple(n, $"Wähle {n} Zahlen: (Vielfache von {k}) UND (Quersumme prim)", value => value % k == 0 && IsPrime(DigitSum(value))),
                () => Simple(n, $"Wähle {n} Fibonacci-Zahlen", IsFibonacci),
                () => Simple(n, $"Wähle {n} Potenzen von 2", IsPowerOfTwo),
                () => Simple(n, $"Wähle {n} Zahlen mit streng steigenden Ziffern", HasStrictlyIncreasingDigits),
                () => Simple(n, $"Wähle {n} Zahlen mit streng fallenden Ziffern", HasStrictlyDecreasingDigits),
                () => Simple(n, $"Wähle {n} Zahlen: enthält {d}, aber enthält NICHT {e}", value => ContainsDigit(value, d) && !ContainsDigit(value, e)),
                () => Simple(n, $"Wähle {n} Zahlen: durch {k} teilbar, aber Quersumme ungerade", value => value % k == 0 && DigitSum(value) % 2 == 1),
                () => Simple(n, $"Wähle {n} Zahlen: Palindrom UND durch {k} teilbar", value => IsPalindrome(value) && value % k == 0),
                () => Timed(n, t, $"In {t}s: Wähle {n} Zahlen mit mod {m} = {r}", value => value % m == r),
                () => Timed(n, t, $"In {t}s: Wähle {n} Zahlen (Prim UND > {a})", value => IsPrime(value) && value > a),
                () => Timed(n, t, $"In {t}s: Wähle {n} Zahlen (Quersumme prim UND enthält {d})", value => IsPrime(DigitSum(value)) && ContainsDigit(value, d)),
                () => Avoid(n, $"Wähle {n} Zahlen (mod {m}={r}), aber meide Quadratzahlen", value => value % m == r, IsSquare),
                () => Avoid(n, $"Wähle {n} Zahlen (Vielfache von {k}), aber meide Palindrome", value => value % k == 0, IsPalindrome),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition($"mod {m}={r}", n1, value => value % m == r),
                        new NumberRainObjectiveDefinition($"Quersumme {s}", n2, value => DigitSum(value) == s)
                    },
                    $"Sammle {n1} Zahlen (mod {m}={r}) und {n2} Zahlen (Quersumme = {s})"),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition("Potenzen von 2", n1, IsPowerOfTwo),
                        new NumberRainObjectiveDefinition("Primzahlen", n2, IsPrime)
                    },
                    $"Sammle {n1} Potenzen von 2 und {n2} Primzahlen"),
                () => TimedMulti(n1, n2, t,
                    $"In {t}s: Sammle {n1} Fibonacci und {n2} Zahlen mit Ziffer {d}",
                    IsFibonacci,
                    value => ContainsDigit(value, d)),
                () => Survival(
                    survivalT,
                    survivalHits,
                    $"Überlebe {survivalT}s und mache mindestens {survivalHits} Treffer",
                    value => value % m == r,
                    $"Regel: mod {m} = {r}"),
                () => Survival(
                    survivalT,
                    survivalHits,
                    $"Überlebe {survivalT}s mit max. {LivesMax} Fehlklicks",
                    value => IsPrime(value) || value % survivalMod == survivalRest,
                    $"Regel: Prim ODER mod {survivalMod} = {survivalRest}")
            };

            return options[rnd.Next(options.Count)]();
        }

        private static NumberRainTask CreateMaster(Random rnd, DifficultySettings settings)
        {
            int n = rnd.Next(settings.MinTarget + 2, settings.MaxTarget + 5);
            int t = rnd.Next(settings.MinTime + 4, settings.MaxTime + 6);
            int k = rnd.Next(2, 13);
            int d = rnd.Next(0, 10);
            int e = (d + rnd.Next(1, 9)) % 10;
            int m = rnd.Next(3, 14);
            int r = rnd.Next(0, m);
            int s = rnd.Next(12, 35);
            int n1 = rnd.Next(5, 9);
            int n2 = rnd.Next(5, 9);
            int n3 = rnd.Next(4, 8);

            var options = new List<Func<NumberRainTask>>
            {
                () => Simple(n, $"Wähle {n} Semiprime (Produkt aus genau 2 Primzahlen)", IsSemiprime),
                () => Simple(n, $"Wähle {n} squarefree Zahlen", IsSquareFree),
                () => Simple(n, $"Wähle {n} Zahlen: (mod {m}={r}) UND (Quersumme prim)", value => value % m == r && IsPrime(DigitSum(value))),
                () => Simple(n, $"Wähle {n} Zahlen: (Prim) UND (Quersumme = {s})", value => IsPrime(value) && DigitSum(value) == s),
                () => Simple(n, $"Wähle {n} Zahlen: (contains {d}) UND (mod {m}={r}) UND (ungerade)", value => ContainsDigit(value, d) && value % m == r && IsOdd(value)),
                () => Simple(n, $"Wähle {n} Zahlen: (Harshad) UND (nicht durch 10 teilbar)", value => IsHarshad(value) && value % 10 != 0),
                () => Simple(n, $"Wähle {n} Zahlen: (pronic n(n+1))", IsPronic),
                () => Simple(n, $"Wähle {n} Zahlen: (Automorph)", IsAutomorphic),
                () => Simple(n, $"Wähle {n} Zahlen: (Palindrom) UND (nicht prim)", value => IsPalindrome(value) && !IsPrime(value)),
                () => Simple(n, $"Wähle {n} Zahlen: (Quadratzahl) UND (enthält {d})", value => IsSquare(value) && ContainsDigit(value, d)),
                () => Timed(n, t, $"In {t}s: Sammle {n} (Semiprime)", IsSemiprime),
                () => Timed(n, t, $"In {t}s: Sammle {n} (squarefree)", IsSquareFree),
                () => Timed(n, t, $"In {t}s: Sammle {n} (mod {m}={r} UND Quersumme prim)", value => value % m == r && IsPrime(DigitSum(value))),
                () => Avoid(n, $"Wähle {n} Treffer (Regel R), aber klicke NIE auf Zahlen mit Eigenschaft X",
                    value => value % m == r, IsPrime),
                () => Avoid(n, $"Wähle {n} Treffer (Regel R), aber jede Zahl mit Ziffer {d} ist Bombe",
                    value => value % k == 0, value => ContainsDigit(value, d)),
                () => Avoid(n, $"Wähle {n} Treffer (Regel R), aber jede Quadratzahl ist Bombe",
                    value => IsPrime(value), IsSquare),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition("Semiprime", n1, IsSemiprime),
                        new NumberRainObjectiveDefinition($"mod {m}={r}", n2, value => value % m == r),
                        new NumberRainObjectiveDefinition($"Quersumme {s}", n3, value => DigitSum(value) == s)
                    },
                    $"Sammle {n1} (Semiprime) + {n2} (mod {m}={r}) + {n3} (Quersumme = {s})"),
                () => TimedMulti(n1, n2, t,
                    $"In {t}s: {n1} (Prim) + {n2} (Potenzen von 2)",
                    IsPrime,
                    IsPowerOfTwo),
                () => Multi(new[]
                    {
                        new NumberRainObjectiveDefinition("squarefree", n1, IsSquareFree),
                        new NumberRainObjectiveDefinition($"enthält {d} aber nicht {e}", n2, value => ContainsDigit(value, d) && !ContainsDigit(value, e))
                    },
                    $"Sammle {n1} (squarefree) und {n2} (enthält {d} aber nicht {e})"),
                () => Chain(n, true, $"Kettenregel: Wähle {n} Zahlen, die > der letzten Wahl sind"),
                () => Chain(n, false, $"Kettenregel: Wähle {n} Zahlen, die durch die letzte Wahl teilbar sind"),
                () => Switch(n, $"Wechselregel: Alle 10 Sekunden ändert sich die Regel",
                    new SwitchRule($"Regel A: Vielfache von {k}", value => value % k == 0,
                                    $"Regel B: Ziffer {d} enthalten", value => ContainsDigit(value, d), 10))
            };

            return options[rnd.Next(options.Count)]();
        }

        private static NumberRainTask Simple(int n, string title, Func<int, bool> predicate)
        {
            return new NumberRainTask
            {
                Title = title,
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Ziel", n, predicate)
                },
                Mode = NumberRainTaskMode.Standard
            };
        }

        private static NumberRainTask Timed(int n, int t, string title, Func<int, bool> predicate)
        {
            return new NumberRainTask
            {
                Title = title,
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Ziel", n, predicate)
                },
                TimeLimitSeconds = t,
                Mode = NumberRainTaskMode.Standard
            };
        }

        private static NumberRainTask Avoid(int n, string title, Func<int, bool> predicate, Func<int, bool> bomb)
        {
            return new NumberRainTask
            {
                Title = title,
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Ziel", n, predicate)
                },
                BombPredicate = bomb,
                Mode = NumberRainTaskMode.Standard
            };
        }

        private static NumberRainTask Multi(IEnumerable<NumberRainObjectiveDefinition> objectives, string title)
        {
            return new NumberRainTask
            {
                Title = title,
                Objectives = objectives.ToList(),
                Mode = NumberRainTaskMode.Standard
            };
        }

        private static NumberRainTask TimedMulti(int n1, int n2, int t, string title, Func<int, bool> predicate1, Func<int, bool> predicate2)
        {
            return new NumberRainTask
            {
                Title = title,
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Ziel A", n1, predicate1),
                    new NumberRainObjectiveDefinition("Ziel B", n2, predicate2)
                },
                TimeLimitSeconds = t,
                Mode = NumberRainTaskMode.Standard
            };
        }

        private static NumberRainTask Combo(int comboTarget, string title, Func<int, bool> predicate)
        {
            return new NumberRainTask
            {
                Title = title,
                ComboTarget = comboTarget,
                ComboPredicate = predicate,
                Mode = NumberRainTaskMode.Combo
            };
        }

        private static NumberRainTask Survival(int seconds, int minHits, string title, Func<int, bool> predicate, string detail)
        {
            return new NumberRainTask
            {
                Title = title,
                Detail = detail,
                SurvivalSeconds = seconds,
                MinimumHits = minHits,
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Treffer", minHits, predicate)
                },
                Mode = NumberRainTaskMode.Survival
            };
        }

        private static NumberRainTask Chain(int n, bool greater, string title)
        {
            return new NumberRainTask
            {
                Title = title,
                Detail = "Letzte Wahl: –",
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Ziel", n, _ => true)
                },
                Mode = greater ? NumberRainTaskMode.ChainGreater : NumberRainTaskMode.ChainDivisible
            };
        }

        private static NumberRainTask Switch(int n, string title, SwitchRule rule)
        {
            return new NumberRainTask
            {
                Title = title,
                Detail = rule.CurrentDescription,
                Objectives = new List<NumberRainObjectiveDefinition>
                {
                    new NumberRainObjectiveDefinition("Ziel", n, _ => true)
                },
                Mode = NumberRainTaskMode.Switch,
                SwitchRule = rule
            };
        }

        private static bool IsEven(int value) => value % 2 == 0;
        private static bool IsOdd(int value) => value % 2 != 0;

        private static bool IsPrime(int value)
        {
            if (value <= 1) return false;
            if (value <= 3) return true;
            if (value % 2 == 0 || value % 3 == 0) return false;
            int limit = (int)Math.Sqrt(value);
            for (int i = 5; i <= limit; i += 6)
            {
                if (value % i == 0 || value % (i + 2) == 0)
                    return false;
            }
            return true;
        }

        private static bool IsSquare(int value)
        {
            if (value < 0) return false;
            int root = (int)Math.Sqrt(value);
            return root * root == value;
        }

        private static bool IsPalindrome(int value)
        {
            string s = Math.Abs(value).ToString();
            return s.SequenceEqual(s.Reverse());
        }

        private static bool ContainsDigit(int value, int digit)
        {
            string s = Math.Abs(value).ToString();
            return s.Contains(digit.ToString());
        }

        private static int DigitSum(int value)
        {
            int sum = 0;
            int n = Math.Abs(value);
            while (n > 0)
            {
                sum += n % 10;
                n /= 10;
            }
            return sum;
        }

        private static int DigitCount(int value)
        {
            int n = Math.Abs(value);
            if (n == 0) return 1;
            int count = 0;
            while (n > 0)
            {
                count++;
                n /= 10;
            }
            return count;
        }

        private static bool HasDuplicateDigits(int value)
        {
            string s = Math.Abs(value).ToString();
            return s.Length != s.Distinct().Count();
        }

        private static bool AllDigitsDistinct(int value)
        {
            string s = Math.Abs(value).ToString();
            return s.Length == s.Distinct().Count();
        }

        private static bool IsFibonacci(int value)
        {
            if (value < 0) return false;
            int a = 0;
            int b = 1;
            while (a < value)
            {
                int next = a + b;
                a = b;
                b = next;
            }
            return value == a;
        }

        private static bool IsPowerOfTwo(int value)
        {
            if (value <= 0) return false;
            return (value & (value - 1)) == 0;
        }

        private static bool HasStrictlyIncreasingDigits(int value)
        {
            string s = Math.Abs(value).ToString();
            for (int i = 1; i < s.Length; i++)
            {
                if (s[i] <= s[i - 1]) return false;
            }
            return s.Length > 1;
        }

        private static bool HasStrictlyDecreasingDigits(int value)
        {
            string s = Math.Abs(value).ToString();
            for (int i = 1; i < s.Length; i++)
            {
                if (s[i] >= s[i - 1]) return false;
            }
            return s.Length > 1;
        }

        private static bool IsSemiprime(int value)
        {
            if (value < 4) return false;
            int count = 0;
            int n = value;
            for (int p = 2; p * p <= n; p++)
            {
                while (n % p == 0)
                {
                    n /= p;
                    count++;
                    if (count > 2) return false;
                }
            }
            if (n > 1) count++;
            return count == 2;
        }

        private static bool IsSquareFree(int value)
        {
            int n = Math.Abs(value);
            if (n == 0) return false;
            for (int p = 2; p * p <= n; p++)
            {
                int p2 = p * p;
                if (n % p2 == 0) return false;
            }
            return true;
        }

        private static bool IsHarshad(int value)
        {
            int sum = DigitSum(value);
            return sum != 0 && value % sum == 0;
        }

        private static bool IsPronic(int value)
        {
            if (value < 0) return false;
            int n = (int)Math.Floor(Math.Sqrt(value));
            return n * (n + 1) == value || (n - 1) * n == value;
        }

        private static bool IsAutomorphic(int value)
        {
            if (value < 0) return false;
            long square = (long)value * value;
            string s = value.ToString();
            return square.ToString().EndsWith(s, StringComparison.Ordinal);
        }
    }
}
