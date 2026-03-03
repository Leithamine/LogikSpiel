#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.ViewModel;

public sealed class MathCrossPageViewModel : ObservableObject
{
    private const int HintCost = 10;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly MathCrossGeneratorService _generator;
    private readonly IGameCatalogService _catalog;

    public event Action? RequestLayoutUpdate;

    private UserProfile? _userProfile;
    private GameDefinition? _gameDefinition;

    private MathCrossGeneratorService.LayoutConstraints? _layoutConstraints;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public string GameId { get; private set; } = "math_cross";
    public string DifficultyKey { get; private set; } = "easy";

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
                OnPropertyChanged(nameof(HeaderSubtitle));
            }
        }
    }

    public string Title => LocalizationService.Format("MathCross_TitleFormat", DiffName(DifficultyKey), LevelNumber);
    public string LevelDisplayText => LocalizationService.Format("Common_LevelFormat", LevelNumber);
    public string DifficultyText => DiffName(DifficultyKey);
    public string HeaderSubtitle => $"{DifficultyText} - {LevelDisplayText}";

    private MathCrossGame? _game;
    public MathCrossGame? Game
    {
        get => _game;
        private set => SetProperty(ref _game, value);
    }

    // Feste Zellgröße
    public double CellSize => 42;

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

    public bool AllowNegativeInput => DifficultyKey is "hard" or "master";
    public bool AllowDecimalInput => DifficultyKey == "master";

    public ObservableCollection<MathCrossCellViewModel> FlatCells { get; } = new();
    public ObservableCollection<string> CandidateTokens { get; } = new();

    private MathCrossCellViewModel? _selectedCell;
    public MathCrossCellViewModel? SelectedCell
    {
        get => _selectedCell;
        set
        {
            if (_selectedCell == value) return;
            var old = _selectedCell;
            _selectedCell = value;
            old?.UpdateDisplay();
            _selectedCell?.UpdateDisplay();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            UpdateCandidateTokens();
            HintCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelection => SelectedCell != null;

    public AsyncCommand BackCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public AsyncCommand CheckCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand<string> TokenCommand { get; }
    public AsyncCommand ClearCellCommand { get; }

    public MathCrossPageViewModel(
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        MathCrossGeneratorService generator,
        IGameCatalogService catalog)
    {
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _generator = generator;
        _catalog = catalog;

        BackCommand = new AsyncCommand(ConfirmBackAsync);

        ResetCommand = new AsyncCommand(async () =>
        {
            if (Game == null) return;
            bool confirm = await _dialog.ConfirmAsync(
                LocalizationService.GetString("MathCross_ResetTitle"),
                LocalizationService.GetString("MathCross_ResetMessage"),
                LocalizationService.GetString("Common_Yes"),
                LocalizationService.GetString("Common_No"));
            if (!confirm) return;

            foreach (var vm in FlatCells)
            {
                if (!vm.IsGiven && vm.Cell.Type is CellType.Number or CellType.Operator)
                {
                    vm.Cell.UserInput = "";
                    vm.UpdateDisplay();
                }
            }
            SelectedCell = null;
        });

        CheckCommand = new AsyncCommand(CheckSolutionAsync);

        HintCommand = new AsyncCommand(async () =>
        {
            var candidate = SelectedCell;
            if (!CanUseHintOnSelectedCell() || candidate == null)
                return;

            if (Coins < HintCost)
            {
                await _dialog.AlertAsync(
                    LocalizationService.GetString("Common_NotEnoughCoinsTitle"),
                    LocalizationService.Format("Common_NeedCoinsFormat", HintCost));
                return;
            }

            bool buy = await _dialog.ConfirmAsync(
                LocalizationService.GetString("MathCross_BuyHintTitle"),
                LocalizationService.Format("MathCross_BuyHintMessage", HintCost));
            if (!buy) return;

            candidate.Cell.UserInput = candidate.Cell.Solution;
            candidate.Cell.IsGiven = true;
            candidate.UpdateDisplay();

            if (_userProfile != null)
            {
                _userProfile.Coins -= HintCost;
                Coins = _userProfile.Coins;
                await _userService.SaveUserAsync(_userProfile);
            }

            SelectedCell = null;
            UpdateCandidateTokens();
            HintCommand.RaiseCanExecuteChanged();
        }, CanUseHintOnSelectedCell);

        TokenCommand = new AsyncCommand<string>(async token =>
        {
            if (string.IsNullOrEmpty(token) || SelectedCell == null || !SelectedCell.IsEditable) return;

            SelectedCell.Cell.UserInput = token;
            SelectedCell.UpdateDisplay();

            var next = FlatCells
                .SkipWhile(c => c != SelectedCell)
                .Skip(1)
                .FirstOrDefault(c => c.IsEditable && string.IsNullOrWhiteSpace(c.Cell.UserInput));

            if (next != null)
            {
                ClearSelections();
                next.IsSelected = true;
                SelectedCell = next;
            }
            HintCommand.RaiseCanExecuteChanged();
            await Task.CompletedTask;
        });

        ClearCellCommand = new AsyncCommand(async () =>
        {
            if (SelectedCell == null || !SelectedCell.IsEditable) return;
            SelectedCell.Cell.UserInput = "";
            SelectedCell.UpdateDisplay();
            UpdateCandidateTokens();
            HintCommand.RaiseCanExecuteChanged();
            await Task.CompletedTask;
        });
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            GameId = string.IsNullOrWhiteSpace(gameId) ? "math_cross" : gameId;
            DifficultyKey = NormalizeDifficulty(difficulty);
            LevelNumber = Math.Max(1, level);
            _gameDefinition = await _catalog.GetGameAsync(GameId);

            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(DifficultyText));
            OnPropertyChanged(nameof(AllowNegativeInput));
            OnPropertyChanged(nameof(AllowDecimalInput));
            OnPropertyChanged(nameof(HeaderSubtitle));

            _userProfile = await _userService.GetUserAsync();
            Coins = _userProfile?.Coins ?? 0;

            await StartNewRoundAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }


    public async Task UpdateLayoutConstraintsAsync(int maxRows, int maxCols)
    {
        maxRows = Math.Clamp(maxRows, 5, 24);
        maxCols = Math.Clamp(maxCols, 5, 24);

        var next = new MathCrossGeneratorService.LayoutConstraints(maxRows, maxCols);
        if (_layoutConstraints.HasValue && _layoutConstraints.Value.Equals(next))
            return;

        _layoutConstraints = next;

        // Wichtig: Laufendes Rätsel nicht neu erzeugen, wenn sich das Layout
        // (z. B. durch Auswahl/Keyboard/Resize) leicht verändert.
        // Neue Constraints werden erst bei der nächsten Runde berücksichtigt.
        await Task.CompletedTask;
    }

    private async Task StartNewRoundAsync()
    {
        FlatCells.Clear();
        SelectedCell = null;
        CandidateTokens.Clear();

        int seed = StableHash($"{GameId}:{DifficultyKey}") + LevelNumber * 77;
        var constraints = _layoutConstraints;
        var game = await Task.Run(() => _generator.GenerateGame(DifficultyKey, seed, constraints));

        if (game == null || game.Rows == 0)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("MathCross_ErrorTitle"),
                LocalizationService.GetString("MathCross_ErrorGenerateMessage"));
            return;
        }

        Game = game;

        // Erstelle ViewModels nur für nicht-leere Zellen
        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                var cell = game.Grid[r, c];
                if (cell.Type != CellType.Empty)
                {
                    FlatCells.Add(new MathCrossCellViewModel(cell, this));
                }
            }
        }

        RequestLayoutUpdate?.Invoke();
    }

    public void OnCellTapped(MathCrossCellViewModel cell)
    {
        if (!cell.IsEditable)
        {
            SelectedCell = null;
            return;
        }
        ClearSelections();
        cell.IsSelected = true;
        SelectedCell = cell;
    }

    private void ClearSelections()
    {
        foreach (var c in FlatCells)
            if (c.IsSelected) c.IsSelected = false;
    }


    private bool CanUseHintOnSelectedCell()
    {
        if (SelectedCell == null || !SelectedCell.IsEditable) return false;

        var cell = SelectedCell.Cell;
        return string.IsNullOrWhiteSpace(cell.UserInput)
            && !IsCellSolved(cell)
            && !string.IsNullOrWhiteSpace(cell.Solution);
    }

    private void UpdateCandidateTokens()
    {
        CandidateTokens.Clear();
        if (SelectedCell == null || !SelectedCell.IsEditable) return;

        var cell = SelectedCell.Cell;

        if (cell.Type == CellType.Number)
        {
            foreach (var t in BuildNumberCandidates(cell.Solution))
                CandidateTokens.Add(t);
        }
        else if (cell.Type == CellType.Operator)
        {
            foreach (var t in BuildOperatorCandidates())
                CandidateTokens.Add(t);
        }
    }

    private IEnumerable<string> BuildOperatorCandidates()
    {
        return DifficultyKey.ToLowerInvariant() switch
        {
            "easy" => new[] { "+", "−" },
            "normal" => new[] { "+", "−", "×" },
            _ => new[] { "+", "−", "×", "÷" }
        };
    }

    private IEnumerable<string> BuildNumberCandidates(string? solution)
    {
        string sol = (solution ?? "").Trim();
        var set = new HashSet<string> { sol };

        int minVal = DifficultyKey is "hard" or "master" ? -100 : 1;
        int maxVal = 100;

        if (TryParse(sol, out var sNum))
        {
            var rnd = Random.Shared;
            while (set.Count < 10)
            {
                double v = sNum + rnd.Next(-15, 16);
                v = Math.Max(minVal, Math.Min(maxVal, v));

                string cand;
                if (DifficultyKey == "master" && rnd.Next(10) < 2)
                {
                    double dec = Math.Round(v + rnd.NextDouble() - 0.5, 1);
                    dec = Math.Max(minVal, Math.Min(maxVal, dec));
                    cand = dec.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    cand = ((int)Math.Round(v)).ToString();
                }
                set.Add(cand.Replace('-', '−'));
            }
        }
        else
        {
            foreach (var p in Enumerable.Range(minVal, maxVal - minVal + 1)
                .Select(n => n.ToString().Replace('-', '−'))
                .OrderBy(_ => Random.Shared.Next())
                .Take(10))
            {
                set.Add(p);
            }
        }

        return set.OrderBy(v => TryParse(v, out var n) ? n : double.MaxValue).Take(10);
    }

    private async Task CheckSolutionAsync()
    {
        if (Game == null) return;

        var missing = FlatCells.Where(vm => vm.IsEditable && string.IsNullOrWhiteSpace(vm.Cell.UserInput)).ToList();
        if (missing.Count > 0)
        {
            SelectedCell = missing[0];
            await _dialog.AlertAsync(
                LocalizationService.GetString("MathCross_MissingTitle"),
                LocalizationService.Format("MathCross_MissingMessageFormat", missing.Count));
            return;
        }

        foreach (var vm in FlatCells)
        {
            var cell = vm.Cell;
            if (cell.Type is CellType.Empty or CellType.Equals || cell.IsGiven) continue;

            bool ok = cell.Type switch
            {
                CellType.Number => SameNumber(cell.UserInput, cell.Solution),
                CellType.Operator => SameOp(cell.UserInput, cell.Solution),
                _ => true
            };

            if (!ok)
            {
                ClearSelections();
                vm.IsSelected = true;
                SelectedCell = vm;
                await _dialog.AlertAsync(
                    LocalizationService.GetString("MathCross_WrongTitle"),
                    LocalizationService.GetString("MathCross_WrongMessage"));
                return;
            }
        }

        int reward = DifficultyKey switch { "easy" => 3, "normal" => 5, "hard" => 7, "master" => 10, _ => 5 };

        if (_userProfile != null)
        {
            _userProfile.Coins += reward;
            Coins = _userProfile.Coins;
            await _userService.SaveUserAsync(_userProfile);
        }

        await _dialog.AlertAsync(
            LocalizationService.GetString("MathCross_SuccessTitle"),
            LocalizationService.Format("Common_CoinsRewardFormat", reward));
        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);

        int maxLevel = _gameDefinition?.LevelCount ?? 100;
        if (LevelNumber >= maxLevel)
        {
            await _dialog.AlertAsync(
                LocalizationService.GetString("MathCross_CompleteTitle"),
                LocalizationService.GetString("MathCross_CompleteMessage"),
                LocalizationService.GetString("Common_Ok"));
            await _nav.GoToAsync(nameof(GameMapPage), new Dictionary<string, object> { ["gameId"] = GameId });
            return;
        }

        LevelNumber++;
        await StartNewRoundAsync();
    }

    private bool IsCellSolved(MathCrossCell cell)
    {
        if (cell.Type is CellType.Empty or CellType.Equals) return true;
        if (cell.Type == CellType.Number) return SameNumber(cell.UserInput, cell.Solution);
        if (cell.Type == CellType.Operator) return SameOp(cell.UserInput, cell.Solution);
        return false;
    }

    private static bool SameOp(string? u, string? s) =>
        MathCrossValueNormalizer.NormalizeOperator(u) == MathCrossValueNormalizer.NormalizeOperator(s);

    private bool SameNumber(string? u, string? s)
    {
        return MathCrossValueNormalizer.AreNumbersEqual(u, s, allowDecimals: DifficultyKey == "master");
    }

    private static bool TryParse(string? s, out double r)
    {
        return MathCrossValueNormalizer.TryParseNumber(s, out r);
    }

    private static string NormalizeDifficulty(string? difficulty)
    {
        var value = (difficulty ?? "easy").Trim().ToLowerInvariant();
        return value is "easy" or "normal" or "hard" or "master" ? value : "easy";
    }

    private static string DiffName(string k) => LocalizationService.GetDifficultyLabel(k);

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (var c in s) { h ^= c; h *= 16777619; }
            return Math.Abs(h);
        }
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
}

public sealed class MathCrossCellViewModel : ObservableObject
{
    public MathCrossCell Cell { get; }
    private readonly MathCrossPageViewModel _parent;

    public MathCrossCellViewModel(MathCrossCell cell, MathCrossPageViewModel parent)
    {
        Cell = cell;
        _parent = parent;
        TapCellCommand = new AsyncCommand(() => { _parent.OnCellTapped(this); return Task.CompletedTask; });
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
                return;

            OnPropertyChanged(nameof(BorderStroke));
            OnPropertyChanged(nameof(BorderThickness));
            OnPropertyChanged(nameof(FocusGlow));
        }
    }

    public bool IsGiven => Cell.IsGiven;
    public bool IsEditable => !Cell.IsGiven && Cell.Type is CellType.Number or CellType.Operator;

    public string DisplayText => Cell.Type == CellType.Equals ? "=" : Cell.IsGiven ? Cell.Solution : Cell.UserInput;

    public string EditableText => !IsEditable ? DisplayText :
        Cell.Type == CellType.Operator ?
            (string.IsNullOrWhiteSpace(Cell.UserInput) ? "?" : Cell.UserInput) :
            (string.IsNullOrWhiteSpace(Cell.UserInput) ? "·" : Cell.UserInput);


    public Brush BorderStroke => new SolidColorBrush(ResolveBorderColor());

    public double BorderThickness => IsSelected ? 2.5 : 1;

    public Shadow? FocusGlow => IsSelected
        ? new Shadow
        {
            Brush = new SolidColorBrush(ResolveGlowColor()),
            Offset = new Point(0, 0),
            Radius = 14,
            Opacity = 1
        }
        : null;

    public AsyncCommand TapCellCommand { get; }

    private Color ResolveBorderColor()
    {
        if (Cell.IsGiven || Cell.Type == CellType.Equals)
            return GetColor("C_MathCell_Fixed_Border", "#646B76");

        if (IsSelected)
        {
            return Cell.Type switch
            {
                CellType.Number => GetColor("C_MathCell_Num_HoverBorder", "#79BCEB"),
                CellType.Operator => GetColor("C_MathCell_Op_HoverBorder", "#B2A4F0"),
                _ => GetColor("C_MathCell_Fixed_Border", "#646B76")
            };
        }

        return Cell.Type switch
        {
            CellType.Number => GetColor("C_MathCell_Num_Border", "#5EA6D8"),
            CellType.Operator => GetColor("C_MathCell_Op_Border", "#9A8BE0"),
            _ => Colors.Transparent
        };
    }

    private Color ResolveGlowColor()
    {
        return Cell.Type switch
        {
            CellType.Number => GetColor("C_MathCell_Num_Glow", "#595EA6D8"),
            CellType.Operator => GetColor("C_MathCell_Op_Glow", "#599A8BE0"),
            _ => Colors.Transparent
        };
    }

    private static Color GetColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue(key, out var resource)
            && resource is Color color)
        {
            return color;
        }

        return Color.FromArgb(fallbackHex);
    }

    public void UpdateDisplay()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(EditableText));
        OnPropertyChanged(nameof(IsGiven));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(BorderStroke));
        OnPropertyChanged(nameof(BorderThickness));
        OnPropertyChanged(nameof(FocusGlow));
    }
}
