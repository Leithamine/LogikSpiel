// LogikSpiel/ViewModel/MathHangmanPageViewModel.cs
#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Model.MathHangman;
using LogikSpiel.Services;
using LogikSpiel.Services.MathHangman;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace LogikSpiel.ViewModel;

public sealed class MathHangmanPageViewModel : ObservableObject
{
    private const int LivesMax = 6;
    private readonly IUserProfileService _userService;
    private readonly IMathHangmanService _hangmanService;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

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

    public string DifficultyText => LocalizationService.Format("MathHangman_DifficultyRangeFormat", RangeMin, RangeMax);

    private string _quersummeText = "";
    public string QuersummeText { get => _quersummeText; private set => SetProperty(ref _quersummeText, value); }

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

    public ObservableCollection<string> PurchasedHints { get; } = new();
    public bool HasHints => PurchasedHints.Count > 0;

    public ObservableCollection<NumberPropertyVM> VisibleProperties { get; } = new();
    public ObservableCollection<ShopHintItemViewModel> ShopItems { get; } = new();

    public AsyncCommand BackCommand { get; }
    public AsyncCommand GuessCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand<NumberPropertyVM> ExplainPropertyCommand { get; }

    public MathHangmanPageViewModel(
        IUserProfileService userService,
        IMathHangmanService hangmanService,
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav)
    {
        _userService = userService;
        _hangmanService = hangmanService;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;

        BackCommand = new AsyncCommand(ConfirmBackAsync);
        GuessCommand = new AsyncCommand(GuessAsync);
        HintCommand = new AsyncCommand(BuyHintAsync);
        ExplainPropertyCommand = new AsyncCommand<NumberPropertyVM>(ExplainPropertyAsync);
        PurchasedHints.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasHints));
        BuildShop();
    }

    private void BuildShop()
    {
        ShopItems.Clear();

        // Neue Hinweise (alle 10 Coins)
        ShopItems.Add(new ShopHintItemViewModel("lastDigit",
            LocalizationService.GetString("MathHangman_ShopLastDigitTitle"),
            LocalizationService.GetString("MathHangman_ShopLastDigitDesc"), 10));
        ShopItems.Add(new ShopHintItemViewModel("firstDigit",
            LocalizationService.GetString("MathHangman_ShopFirstDigitTitle"),
            LocalizationService.GetString("MathHangman_ShopFirstDigitDesc"), 10));
        ShopItems.Add(new ShopHintItemViewModel("distinctCount",
            LocalizationService.GetString("MathHangman_ShopDistinctCountTitle"),
            LocalizationService.GetString("MathHangman_ShopDistinctCountDesc"), 10));
        ShopItems.Add(new ShopHintItemViewModel("hasDouble",
            LocalizationService.GetString("MathHangman_ShopHasDoubleTitle"),
            LocalizationService.GetString("MathHangman_ShopHasDoubleDesc"), 10));
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
        PurchasedHints.Clear();
        VisibleProperties.Clear();
        foreach (var item in ShopItems)
            item.IsPurchased = false;

        var user = await _userService.GetUserAsync();
        Coins = user?.Coins ?? 0;

        string secretKey = GetSecretKey();
        if (Preferences.ContainsKey(secretKey))
        {
            _secret = Preferences.Get(secretKey, MathHangmanDifficulty.MinSecret);
        }
        else
        {
            // "Nie wieder"-Logik: Generiere Zahl, bis sie neu ist
            int attempts = 0;
            do
            {
                _secret = _hangmanService.GenerateSecretNumber(DifficultyKey);
                attempts++;
                // Falls der User schon fast alle Zahlen (unwahrscheinlich) hat, brechen wir nach 100 Versuchen ab
            } while (user != null && user.UsedNumbers.Contains(_secret) && attempts < 100);
            Preferences.Set(secretKey, _secret);
        }

        (int minRange, int maxRange) = MathHangmanDifficulty.VisibleRange(_secret, LevelNumber);
        RangeMin = minRange;
        RangeMax = maxRange;

        int digitSum = _secret.ToString().Sum(c => c - '0');
        QuersummeText = LocalizationService.Format("MathHangman_DigitSumFormat", digitSum);

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
            await _dialog.AlertAsync(
                LocalizationService.GetString("MathHangman_InputTitle"),
                LocalizationService.GetString("MathHangman_InputMessage"));
            return;
        }

        if (g == _secret)
        {
            IsFinished = true;
            var user = await _userService.GetUserAsync();
            if (user != null)
            {
                int reward = MathHangmanDifficulty.RewardCoins(DifficultyKey);
                user.Coins += reward;
                user.AddUsedNumber(_secret); 
                await _userService.SaveUserAsync(user);
            }
            int completedLevel = LevelNumber;
            await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, completedLevel);
            Preferences.Remove(GetSecretKey());
            await _dialog.AlertAsync(
                LocalizationService.GetString("MathHangman_WinTitle"),
                LocalizationService.Format("MathHangman_WinMessageFormat", _secret));
            int nextLevel = completedLevel + 1;
            int maxLevel = 10000; // oder aus Config
            if (nextLevel > maxLevel)
            {
                await _dialog.AlertAsync(
                    LocalizationService.GetString("MathHangman_CompleteTitle"),
                    LocalizationService.GetString("MathHangman_CompleteMessage"),
                    LocalizationService.GetString("Common_Ok"));
                await _nav.GoToAsync(nameof(GameMapPage), new Dictionary<string, object> { ["gameId"] = GameId });
                return;
            }
            LevelNumber = nextLevel;
            await StartRoundAsync();
        }
        else
        {
            Lives = Math.Max(0, Lives - 1);

            if (Lives <= 0)
            {
                IsFinished = true;
                bool retry = await _dialog.ConfirmAsync(
                    LocalizationService.GetString("MathHangman_LoseTitle"),
                    LocalizationService.Format("MathHangman_LoseMessageFormat", _secret),
                    LocalizationService.GetString("Common_Retry"),
                    LocalizationService.GetString("Common_Back"));

                if (retry)
                {
                    await StartRoundAsync();
                    return;
                }

                var parameters = new Dictionary<string, object>
                {
                    ["gameId"] = GameId
                };

                await _nav.GoToAsync(nameof(GameMapPage), parameters);
            }
        }
    }

    private async Task BuyHintAsync()
    {
        if (IsFinished) return;

        if (Coins < 10)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("Common_NotEnoughCoinsTitle"),
                LocalizationService.GetString("MathHangman_NotEnoughCoinsMessage"));
            return;
        }

        if (ShopItems.All(item => item.IsPurchased))
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("MathHangman_HintsTitleDialog"),
                LocalizationService.GetString("MathHangman_HintsPurchasedMessage"));
            return;
        }

        var optionItems = ShopItems
            .Where(item => item.CanPurchase)
            .ToList();
        var optionMap = optionItems.ToDictionary(
            item => $"{item.Title} ({LocalizationService.Format("Common_PriceCoinsFormat", item.Price)})",
            item => item);
        var options = optionMap.Keys.ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync(
            LocalizationService.GetString("MathHangman_BuyHintSheetTitle"),
            LocalizationService.GetString("Common_Cancel"), null, options);

        if (string.IsNullOrEmpty(choice) || choice == LocalizationService.GetString("Common_Cancel")) return;

        if (!optionMap.TryGetValue(choice, out var selectedItem))
            return;
        if (selectedItem == null || selectedItem.IsPurchased) return;

        var user = await _userService.GetUserAsync();
        if (user != null)
        {
            user.Coins -= selectedItem.Price;
            await _userService.SaveUserAsync(user);
            Coins = user.Coins;
        }

        // Hinweis-Logik
        string s = _secret.ToString();

        string hintText = selectedItem.Id switch
        {
            "lastDigit" => LocalizationService.Format("MathHangman_HintLastDigitFormat", s[^1]),
            "firstDigit" => LocalizationService.Format("MathHangman_HintFirstDigitFormat", s[0]),
            "distinctCount" => LocalizationService.Format("MathHangman_HintDistinctCountFormat", s.Distinct().Count()),
            "hasDouble" => s.Length != s.Distinct().Count()
                                ? LocalizationService.GetString("MathHangman_HintHasDoubleTrue")
                                : LocalizationService.GetString("MathHangman_HintHasDoubleFalse"),
            _ => LocalizationService.GetString("MathHangman_HintNotAvailable")
        };

        selectedItem.IsPurchased = true;
        if (!PurchasedHints.Contains(hintText))
        {
            PurchasedHints.Add(hintText);
            OnPropertyChanged(nameof(HasHints));
        }
    }

    private async Task ExplainPropertyAsync(NumberPropertyVM? property)
    {
        if (property == null) return;

        string message = LocalizationService.Format("MathHangman_PropertyDetailsFormat", property.KidDescription, property.Examples);
        await _dialog.AlertAsync(property.Key, message);
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

    private string PickContainedDigit()
    {
        var digits = _secret.ToString().Distinct().ToArray();
        return digits.Length == 0 ? "0" : digits[Random.Shared.Next(digits.Length)].ToString();
    }

    private string GetSecretKey() => $"MATH_HANGMAN_SECRET_{GameId}_{DifficultyKey}_{LevelNumber}";
}
