// LogikSpiel/ViewModel/MathHangmanPageViewModel.cs
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
    private readonly IUserProfileService _userService;
    private readonly IMathHangmanService _hangmanService;
    private readonly IDialogService _dialog;

    public event Action? RequestNearMiss;

    public string GameId { get; private set; } = "math_hangman";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set
        {
            if (SetProperty(ref _levelNumber, value))
                OnPropertyChanged(nameof(DifficultyText));
        }
    }

    private int _rangeMin;
    public int RangeMin
    {
        get => _rangeMin;
        private set
        {
            if (SetProperty(ref _rangeMin, value))
                OnPropertyChanged(nameof(DifficultyText));
        }
    }

    private int _rangeMax;
    public int RangeMax
    {
        get => _rangeMax;
        private set
        {
            if (SetProperty(ref _rangeMax, value))
                OnPropertyChanged(nameof(DifficultyText));
        }
    }

    public string DifficultyText => $"Bereich: {RangeMin:N0} – {RangeMax:N0} (Lv {LevelNumber})";

    private int _coins;
    public int Coins { get => _coins; private set => SetProperty(ref _coins, value); }

    private int _lives = LivesMax;
    public int Lives { get => _lives; private set { if (SetProperty(ref _lives, value)) OnPropertyChanged(nameof(WrongCount)); } }
    public int WrongCount => LivesMax - Lives;

    private int _secret;
    private bool _isFinished;
    public bool IsFinished { get => _isFinished; private set => SetProperty(ref _isFinished, value); }

    private string _guessText = "";
    public string GuessText { get => _guessText; set => SetProperty(ref _guessText, value); }

    private string _currentHint = "";
    public string CurrentHint { get => _currentHint; private set { SetProperty(ref _currentHint, value); OnPropertyChanged(nameof(HasHint)); } }
    public bool HasHint => !string.IsNullOrWhiteSpace(CurrentHint);

    public ObservableCollection<NumberPropertyVM> VisibleProperties { get; } = new();
    public ObservableCollection<ShopHint> ShopItems { get; } = new();

    public AsyncCommand GuessCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand<NumberPropertyVM> ExplainPropertyCommand { get; }

    public MathHangmanPageViewModel(IUserProfileService userService, IMathHangmanService hangmanService, IDialogService dialog)
    {
        _userService = userService;
        _hangmanService = hangmanService;
        _dialog = dialog;

        GuessCommand = new AsyncCommand(GuessAsync);
        HintCommand = new AsyncCommand(BuyHintAsync);
        ExplainPropertyCommand = new AsyncCommand<NumberPropertyVM>(ExplainPropertyAsync);
        BuildShop();
    }

    private void BuildShop()
    {
        ShopItems.Clear();
        // Alle Hinweise kosten 10 Coins
        ShopItems.Add(new ShopHint("digits", "🔢 Stellenanzahl", "Anzahl der Ziffern", 10));
        ShopItems.Add(new ShopHint("sumdigits", "➕ Quersumme", "Summe aller Ziffern", 10));
        ShopItems.Add(new ShopHint("parity", "⚖️ Gerade/Ungerade", "Ist die Zahl durch 2 teilbar?", 10));
        ShopItems.Add(new ShopHint("contains", "🔎 Ziffer enthalten", "Welche Ziffer ist dabei?", 10));
    }

    public async Task LoadAsync(string gameId, string difficultyKey, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "math_hangman" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficultyKey) ? "normal" : difficultyKey;
        LevelNumber = Math.Max(1, level);

        await StartRoundAsync();
    }

    public async Task StartRoundAsync()
    {
        IsFinished = false;
        Lives = LivesMax;
        GuessText = "";
        CurrentHint = "";
        VisibleProperties.Clear();

        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;

        // "Nie wieder"-Logik: Generiere Zahl, bis sie neu ist
        int attempts = 0;
        do
        {
            _secret = _hangmanService.GenerateSecretNumber(DifficultyKey);
            attempts++;
            // Falls der User schon fast alle Zahlen (unwahrscheinlich) hat, brechen wir nach 100 Versuchen ab
        } while (user != null && user.UsedNumbers.Contains(_secret) && attempts < 100);

        (int minRange, int maxRange) = MathHangmanDifficulty.VisibleRange(_secret, LevelNumber);
        RangeMin = minRange;
        RangeMax = maxRange;

        var props = _hangmanService.GetAllTrueProperties(_secret);
        foreach (var p in props)
        {
            VisibleProperties.Add(new NumberPropertyVM(p.Key, p.KidDescription, p.Examples));
        }
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
            var user = await _userService.GetUserAsync();
            if (user != null)
            {
                user.Coins += 50;
                user.AddUsedNumber(_secret); // Hier wird die neue Methode genutzt
                await _userService.SaveUserAsync(user);
            }
            await _dialog.AlertAsync("Gewonnen! 🎉", $"Richtig! Die Zahl war {_secret}\n+50 Coins");
            await StartRoundAsync();
        }
        else
        {
            if (Math.Abs(g - _secret) == 1)
                RequestNearMiss?.Invoke();

            Lives = Math.Max(0, Lives - 1);
            CurrentHint = g < _secret ? "💡 Die Zahl ist GRÖSSER ⬆️" : "💡 Die Zahl ist KLEINER ⬇️";

            if (Lives <= 0)
            {
                IsFinished = true;
                await _dialog.AlertAsync("Verloren 😢", $"Die Zahl war: {_secret}");
                await StartRoundAsync();
            }
        }
    }

    private async Task BuyHintAsync()
    {
        if (IsFinished) return;

        if (Coins < 10)
        {
            await _dialog.AlertAsync("Nicht genug Coins", "Ein Hinweis kostet 10 Coins.");
            return;
        }

        var options = ShopItems.Select(h => $"{h.Title} (10 💰)").ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync("💡 Hinweis kaufen", "Abbrechen", null, options);

        if (string.IsNullOrEmpty(choice) || choice == "Abbrechen") return;

        var user = await _userService.GetUserAsync();
        if (user != null)
        {
            user.Coins -= 10;
            await _userService.SaveUserAsync(user);
            Coins = user.Coins;
        }

        // Hinweis-Logik
        if (choice.Contains("Stellenanzahl")) CurrentHint = $"🔢 Die Zahl hat {_secret.ToString().Length} Stellen.";
        else if (choice.Contains("Quersumme")) CurrentHint = $"➕ Die Quersumme ist {_secret.ToString().Sum(c => c - '0')}.";
        else if (choice.Contains("Gerade")) CurrentHint = _secret % 2 == 0 ? "⚖️ Sie ist gerade." : "⚖️ Sie ist ungerade.";
        else CurrentHint = "Hinweis gekauft!";
    }

    private async Task ExplainPropertyAsync(NumberPropertyVM? property)
    {
        if (property == null) return;

        string message = $"{property.KidDescription}\nBeispiele: {property.Examples}";
        await _dialog.AlertAsync(property.Key, message);
    }
}
