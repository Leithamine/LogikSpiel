#nullable enable
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Model.MathHangman;
using LogikSpiel.Services;
using LogikSpiel.Services.MathHangman;

namespace LogikSpiel.ViewModel;

public sealed class MathHangmanPageViewModel : ObservableObject
{
    private const int LivesMax = 6;

    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly IUserProfileService _userService;
    private readonly IMathHangmanService _hangmanService;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialog;

    public string GameId { get; private set; } = "math_hangman";
    public string DifficultyKey { get; private set; } = "normal";
    public int LevelNumber { get; private set; } = 1;

    public string DifficultyText => "Zufällig";

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    private int _lives = LivesMax;
    public int Lives
    {
        get => _lives;
        private set
        {
            if (SetProperty(ref _lives, value))
                OnPropertyChanged(nameof(WrongCount));
        }
    }

    public int WrongCount => LivesMax - Lives;

    private int _secret;

    private bool _isFinished;
    public bool IsFinished { get => _isFinished; private set => SetProperty(ref _isFinished, value); }

    private string _guessText = "";
    public string GuessText { get => _guessText; set => SetProperty(ref _guessText, value); }

    private int _currentSeed;

    // Aktueller Hinweis
    private string _currentHint = "";
    public string CurrentHint { get => _currentHint; private set { SetProperty(ref _currentHint, value); OnPropertyChanged(nameof(HasHint)); } }
    public bool HasHint => !string.IsNullOrWhiteSpace(CurrentHint);

    // Alle TRUE-Eigenschaften - werden von Anfang an gezeigt
    public ObservableCollection<NumberPropertyVM> VisibleProperties { get; } = new();

    // Shop Hinweise
    public ObservableCollection<ShopHint> ShopItems { get; } = new();

    // Range für Hinweise
    private int _rangeA;
    private int _rangeB;

    public AsyncCommand BackCommand { get; }
    public AsyncCommand GuessCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand<NumberPropertyVM> ExplainPropertyCommand { get; }

    public MathHangmanPageViewModel(
        IGameCatalogService catalog,
        IGameProgressStore progressStore,
        IUserProfileService userService,
        IMathHangmanService hangmanService,
        INavigationService nav,
        IDialogService dialog)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _userService = userService;
        _hangmanService = hangmanService;
        _nav = nav;
        _dialog = dialog;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
        GuessCommand = new AsyncCommand(GuessAsync);
        HintCommand = new AsyncCommand(BuyHintAsync);

        ExplainPropertyCommand = new AsyncCommand<NumberPropertyVM>(async p =>
        {
            if (p == null) return;
            await _dialog.AlertAsync(p.Key, $"{p.KidDescription}\n\nBeispiele:\n{p.Examples}");
        });

        BuildShop();
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "math_hangman" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty;
        LevelNumber = Math.Max(1, level);

        OnPropertyChanged(nameof(DifficultyText));

        await RefreshCoinsAsync();
        await StartRoundAsync();
    }

    private async Task RefreshCoinsAsync()
    {
        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;
    }

    private void BuildShop()
    {
        ShopItems.Clear();
        ShopItems.Add(new ShopHint("digits", "🔢 Stellenanzahl", "Die Zahl hat x Stellen.", 10));
        ShopItems.Add(new ShopHint("parity", "⚖️ Gerade/Ungerade", "Sie ist gerade oder ungerade.", 12));
        ShopItems.Add(new ShopHint("divisible", "➗ Teilbar durch x", "Sie ist teilbar durch x.", 18));
        ShopItems.Add(new ShopHint("sumdigits", "➕ Quersumme", "Quersumme ist x.", 18));
        ShopItems.Add(new ShopHint("position", "📍 Ziffernposition", "Position Ziffer ist …", 20));
        ShopItems.Add(new ShopHint("contains", "🔎 Ziffer enthalten", "Die Zahl enthält die Ziffer.", 16));
        ShopItems.Add(new ShopHint("range", "📊 Zahlenbereich", "Die Zahl liegt zwischen A und B.", 15));
    }

    private async Task StartRoundAsync()
    {
        IsFinished = false;
        Lives = LivesMax;
        GuessText = "";
        CurrentHint = "";
        VisibleProperties.Clear();

        _currentSeed = StableHash($"{GameId}:{DifficultyKey}:{LevelNumber}");
        _secret = _hangmanService.GenerateSecretNumber(_currentSeed, DifficultyKey);
        BuildShop();

        // ALLE TRUE-Eigenschaften sofort anzeigen
        var allTrue = _hangmanService.GetAllTrueProperties(_secret);
        foreach (var p in allTrue)
        {
            VisibleProperties.Add(new NumberPropertyVM(p.Key, p.KidDescription, p.Examples));
        }

        // Range für Hinweise
        var (min, max) = MathHangmanDifficulty.Range(DifficultyKey);
        var rng = new Random(_currentSeed ^ 0x51A2);
        int width = Math.Max(15, (max - min) / 6);
        _rangeA = Math.Max(min, _secret - rng.Next(width / 2, width + 1));
        _rangeB = Math.Min(max, _secret + rng.Next(width / 2, width + 1));

        OnPropertyChanged(nameof(WrongCount));
        await Task.CompletedTask;
    }

    private async Task GuessAsync()
    {
        if (IsFinished) return;

        if (!int.TryParse(GuessText?.Trim(), out var g))
        {
            await _dialog.AlertAsync("Eingabe", "Bitte eine Zahl eingeben.");
            return;
        }

        if (g == _secret)
        {
            IsFinished = true;
            int reward = MathHangmanDifficulty.RewardCoins(DifficultyKey);

            var user = await _userService.GetUserAsync();
            if (user != null)
            {
                user.Coins += reward;
                await _userService.SaveUserAsync(user);
            }
            await RefreshCoinsAsync();
            await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);

            await _dialog.AlertAsync("Gewonnen! 🎉", $"Richtig! Die Zahl war {_secret}\n\n+{reward} Coins");

            LevelNumber++;
            await StartRoundAsync();
            return;
        }

        // Falsch
        Lives = Math.Max(0, Lives - 1);
        CurrentHint = g < _secret ? $"💡 {g} ist zu klein ⬆️" : $"💡 {g} ist zu groß ⬇️";

        if (Lives <= 0)
        {
            IsFinished = true;
            await _dialog.AlertAsync("Verloren 😢", $"Keine Leben mehr.\n\nDie Zahl war: {_secret}");
        }
    }

    private async Task BuyHintAsync()
    {
        if (IsFinished) return;

        // Erstelle Optionen mit Preisen
        var options = ShopItems.Select(h => $"{h.Title} ({PriceFor(h)} 💰)").ToArray();

        var choice = await Shell.Current.DisplayActionSheetAsync("💡 Hinweis kaufen", "Abbrechen", null, options);

        if (string.IsNullOrEmpty(choice) || choice == "Abbrechen") return;

        int idx = Array.IndexOf(options, choice);
        if (idx < 0 || idx >= ShopItems.Count) return;

        var hint = ShopItems[idx];
        int price = PriceFor(hint);

        var user = await _userService.GetUserAsync();
        if (user == null || user.Coins < price)
        {
            await _dialog.AlertAsync("Nicht genug Coins", $"Du brauchst {price} Coins.");
            return;
        }

        // Kaufen
        user.Coins -= price;
        await _userService.SaveUserAsync(user);
        await RefreshCoinsAsync();

        ApplyHint(hint);
    }

    private int PriceFor(ShopHint h)
    {
        var mul = MathHangmanDifficulty.PriceMultiplier(DifficultyKey);
        return (int)Math.Round(h.BasePrice * mul);
    }

    private void ApplyHint(ShopHint h)
    {
        CurrentHint = h.Id switch
        {
            "range" => $"📊 Die Zahl liegt zwischen {_rangeA} und {_rangeB}.",
            "digits" => $"🔢 Die Zahl hat {_secret.ToString().Length} Stellen.",
            "sumdigits" => $"➕ Quersumme ist {SumDigits(_secret)}.",
            "parity" => _secret % 2 == 0 ? "⚖️ Sie ist gerade." : "⚖️ Sie ist ungerade.",
            "divisible" => GetDivisibleHint(),
            "contains" => GetContainsDigitHint(),
            "position" => GetPositionDigitHint(),
            _ => "Hinweis"
        };
    }

    private string GetDivisibleHint()
    {
        int? divisor = PickDivisorHint();
        return divisor.HasValue
            ? $"➗ Sie ist teilbar durch {divisor.Value}."
            : $"📊 Die Zahl liegt zwischen {_rangeA} und {_rangeB}.";
    }

    private int? PickDivisorHint()
    {
        int[] candidates = { 2, 3, 4, 5, 6, 7, 8, 9, 11 };
        var possible = candidates.Where(d => _secret % d == 0).ToList();
        if (possible.Count == 0)
            return null;

        var rng = new Random(_currentSeed ^ _secret);
        return possible[rng.Next(possible.Count)];
    }

    private string GetContainsDigitHint()
    {
        var digits = _secret.ToString().Select(c => c - '0').Distinct().ToList();
        var rng = new Random(_currentSeed ^ 0x1234);
        int digit = digits[rng.Next(digits.Count)];
        return $"🔎 Die Zahl enthält die Ziffer {digit}.";
    }

    private string GetPositionDigitHint()
    {
        var s = _secret.ToString();
        var rng = new Random(_currentSeed ^ 0x7777);
        int index = rng.Next(s.Length);
        int position = index + 1;
        char digit = s[index];
        return $"📍 Position {position} ist die Ziffer {digit}.";
    }

    private static int SumDigits(int n) => Math.Abs(n).ToString().Sum(c => c - '0');

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (var c in s) { h ^= c; h *= 16777619; }
            return Math.Abs(h);
        }
    }
}
