#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class MathCrossPageViewModel : ObservableObject
{
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly MathCrossGeneratorService _generator;

    private UserProfile? _userProfile;

    public ObservableCollection<MathCrossCellViewModel> FlatCells { get; } = new();
    public ObservableCollection<ObservableCollection<MathCrossCellViewModel>> GridCells { get; } = new();

    private double _cellSize = 40;
    public double CellSize { get => _cellSize; set => SetProperty(ref _cellSize, value); }

    private double _cellFontSize = 16;
    public double CellFontSize { get => _cellFontSize; set => SetProperty(ref _cellFontSize, value); }

    public string GameId { get; private set; } = "math_cross";
    public string DifficultyKey { get; private set; } = "easy";

    // No decimals allowed anymore
    public bool AllowDecimalInput => false;

    // Negative only for Master
    public bool AllowNegativeInput => DifficultyKey == "master";

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set
        {
            if (SetProperty(ref _levelNumber, value))
                OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => $"Math Cross – {DiffName(DifficultyKey)} (Lv {LevelNumber})";

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private MathCrossGame? _game;
    public MathCrossGame? Game { get => _game; private set => SetProperty(ref _game, value); }

    private MathCrossCellViewModel? _selectedCell;
    public MathCrossCellViewModel? SelectedCell
    {
        get => _selectedCell;
        set
        {
            if (_selectedCell != null) _selectedCell.IsSelected = false;
            if (SetProperty(ref _selectedCell, value) && _selectedCell != null)
                _selectedCell.IsSelected = true;
        }
    }

    // ===== Operator Picker =====
    private bool _isOperatorPickerVisible;
    public bool IsOperatorPickerVisible
    {
        get => _isOperatorPickerVisible;
        set
        {
            if (SetProperty(ref _isOperatorPickerVisible, value))
                OnPropertyChanged(nameof(IsDimVisible));
        }
    }

    public ObservableCollection<string> AvailableOperators { get; } = new();

    public bool IsDimVisible => IsOperatorPickerVisible;

    // Commands
    public AsyncCommand BackCommand { get; }
    public AsyncCommand CheckCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public AsyncCommand SolveCommand { get; }
    public AsyncCommand HintCommand { get; }

    public AsyncCommand CloseAllPickersCommand { get; }
    public AsyncCommand<string> PickOperatorCommand { get; }

    public MathCrossPageViewModel(
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        MathCrossGeneratorService generator)
    {
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _generator = generator;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
        CheckCommand = new AsyncCommand(CheckSolutionAsync);
        ResetCommand = new AsyncCommand(ResetAllInputsAsync);
        SolveCommand = new AsyncCommand(SolvePuzzleAsync);
        HintCommand = new AsyncCommand(GiveHintAsync);

        CloseAllPickersCommand = new AsyncCommand(() =>
        {
            IsOperatorPickerVisible = false;
            return Task.CompletedTask;
        });

        PickOperatorCommand = new AsyncCommand<string>(PickOperatorAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "math_cross" : gameId.Trim();
        DifficultyKey = NormalizeDifficulty(difficulty);
        LevelNumber = Math.Max(1, level);

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(AllowDecimalInput));
        OnPropertyChanged(nameof(AllowNegativeInput));

        UpdateAvailableOperators();

        _userProfile = await _userService.GetUserAsync();
        Coins = _userProfile?.Coins ?? 0;

        await StartNewRoundAsync();
    }

    private static string NormalizeDifficulty(string? diff)
    {
        var d = (diff ?? "easy").Trim().ToLowerInvariant();
        return d switch
        {
            "einfach" => "easy",
            "normal" => "normal",
            "schwer" => "hard",
            "master" => "master",
            "easy" => "easy",
            "hard" => "hard",
            _ => "easy"
        };
    }

    private void UpdateAvailableOperators()
    {
        AvailableOperators.Clear();

        if (DifficultyKey == "easy")
        {
            AvailableOperators.Add("+");
            AvailableOperators.Add("-");
            return;
        }

        AvailableOperators.Add("+");
        AvailableOperators.Add("-");
        AvailableOperators.Add("×");
        AvailableOperators.Add("÷");
    }

    public async Task StartNewRoundAsync()
    {
        IsBusy = true;
        IsOperatorPickerVisible = false;

        int seed = StableHash($"{GameId}:{DifficultyKey}") + LevelNumber * 77;

        MathCrossGame? newGame = null;

        await Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    var g = _generator.GenerateGame(DifficultyKey, seed + i);
                    if (g != null && g.Rows > 0 && g.Equations.Count > 0)
                    {
                        newGame = g;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MathCross generation failed: {ex}");
                }
            }
        });

        if (newGame == null || newGame.Rows == 0)
        {
            try
            {
                newGame = _generator.GenerateGame("easy", seed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MathCross fallback generation failed: {ex}");
                newGame = new MathCrossGame();
            }
        }

        Game = newGame ?? new MathCrossGame();

        BuildGridViewModels();
        IsBusy = false;
    }

    private void BuildGridViewModels()
    {
        GridCells.Clear();
        FlatCells.Clear();
        SelectedCell = null;

        if (Game == null || Game.Rows <= 0 || Game.Cols <= 0) return;

        for (int r = 0; r < Game.Rows; r++)
        {
            var row = new ObservableCollection<MathCrossCellViewModel>();
            for (int c = 0; c < Game.Cols; c++)
            {
                var vm = new MathCrossCellViewModel(Game.Grid[r, c], this);
                row.Add(vm);
                FlatCells.Add(vm);
            }
            GridCells.Add(row);
        }
    }

    internal void SelectForEdit(MathCrossCellViewModel cellVm) => SelectedCell = cellVm;

    internal void OpenOperatorPicker(MathCrossCellViewModel cellVm)
    {
        SelectedCell = cellVm;
        if (!cellVm.IsEditable || !cellVm.IsOperatorCell) return;

        UpdateAvailableOperators();
        IsOperatorPickerVisible = true;
    }

    // Called when user enters a number via native keyboard
    internal void OnNumberEntered(MathCrossCellViewModel cellVm, string newValue)
    {
        if (!cellVm.IsEditable || !cellVm.IsNumberCell) return;

        var cell = cellVm.Cell;

        // Validate input
        if (string.IsNullOrWhiteSpace(newValue))
        {
            cell.UserInput = "";
            cellVm.UpdateDisplay();
            return;
        }

        // Clean input
        var cleaned = newValue.Trim();

        // Allow minus sign for Master difficulty
        if (cleaned == "-" && AllowNegativeInput)
        {
            cell.UserInput = "-";
            cellVm.UpdateDisplay();
            return;
        }

        // Try parse as integer
        if (int.TryParse(cleaned, out var num))
        {
            // Check bounds based on difficulty
            if (!AllowNegativeInput && num < 0)
            {
                cell.UserInput = Math.Abs(num).ToString();
            }
            else
            {
                cell.UserInput = num.ToString();
            }
        }

        cellVm.UpdateDisplay();
    }

    private async Task PickOperatorAsync(string? op)
    {
        if (string.IsNullOrWhiteSpace(op) || SelectedCell == null) return;

        var cell = SelectedCell.Cell;
        if (cell.IsGiven || cell.Type != CellType.Operator) return;

        cell.UserInput = op.Trim();
        SelectedCell.UpdateDisplay();

        IsOperatorPickerVisible = false;

        MoveToNextCell();
        await Task.CompletedTask;
    }

    // ======= RESET/SOLVE/HINT =======

    private async Task ResetAllInputsAsync()
    {
        if (Game == null) return;

        foreach (var vm in FlatCells)
        {
            var cell = vm.Cell;
            if (cell.IsGiven) continue;
            if (cell.Type is CellType.Empty or CellType.Equals) continue;

            cell.UserInput = "";
            vm.UpdateDisplay();
        }

        SelectedCell = null;
        IsOperatorPickerVisible = false;

        await Task.CompletedTask;
    }

    private async Task SolvePuzzleAsync()
    {
        if (Game == null) return;

        foreach (var vm in FlatCells)
        {
            var cell = vm.Cell;
            if (cell.Type is CellType.Empty or CellType.Equals) continue;
            if (cell.IsGiven) continue;

            cell.UserInput = cell.Solution;
            vm.UpdateDisplay();
        }

        SelectedCell = null;
        IsOperatorPickerVisible = false;

        await Task.CompletedTask;
    }

    private async Task GiveHintAsync()
    {
        if (Game == null) return;

        var candidates = FlatCells
            .Where(vm => vm.IsEditable && string.IsNullOrEmpty(vm.Cell.UserInput))
            .ToList();

        if (candidates.Count == 0)
        {
            await _dialog.AlertAsync("Hinweis", "Alle Felder sind bereits ausgefüllt!");
            return;
        }

        var pick = candidates[new Random().Next(candidates.Count)];
        pick.Cell.UserInput = pick.Cell.Solution;
        pick.UpdateDisplay();

        await _dialog.AlertAsync("Hinweis", "Ein Feld wurde für dich ausgefüllt!");
    }

    private void MoveToNextCell()
    {
        if (Game == null || SelectedCell == null) return;

        int row = SelectedCell.Cell.Row;
        int col = SelectedCell.Cell.Col;

        // Search forward
        for (int r = row; r < Game.Rows; r++)
        {
            int start = (r == row) ? col + 1 : 0;
            for (int c = start; c < Game.Cols; c++)
            {
                var cell = Game.Grid[r, c];
                if (!cell.IsGiven && cell.Type is CellType.Number or CellType.Operator)
                {
                    SelectedCell = GridCells[r][c];
                    return;
                }
            }
        }

        // Wrap around
        for (int r = 0; r <= row; r++)
        {
            int end = (r == row) ? col : Game.Cols;
            for (int c = 0; c < end; c++)
            {
                var cell = Game.Grid[r, c];
                if (!cell.IsGiven && cell.Type is CellType.Number or CellType.Operator)
                {
                    SelectedCell = GridCells[r][c];
                    return;
                }
            }
        }
    }

    // ======= CHECK =======

    private async Task CheckSolutionAsync()
    {
        if (Game == null) return;

        // Check if all editable fields are filled
        foreach (var vm in FlatCells)
        {
            if (vm.IsEditable && string.IsNullOrWhiteSpace(vm.Cell.UserInput))
            {
                await _dialog.AlertAsync("Fehlt noch was", "Bitte fülle alle leeren Felder aus.");
                return;
            }
        }

        bool allCorrect = true;

        foreach (var vm in FlatCells)
        {
            var cell = vm.Cell;
            if (cell.Type is CellType.Empty or CellType.Equals) continue;
            if (cell.IsGiven) continue;

            var userInput = NormalizeToken(cell.UserInput);
            var solution = NormalizeToken(cell.Solution);

            if (userInput != solution)
            {
                allCorrect = false;
                break;
            }
        }

        if (!allCorrect)
        {
            await _dialog.AlertAsync("Leider falsch", "Einige Eingaben stimmen nicht. Überprüfe die Kreuzungen!");
            return;
        }

        await _dialog.AlertAsync("Super!", "Level gelöst!");
        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);
        LevelNumber++;

        await StartNewRoundAsync();
    }

    private static string NormalizeToken(string? s)
    {
        return (s ?? "").Trim();
    }

    public Color GetCellColor(MathCrossCell cell, bool isSelected)
    {
        if (cell.Type == CellType.Empty) return Colors.Transparent;
        if (isSelected) return Color.FromArgb("#FFF9C4");
        if (cell.IsGiven || cell.Type == CellType.Equals) return Color.FromArgb("#F5F5F5");
        return Colors.White;
    }

    private static string DiffName(string key) => key switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "master" => "Master",
        _ => key
    };

    private static int StableHash(string s)
    {
        unchecked
        {
            const int fnvOffset = (int)2166136261;
            const int fnvPrime = 16777619;
            int hash = fnvOffset;
            foreach (var ch in s) { hash ^= ch; hash *= fnvPrime; }
            return Math.Abs(hash);
        }
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

        TapOperatorCommand = new AsyncCommand(() =>
        {
            _parent.OpenOperatorPicker(this);
            return Task.CompletedTask;
        });
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    public bool IsGiven => Cell.IsGiven;

    public bool IsEditable => !Cell.IsGiven && Cell.Type is CellType.Number or CellType.Operator;

    public bool IsNumberCell => Cell.Type == CellType.Number;
    public bool IsOperatorCell => Cell.Type == CellType.Operator;

    // Show Entry for editable number cells
    public bool ShowNumberEntry => IsEditable && IsNumberCell;

    // Show Button for editable operator cells
    public bool ShowOperatorButton => IsEditable && IsOperatorCell;

    // Show Label for given/readonly cells
    public bool ShowReadOnlyLabel => !IsEditable && Cell.Type != CellType.Empty;

    public string DisplayText =>
        Cell.Type == CellType.Empty ? "" :
        Cell.Type == CellType.Equals ? "=" :
        Cell.IsGiven ? Cell.Solution :
        Cell.UserInput;

    // For Entry binding (numbers)
    public string NumberInput
    {
        get => Cell.IsGiven ? Cell.Solution : (Cell.UserInput ?? "");
        set
        {
            if (!Cell.IsGiven && Cell.Type == CellType.Number)
            {
                _parent.OnNumberEntered(this, value);
            }
        }
    }

    public string OperatorDisplay
    {
        get
        {
            if (Cell.Type != CellType.Operator) return "";
            if (Cell.IsGiven) return Cell.Solution;
            return string.IsNullOrWhiteSpace(Cell.UserInput) ? "?" : Cell.UserInput;
        }
    }

    public AsyncCommand TapOperatorCommand { get; }

    public Color BackgroundColor => _parent.GetCellColor(Cell, IsSelected);

    public void UpdateDisplay()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(OperatorDisplay));
        OnPropertyChanged(nameof(NumberInput));
        OnPropertyChanged(nameof(IsGiven));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(ShowNumberEntry));
        OnPropertyChanged(nameof(ShowOperatorButton));
        OnPropertyChanged(nameof(ShowReadOnlyLabel));
        OnPropertyChanged(nameof(BackgroundColor));
    }
}
