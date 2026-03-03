#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly Stack<PlacementMove> _undoStack = new();
    private readonly HashSet<string> _satisfiedEquationCellKeys = new();

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
    public MathCrossGame? Game { get => _game; private set => SetProperty(ref _game, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsNotBusy)); } }
    public bool IsNotBusy => !IsBusy;

    public bool AllowNegativeInput => DifficultyKey is "normal" or "hard" or "master";
    public bool AllowDecimalInput => DifficultyKey == "master";

    public ObservableCollection<MathCrossCellViewModel> FlatCells { get; } = new();
    public ObservableCollection<NumberBankTileViewModel> NumberBank { get; } = new();

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
            OnPropertyChanged(nameof(CanUseLamp));
            HintCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isDragging;
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (!SetProperty(ref _isDragging, value)) return;
            foreach (var cell in FlatCells) cell.UpdateDisplay();
        }
    }

    public bool HasSelection => SelectedCell != null;
    public bool CanUseLamp => CanUseHintOnSelectedCell();

    public AsyncCommand BackCommand { get; }
    public AsyncCommand HintCommand { get; }
    public AsyncCommand UndoCommand { get; }
    public AsyncCommand<NumberBankTileViewModel> NumberTileTappedCommand { get; }

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
        UndoCommand = new AsyncCommand(UndoAsync, () => _undoStack.Count > 0);
        HintCommand = new AsyncCommand(UseHintAsync, CanUseHintOnSelectedCell);

        NumberTileTappedCommand = new AsyncCommand<NumberBankTileViewModel>(async tile =>
        {
            if (tile == null || SelectedCell == null) return;
            if (TryPlaceTileOnCell(tile, SelectedCell.Cell, isHint: false))
            {
                SelectedCell = null;
                await CheckForAutoCompleteAsync();
            }
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
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(AllowNegativeInput));
            OnPropertyChanged(nameof(AllowDecimalInput));

            _userProfile = await _userService.GetUserAsync();
            Coins = _userProfile?.Coins ?? 0;

            await StartNewRoundAsync();
        }
        finally { IsBusy = false; }
    }

    public async Task UpdateLayoutConstraintsAsync(int maxRows, int maxCols)
    {
        maxRows = Math.Clamp(maxRows, 5, 24);
        maxCols = Math.Clamp(maxCols, 5, 24);

        var next = new MathCrossGeneratorService.LayoutConstraints(maxRows, maxCols);
        if (_layoutConstraints.HasValue && _layoutConstraints.Value.Equals(next))
            return;

        _layoutConstraints = next;
        await Task.CompletedTask;
    }

    private async Task StartNewRoundAsync()
    {
        FlatCells.Clear();
        NumberBank.Clear();
        _undoStack.Clear();
        UndoCommand.RaiseCanExecuteChanged();
        SelectedCell = null;

        int seed = StableHash($"{GameId}:{DifficultyKey}") + LevelNumber * 77;
        var game = await Task.Run(() => _generator.GenerateGame(DifficultyKey, seed, _layoutConstraints));
        if (game == null || game.Rows == 0)
        {
            await _dialog.AlertAsync(LocalizationService.GetString("MathCross_ErrorTitle"), LocalizationService.GetString("MathCross_ErrorGenerateMessage"));
            return;
        }

        Game = game;

        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type != CellType.Empty)
                    FlatCells.Add(new MathCrossCellViewModel(game.Grid[r, c], this));

        foreach (var tile in game.NumberBank)
            NumberBank.Add(new NumberBankTileViewModel(tile));

        RefreshEquationHighlights();
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

    public bool TryPlaceTileById(string? tileId, MathCrossCellViewModel? targetVm)
    {
        if (string.IsNullOrWhiteSpace(tileId) || targetVm == null) return false;
        var tile = NumberBank.FirstOrDefault(t => t.Tile.Id == tileId);
        if (tile == null) return false;
        if (!TryPlaceTileOnCell(tile, targetVm.Cell, isHint: false)) return false;
        RefreshEquationHighlights();
        _ = CheckForAutoCompleteAsync();
        return true;
    }

    public void SetDragging(bool isDragging) => IsDragging = isDragging;

    private bool TryPlaceTileOnCell(NumberBankTileViewModel tileVm, MathCrossCell cell, bool isHint)
    {
        if (cell.Type != CellType.Number || cell.IsGiven)
            return false;

        NumberBank.Remove(tileVm);

        var oldInput = cell.UserInput;
        var oldTileId = cell.PlacedTileId;

        if (!string.IsNullOrWhiteSpace(oldTileId))
        {
            NumberBank.Insert(0, new NumberBankTileViewModel(new NumberBankTile { Id = oldTileId, Value = oldInput }));
        }

        cell.UserInput = tileVm.Tile.Value;
        cell.PlacedTileId = tileVm.Tile.Id;

        if (isHint)
        {
            cell.IsGiven = true;
            cell.IsHintGiven = true;
            cell.PlacedTileId = null;
        }
        else
        {
            _undoStack.Push(new PlacementMove(cell, oldInput, oldTileId, tileVm.Tile.Value, tileVm.Tile.Id));
            UndoCommand.RaiseCanExecuteChanged();
        }

        RefreshEquationHighlights();
        FlatCells.FirstOrDefault(f => f.Cell == cell)?.UpdateDisplay();
        return true;
    }

    private async Task UseHintAsync()
    {
        var candidate = SelectedCell;
        if (!CanUseHintOnSelectedCell() || candidate == null)
            return;

        if (Coins < HintCost)
        {
            await _dialog.AlertAsync(LocalizationService.GetString("Common_NotEnoughCoinsTitle"), LocalizationService.Format("Common_NeedCoinsFormat", HintCost));
            return;
        }

        bool buy = await _dialog.ConfirmAsync(LocalizationService.GetString("MathCross_BuyHintTitle"), LocalizationService.Format("MathCross_BuyHintMessage", HintCost));
        if (!buy) return;

        var matchingTile = NumberBank.FirstOrDefault(t => MathCrossValueNormalizer.AreNumbersEqual(t.Tile.Value, candidate.Cell.Solution, allowDecimals: DifficultyKey == "master"));
        if (matchingTile != null)
            TryPlaceTileOnCell(matchingTile, candidate.Cell, isHint: true);
        else
        {
            candidate.Cell.UserInput = candidate.Cell.Solution;
            candidate.Cell.IsGiven = true;
            candidate.Cell.IsHintGiven = true;
            candidate.UpdateDisplay();
        }

        if (_userProfile != null)
        {
            _userProfile.Coins -= HintCost;
            Coins = _userProfile.Coins;
            await _userService.SaveUserAsync(_userProfile);
        }

        SelectedCell = null;
        await CheckForAutoCompleteAsync();
    }

    private bool CanUseHintOnSelectedCell()
    {
        if (SelectedCell == null || !SelectedCell.IsEditable) return false;
        return string.IsNullOrWhiteSpace(SelectedCell.Cell.UserInput);
    }

    private async Task UndoAsync()
    {
        if (_undoStack.Count == 0) return;
        var move = _undoStack.Pop();
        UndoCommand.RaiseCanExecuteChanged();

        var cell = move.Cell;
        if (!string.IsNullOrWhiteSpace(move.NewTileId))
        {
            var existing = NumberBank.FirstOrDefault(t => t.Tile.Id == move.NewTileId);
            if (existing == null)
                NumberBank.Insert(0, new NumberBankTileViewModel(new NumberBankTile { Id = move.NewTileId!, Value = move.NewInput }));
        }

        if (!string.IsNullOrWhiteSpace(move.OldTileId))
        {
            var oldTile = NumberBank.FirstOrDefault(t => t.Tile.Id == move.OldTileId);
            if (oldTile != null) NumberBank.Remove(oldTile);
        }

        cell.UserInput = move.OldInput;
        cell.PlacedTileId = move.OldTileId;
        RefreshEquationHighlights();
        FlatCells.First(f => f.Cell == cell).UpdateDisplay();
        await Task.CompletedTask;
    }

    private void RefreshEquationHighlights()
    {
        _satisfiedEquationCellKeys.Clear();
        if (Game == null) return;

        foreach (var eq in Game.Equations)
        {
            if (!IsEquationSatisfied(eq, DifficultyKey == "master"))
                continue;

            foreach (var (r, c) in eq.Cells)
                _satisfiedEquationCellKeys.Add(BuildCellKey(r, c));
        }

        foreach (var vm in FlatCells)
            vm.UpdateDisplay();
    }

    public bool IsCellInSatisfiedEquation(MathCrossCell cell)
        => _satisfiedEquationCellKeys.Contains(BuildCellKey(cell.Row, cell.Col));

    private bool IsEquationSatisfied(MathEquation eq, bool allowDecimals)
    {
        if (Game == null || eq.Cells.Count < 5)
            return false;

        int equalsIndex = eq.Cells.Count - 2;
        var numbers = new List<decimal>();
        var ops = new List<string>();

        for (int i = 0; i < eq.Cells.Count; i++)
        {
            var (r, c) = eq.Cells[i];
            var cell = Game.Grid[r, c];

            if (i == equalsIndex)
                continue;

            if (i % 2 == 0)
            {
                string raw = cell.IsGiven ? cell.Solution : cell.UserInput;
                if (!decimal.TryParse(MathCrossValueNormalizer.NormalizeNumberText(raw),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var v))
                    return false;

                numbers.Add(v);
            }
            else
            {
                ops.Add(MathCrossValueNormalizer.NormalizeOperator(cell.Solution));
            }
        }

        if (numbers.Count < 2 || ops.Count == 0)
            return false;

        decimal result = numbers[^1];
        var lhsNums = numbers.Take(numbers.Count - 1).ToList();
        var lhsOps = ops.ToList();

        var evaluated = EvaluateExpression(lhsNums, lhsOps, allowDecimals);
        if (evaluated == null)
            return false;

        if (allowDecimals)
            return Math.Abs((double)(evaluated.Value - result)) <= 0.0001;

        return evaluated.Value == result;
    }

    private static decimal? EvaluateExpression(List<decimal> nums, List<string> ops, bool allowDecimals)
    {
        if (nums.Count == 0) return null;

        foreach (var priority in new[] { 3, 2, 1 })
        {
            int i = 0;
            while (i < ops.Count)
            {
                if (GetPriority(ops[i]) != priority)
                {
                    i++;
                    continue;
                }

                var calc = Calc(nums[i], ops[i], nums[i + 1], allowDecimals);
                if (calc == null) return null;

                nums[i] = calc.Value;
                nums.RemoveAt(i + 1);
                ops.RemoveAt(i);
            }
        }

        return nums.Count == 1 ? nums[0] : null;
    }

    private static int GetPriority(string op)
        => op switch
        {
            "^" => 3,
            "×" or "÷" or "%" or "//" => 2,
            _ => 1
        };

    private static decimal? Calc(decimal a, string op, decimal b, bool allowDecimals)
    {
        return op switch
        {
            "+" => a + b,
            "-" or "−" => a - b,
            "×" => a * b,
            "÷" when b != 0 => allowDecimals ? a / b : (a % b == 0 ? a / b : null),
            "%" when b != 0 && a == decimal.Truncate(a) && b == decimal.Truncate(b) => a % b,
            "//" when b != 0 && a == decimal.Truncate(a) && b == decimal.Truncate(b) => decimal.Truncate(a / b),
            "^" when b == decimal.Truncate(b) && b >= 0 => (decimal)Math.Pow((double)a, (double)b),
            _ => null
        };
    }

    private static string BuildCellKey(int row, int col) => $"{row}:{col}";

    private async Task CheckForAutoCompleteAsync()
    {
        if (Game == null) return;
        var editable = FlatCells.Where(c => c.Cell.Type == CellType.Number && !c.Cell.IsGiven).ToList();
        if (editable.Any(c => string.IsNullOrWhiteSpace(c.Cell.UserInput))) return;

        if (editable.Any(c => !MathCrossValueNormalizer.AreNumbersEqual(c.Cell.UserInput, c.Cell.Solution, DifficultyKey == "master")))
            return;

        int reward = DifficultyKey switch { "easy" => 3, "normal" => 5, "hard" => 7, "master" => 10, _ => 5 };
        if (_userProfile != null)
        {
            _userProfile.Coins += reward;
            Coins = _userProfile.Coins;
            await _userService.SaveUserAsync(_userProfile);
        }

        await _dialog.AlertAsync(LocalizationService.GetString("MathCross_SuccessTitle"), LocalizationService.Format("Common_CoinsRewardFormat", reward));
        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);

        int maxLevel = _gameDefinition?.LevelCount ?? 100;
        if (LevelNumber >= maxLevel)
        {
            await _dialog.AlertAsync(LocalizationService.GetString("MathCross_CompleteTitle"), LocalizationService.GetString("MathCross_CompleteMessage"), LocalizationService.GetString("Common_Ok"));
            await _nav.GoToAsync(nameof(GameMapPage), new Dictionary<string, object> { ["gameId"] = GameId });
            return;
        }

        LevelNumber++;
        await StartNewRoundAsync();
    }

    private void ClearSelections()
    {
        foreach (var c in FlatCells)
            if (c.IsSelected) c.IsSelected = false;
    }

    private static string NormalizeDifficulty(string? difficulty)
    {
        var value = (difficulty ?? "easy").Trim().ToLowerInvariant();
        return value is "easy" or "normal" or "hard" or "master" ? value : "easy";
    }

    private static string DiffName(string k) => LocalizationService.GetDifficultyLabel(k);

    private static int StableHash(string s)
    {
        unchecked { int h = (int)2166136261; foreach (var c in s) { h ^= c; h *= 16777619; } return Math.Abs(h); }
    }

    private async Task ConfirmBackAsync()
    {
        bool leave = await _dialog.ConfirmAsync(LocalizationService.GetString("Common_Back"), LocalizationService.GetString("Common_LeavePuzzlePrompt"), LocalizationService.GetString("Common_Yes"), LocalizationService.GetString("Common_No"));
        if (!leave) return;
        await _nav.GoToAsync(nameof(GameMapPage), new Dictionary<string, object> { ["gameId"] = GameId });
    }

    private sealed record PlacementMove(MathCrossCell Cell, string OldInput, string? OldTileId, string NewInput, string? NewTileId);
}

public sealed class NumberBankTileViewModel : ObservableObject
{
    public NumberBankTile Tile { get; }
    public NumberBankTileViewModel(NumberBankTile tile) => Tile = tile;
    public string DisplayValue => Tile.Value;
    public double TileWidth => Math.Max(64, 28 + DisplayValue.Length * 16);
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
            if (!SetProperty(ref _isSelected, value)) return;
            OnPropertyChanged(nameof(BorderStroke));
            OnPropertyChanged(nameof(BorderThickness));
            OnPropertyChanged(nameof(FocusGlow));
        }
    }

    public bool IsGiven => Cell.IsGiven;
    public bool IsEditable => !Cell.IsGiven && Cell.Type == CellType.Number;
    public bool IsDropTarget => _parent.IsDragging && IsEditable;
    public bool IsEquationSatisfied => _parent.IsCellInSatisfiedEquation(Cell);

    public string DisplayText => Cell.Type == CellType.Equals || Cell.Type == CellType.Operator
        ? Cell.Solution
        : Cell.IsGiven ? Cell.Solution : (string.IsNullOrWhiteSpace(Cell.UserInput) ? "" : Cell.UserInput);

    public string EditableText => !IsEditable ? DisplayText : (string.IsNullOrWhiteSpace(Cell.UserInput) ? "" : Cell.UserInput);

    public Color CellBackground => ResolveBackgroundColor();

    public Brush BorderStroke => new SolidColorBrush(ResolveBorderColor());
    public double BorderThickness => IsSelected || IsDropTarget ? 2.5 : 1;

    public Shadow? FocusGlow => (IsSelected || IsEquationSatisfied)
        ? new Shadow { Brush = new SolidColorBrush(ResolveGlowColor()), Offset = new Point(0, 0), Radius = IsEquationSatisfied ? 16 : 14, Opacity = 1 }
        : null;

    public AsyncCommand TapCellCommand { get; }

    private Color ResolveBorderColor()
    {
        if (IsEquationSatisfied)
            return GetColor("C_Success", "#42C67A");

        if (IsDropTarget) return GetColor("C_MathCell_Num_HoverBorder", "#79BCEB");
        if (Cell.IsGiven || Cell.Type == CellType.Equals || Cell.Type == CellType.Operator)
            return GetColor("C_MathCell_Fixed_Border", "#646B76");
        if (IsSelected) return GetColor("C_MathCell_Num_HoverBorder", "#79BCEB");
        return GetColor("C_MathCell_Num_Border", "#5EA6D8");
    }

    private Color ResolveGlowColor()
    {
        if (IsEquationSatisfied)
            return GetColor("C_Success", "#42C67A");

        return GetColor("C_MathCell_Num_Glow", "#595EA6D8");
    }

    private Color ResolveBackgroundColor()
    {
        if (IsEquationSatisfied)
            return GetColor("C_Success", "#42C67A").WithAlpha(0.28f);

        if (Cell.Type == CellType.Empty)
            return Colors.Transparent;

        if (Cell.IsGiven || Cell.Type == CellType.Equals)
            return GetColor("C_MathCell_Fixed_Bg", "#313B4A");

        return Cell.Type switch
        {
            CellType.Number => GetColor("C_MathCell_Num_Bg", "#23445E"),
            CellType.Operator => GetColor("C_MathCell_Op_Bg", "#3A3556"),
            _ => Colors.Transparent
        };
    }

    private static Color GetColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue(key, out var resource)
            && resource is Color color)
            return color;

        return Color.FromArgb(fallbackHex);
    }

    public void UpdateDisplay()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(EditableText));
        OnPropertyChanged(nameof(IsGiven));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(IsDropTarget));
        OnPropertyChanged(nameof(IsEquationSatisfied));
        OnPropertyChanged(nameof(CellBackground));
        OnPropertyChanged(nameof(BorderStroke));
        OnPropertyChanged(nameof(BorderThickness));
        OnPropertyChanged(nameof(FocusGlow));
    }
}
