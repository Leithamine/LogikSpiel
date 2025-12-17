using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class PuzzlePageViewModel : ObservableObject, IQueryAttributable
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly LockRiddleGeneratorService _riddleGenerator;

    // --- User & Coins ---
    private UserProfile? _userProfile;
    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    // --- Spiel Status ---
    public string GameId { get; private set; } = "riddle_lock";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set { if (SetProperty(ref _levelNumber, value)) OnPropertyChanged(nameof(Title)); }
    }

    public string Title => $"Code Knacker – {DiffName(DifficultyKey)}";

    // --- Listen für das UI ---
    public ObservableCollection<DigitInputViewModel> InputDigits { get; } = new();
    public ObservableCollection<LockHint> Hints { get; } = new();

    private string _secretSolution = "";

    // --- Commands ---
    public AsyncCommand BackCommand { get; }
    public AsyncCommand CheckCommand { get; }
    public AsyncCommand HintCommand { get; }

    public PuzzlePageViewModel(
        IGameCatalogService catalog,
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        LockRiddleGeneratorService riddleGenerator)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _riddleGenerator = riddleGenerator;

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

    // --- WIEDER HINZUGEFÜGT: LoadAsync für Kompatibilität ---
    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = gameId;
        DifficultyKey = difficulty;
        LevelNumber = Math.Max(1, level);
        
        OnPropertyChanged(nameof(Title));

        // Spiel starten
        StartNewRound();
        try
        {
            // User laden
            _userProfile = await _userService.GetUserAsync();
            if (_userProfile != null) Coins = _userProfile.Coins;
        }
        catch( Exception ex)
        {
            // Fehler beim Laden des Users abfangen, damit das Spiel nicht abstürzt
            System.Diagnostics.Debug.WriteLine($"Fehler beim User-Laden: {ex.Message}");
        }

    }

    // Für Navigation via Shell Parameter (optional, falls du beides nutzt)
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        string diff = "normal";
        int lvl = 1;

        if (query.TryGetValue("difficulty", out var diffObj)) diff = diffObj.ToString() ?? "normal";
        if (query.TryGetValue("level", out var levelObj) && int.TryParse(levelObj.ToString(), out int l)) lvl = l;

        // Ruft einfach LoadAsync auf -> löst das Problem elegant
        Task.Run(() => LoadAsync("riddle_lock", diff, lvl));
    }

    private void StartNewRound()
    {
        int difficultyLevel = MapDifficultyToInt(DifficultyKey);

        // Generator aufrufen
        var riddleGame = _riddleGenerator.GenerateGame(difficultyLevel);
        _secretSolution = riddleGame.SecretCode;

        // UI Updates MÜSSEN auf dem MainThread passieren
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Hints.Clear();
            foreach (var hint in riddleGame.Hints) Hints.Add(hint);

            InputDigits.Clear();
            for (int i = 0; i < _secretSolution.Length; i++)
            {
                InputDigits.Add(new DigitInputViewModel());
            }
        });
    }

    private async Task CheckSolutionAsync()
    {
        string input = string.Join("", InputDigits.Select(d => d.Digit));

        if (input.Length != _secretSolution.Length || input.Contains(' ') || string.IsNullOrEmpty(input))
        {
            await _dialog.AlertAsync("Unvollständig", "Bitte fülle alle Felder aus.");
            return;
        }

        if (input == _secretSolution)
        {
            // Gewonnen!
            int reward = MapDifficultyToInt(DifficultyKey) * 2;
            if (_userProfile != null)
            {
                _userProfile.Coins += reward;
                Coins = _userProfile.Coins;
                await _userService.SaveUserAsync(_userProfile);
            }

            await _dialog.AlertAsync("KORREKT! 🎉", $"Code geknackt!\nDu erhältst {reward} Coins.");

            // Nächstes Level
            LevelNumber++;
            StartNewRound();
            // Fortschritt speichern (optional)
            await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber - 1);
        }
        else
        {
            await _dialog.AlertAsync("Falsch ❌", "Code stimmt nicht. Prüfe die Hinweise!");
        }
    }

    private void RevealOneDigit()
    {
        for (int i = 0; i < _secretSolution.Length; i++)
        {
            if (InputDigits[i].Digit != _secretSolution[i].ToString())
            {
                InputDigits[i].Digit = _secretSolution[i].ToString();
                InputDigits[i].IsLocked = true;

                if (_userProfile != null)
                {
                    _userProfile.Coins -= 10;
                    Coins = _userProfile.Coins;
                    _userService.SaveUserAsync(_userProfile);
                }
                return;
            }
        }
    }

    private int MapDifficultyToInt(string key) => key.ToLowerInvariant() switch
    {
        "easy" => 3,     // Einfach = 3 Zahlen
        "normal" => 4,   // Normal = 4 Zahlen
        "hard" => 5,     // Schwer = 5 Zahlen
        "master" => 6,   // Master = 6 Zahlen
        _ => 4
    };

    private static string DiffName(string key) => key switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => key
    };
}

public class DigitInputViewModel : ObservableObject
{
    private string _digit = "";
    public string Digit { get => _digit; set => SetProperty(ref _digit, value); }

    private bool _isLocked;
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
}