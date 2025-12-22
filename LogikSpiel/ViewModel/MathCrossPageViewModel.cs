#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.ViewModel;

public sealed class MathCrossPageViewModel : ObservableObject
{
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly MathCrossGeneratorService _generator;

    private UserProfile? _userProfile;

    // ===== Grid data =====
    private ObservableCollection<MathCrossCellViewModel> _flatCells = new();
    public ObservableCollection<MathCrossCellViewModel> FlatCells
    {
        get => _flatCells;
        set => SetProperty(ref _flatCells, value);
    }

    public ObservableCollection<ObservableCollection<MathCrossCellViewModel>> GridCells { get; } = new();

    // ===== Layout =====
    private double _cellSize = 60;
    public double CellSize { get => _cellSize; set => SetProperty(ref _cellSize, value); }

    private double _cellFontSize = 20;
    public double CellFontSize { get => _cellFontSize; set => SetProperty(ref _cellFontSize, value); }

    // ===== Game meta =====
    public string GameId { get; private set; } = "math_cross";
    public string DifficultyKey { get; private set; } = "easy";

    // ✅ Für KeyboardByDifficultyConverter
    public bool AllowDecimalInput => false;
    public bool AllowNegativeInput => DifficultyKey == "master";

    public string DifficultyText => $"Schwierigkeit: {DiffName(DifficultyKey)}";

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set => SetProperty(ref _levelNumber, value);
    }

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

    private MathCrossGame? _game;
    public MathCrossGame? Game
    {
        get => _game;
        private set => SetProperty(ref _game, value);
    }

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

    // ===== Token banks =====
    public ObservableCollection<MathTokenViewModel> NumberTokens { get; } = new();

    private MathTokenViewModel? _selectedNumber;
    public MathTokenViewModel? SelectedNumber
    {
        get => _selectedNumber;
        private set => SetProperty(ref _selectedNumber, value);
    }

    private string? _selectedOperator;
    public string? SelectedOperator
    {
        get => _selectedOperator;
        private set => SetProperty(ref _selectedOperator, value);
    }

    // ✅ Fokus springt NICHT automatisch (User entscheidet)
    public bool AutoAdvance { get; set; } = false;

    // ===== Commands =====
    public AsyncCommand BackCommand { get; }
    public AsyncCommand ResetCommand { get; }
    public AsyncCommand SolveLevelCommand { get; }
    public AsyncCommand<string> PickOperatorCommand { get; }
    public AsyncCommand<MathTokenViewModel> PickNumberTokenCommand { get; }

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
        ResetCommand = new AsyncCommand(ResetAllInputsAsync);
        SolveLevelCommand = new AsyncCommand(CheckSolutionAsync);

        PickOperatorCommand = new AsyncCommand<string>(PickOperatorAsync);
        PickNumberTokenCommand = new AsyncCommand<MathTokenViewModel>(PickNumberTokenAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "math_cross" : gameId.Trim();
        DifficultyKey = NormalizeDifficulty(difficulty);
        LevelNumber = Math.Max(1, level);

        OnPropertyChanged(nameof(DifficultyText));
        OnPropertyChanged(nameof(AllowDecimalInput));
        OnPropertyChanged(nameof(AllowNegativeInput));

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

    public async Task StartNewRoundAsync()
    {
        IsBusy = true;

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
                catch
                {
                    // ignore and keep trying
                }
            }
        });

        Game = newGame ?? new MathCrossGame();

        BuildGridViewModels();
        BuildNumberTokens(seed);

        SelectedCell = null;
        ClearSelections();

        IsBusy = false;
    }

    private void BuildGridViewModels()
    {
        GridCells.Clear();
        SelectedCell = null;

        if (Game == null || Game.Rows <= 0 || Game.Cols <= 0)
        {
            FlatCells = new ObservableCollection<MathCrossCellViewModel>();
            return;
        }

        var temp = new List<MathCrossCellViewModel>();

        for (int r = 0; r < Game.Rows; r++)
        {
            var row = new ObservableCollection<MathCrossCellViewModel>();
            for (int c = 0; c < Game.Cols; c++)
            {
                var vm = new MathCrossCellViewModel(Game.Grid[r, c], this);
                row.Add(vm);
                temp.Add(vm);
            }
            GridCells.Add(row);
        }

        FlatCells = new ObservableCollection<MathCrossCellViewModel>(temp);
    }

    private void BuildNumberTokens(int seed)
    {
        NumberTokens.Clear();
        if (Game == null) return;

        var list = new List<MathTokenViewModel>();

        foreach (var vm in FlatCells)
        {
            var cell = vm.Cell;

            // Nur NICHT-gegebene Zahlen werden als Tokens erzeugt
            if (cell.IsGiven) continue;
            if (cell.Type != CellType.Number) continue;

            list.Add(new MathTokenViewModel(cell.Solution));

            // Editierbare Zahl-Felder starten leer
            cell.UserInput = "";
            vm.UpdateDisplay();
        }

        // Shuffle Tokens deterministic
        var rnd = new Random(seed + 999);
        foreach (var t in list.OrderBy(_ => rnd.Next()))
            NumberTokens.Add(t);
    }

    private void ClearSelections()
    {
        if (SelectedNumber != null) SelectedNumber.IsSelected = false;
        SelectedNumber = null;

        SelectedOperator = null;
    }

    // ===== UI actions =====

    internal void OnCellTapped(MathCrossCellViewModel cellVm)
    {
        if (!cellVm.IsEditable) return;

        SelectedCell = cellVm;

        // Zahl ausgewählt + Zahlzelle -> eintragen
        if (SelectedNumber != null && cellVm.IsNumberCell)
        {
            PlaceNumberToken(SelectedNumber, cellVm);
            return;
        }

        // Operator ausgewählt + Operatorzelle -> eintragen
        if (!string.IsNullOrWhiteSpace(SelectedOperator) && cellVm.IsOperatorCell)
        {
            PlaceOperator(SelectedOperator!, cellVm);
            return;
        }

        // Keine Auswahl: Tap auf belegtes Feld -> zurück / löschen
        if (SelectedNumber == null && string.IsNullOrWhiteSpace(SelectedOperator))
        {
            if (cellVm.IsNumberCell && !string.IsNullOrWhiteSpace(cellVm.Cell.UserInput))
            {
                ReturnNumberToBank(cellVm);
            }
            else if (cellVm.IsOperatorCell && !string.IsNullOrWhiteSpace(cellVm.Cell.UserInput))
            {
                cellVm.Cell.UserInput = "";
                cellVm.UpdateDisplay();
            }
        }
    }

    private async Task PickNumberTokenAsync(MathTokenViewModel? token)
    {
        if (token == null) return;

        if (SelectedNumber == token)
        {
            ClearSelections();
            return;
        }

        if (SelectedNumber != null) SelectedNumber.IsSelected = false;

        SelectedNumber = token;
        SelectedNumber.IsSelected = true;

        // Operator abwählen
        SelectedOperator = null;

        // Wenn bereits eine Zelle aktiv ist und eine Zahl erwartet, direkt eintragen
        if (SelectedCell is { IsEditable: true, IsNumberCell: true })
        {
            PlaceNumberToken(token, SelectedCell);
        }

        await Task.CompletedTask;
    }

    private async Task PickOperatorAsync(string? op)
    {
        if (string.IsNullOrWhiteSpace(op)) return;

        var normalized = NormalizeOperator(op);

        // Toggle: gleicher Operator nochmal -> abwählen
        if (SelectedOperator == normalized)
        {
            SelectedOperator = null;
            await Task.CompletedTask;
            return;
        }

        SelectedOperator = normalized;

        // Zahl abwählen
        if (SelectedNumber != null) SelectedNumber.IsSelected = false;
        SelectedNumber = null;

        // Wenn bereits eine Zelle aktiv ist und ein Operator erwartet, direkt eintragen
        if (SelectedCell is { IsEditable: true, IsOperatorCell: true })
        {
            PlaceOperator(normalized, SelectedCell);
        }

        await Task.CompletedTask;
    }

    private void PlaceNumberToken(MathTokenViewModel token, MathCrossCellViewModel cellVm)
    {
        if (!cellVm.IsEditable || !cellVm.IsNumberCell) return;

        // Falls schon Zahl drin -> zurück in Bank
        if (!string.IsNullOrWhiteSpace(cellVm.Cell.UserInput))
            NumberTokens.Add(new MathTokenViewModel(cellVm.Cell.UserInput));

        cellVm.Cell.UserInput = NormalizeNumberString(token.Value);
        cellVm.UpdateDisplay();

        NumberTokens.Remove(token);

        // Zahl-Auswahl leeren (wie üblich)
        ClearSelections();

        if (AutoAdvance) MoveToNextCell();
    }

    private void PlaceOperator(string op, MathCrossCellViewModel cellVm)
    {
        if (!cellVm.IsEditable || !cellVm.IsOperatorCell) return;

        cellVm.Cell.UserInput = NormalizeOperator(op);
        cellVm.UpdateDisplay();

        // Operator bleibt ausgewählt (für mehrere Operatoren)
        if (AutoAdvance) MoveToNextCell();
    }

    private void ReturnNumberToBank(MathCrossCellViewModel cellVm)
    {
        var v = cellVm.Cell.UserInput;
        if (string.IsNullOrWhiteSpace(v)) return;

        NumberTokens.Add(new MathTokenViewModel(v));
        cellVm.Cell.UserInput = "";
        cellVm.UpdateDisplay();
    }

    private void MoveToNextCell()
    {
        if (Game == null || SelectedCell == null) return;

        int row = SelectedCell.Cell.Row;
        int col = SelectedCell.Cell.Col;

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

    // ===== Reset =====
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

        int seed = StableHash($"{GameId}:{DifficultyKey}") + LevelNumber * 77;
        BuildNumberTokens(seed);

        SelectedCell = null;
        ClearSelections();

        await Task.CompletedTask;
    }

    // ===== Check / Solve =====
    private async Task CheckSolutionAsync()
    {
        if (Game == null) return;

        var missing = FlatCells
            .Where(vm => vm.IsEditable && IsMissingInput(vm.Cell.UserInput, vm.Cell.Type))
            .ToList();

        if (missing.Count > 0)
        {
            SelectedCell = missing[0];
            await _dialog.AlertAsync("Fehlt noch was", $"Bitte fülle alle Felder aus. Es fehlen noch: {missing.Count}");
            return;
        }


        // Vergleiche: Zahlen als int, Operatoren normalisiert
        foreach (var vm in FlatCells)
        {
            var cell = vm.Cell;
            if (cell.Type is CellType.Empty or CellType.Equals) continue;
            if (cell.IsGiven) continue;

            bool ok = cell.Type switch
            {
                CellType.Number => SameNumber(cell.UserInput, cell.Solution),
                CellType.Operator => SameOperator(cell.UserInput, cell.Solution),
                _ => true
            };

            if (!ok)
            {
                SelectedCell = vm;
                await _dialog.AlertAsync("Nicht korrekt", "Mindestens ein Feld ist falsch. (Feld ist markiert)");
                return;
            }
        }

        await _dialog.AlertAsync("Super!", "Level gelöst!");
        await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, LevelNumber);

        LevelNumber++;
        await StartNewRoundAsync();
    }
    private static bool IsMissingInput(string? input, CellType type)
    {
        var t = (input ?? "").Trim();

        // Platzhalter gelten als "leer"
        if (string.IsNullOrWhiteSpace(t)) return true;
        if (type == CellType.Number && (t == "·" || t == ".")) return true;
        if (type == CellType.Operator && (t == "?")) return true;

        return false;
    }

    // ===== Normalisierung =====
    private static bool SameOperator(string? user, string? sol)
        => NormalizeOperator(user) == NormalizeOperator(sol);

    private static bool SameNumber(string? user, string? sol)
    {
        if (!int.TryParse(NormalizeNumberString(user), out var u)) return false;
        if (!int.TryParse(NormalizeNumberString(sol), out var s)) return false;
        return u == s;
    }

    private static string NormalizeOperator(string? s)
    {
        var t = (s ?? "").Trim();

        // Unicode minus -> normal minus
        t = t.Replace('−', '-');

        // Varianten normalisieren
        if (t == "x" || t == "X" || t == "*") return "×";
        if (t == "/" || t == ":") return "÷";

        return t;
    }

    private static string NormalizeNumberString(string? s)
    {
        var t = (s ?? "").Trim();

        t = t.Replace('−', '-');

        if (t.StartsWith("+")) t = t[1..];

        if (int.TryParse(t, out var v)) return v.ToString();

        return t;
    }

    // ===== Farben =====
    public Color GetCellColor(MathCrossCell cell, bool isSelected)
    {
        if (cell.Type == CellType.Empty) return Colors.Transparent;
        if (isSelected) return Color.FromArgb("#FFF3B0");

        if (cell.Type == CellType.Equals) return Color.FromArgb("#EDEDED");
        if (cell.IsGiven) return Color.FromArgb("#E0E0E0");

        // Farbschema laut Anforderung:
        // - Zahlen-Eingabefelder: Weiß
        // - Operator-Eingabefelder: Gelb
        // - Gefüllte / vorgegebene Felder: Grau (oben abgedeckt)
        if (cell.Type == CellType.Operator)
            return Color.FromArgb("#FFEAA7"); // sanftes Gelb

        if (cell.Type == CellType.Number)
            return Colors.White;

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

    // ✅ Empty-Felder komplett ausblenden im UI
    public bool IsBoardCellVisible => Cell.Type != CellType.Empty;

    public MathCrossCellViewModel(MathCrossCell cell, MathCrossPageViewModel parent)
    {
        Cell = cell;
        _parent = parent;

        TapCellCommand = new AsyncCommand(() =>
        {
            _parent.OnCellTapped(this);
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

    public bool ShowEditableButton => IsBoardCellVisible && IsEditable;
    public bool ShowReadOnlyLabel => IsBoardCellVisible && !IsEditable;

    public string DisplayText =>
        Cell.Type == CellType.Empty ? "" :
        Cell.Type == CellType.Equals ? "=" :
        Cell.IsGiven ? Cell.Solution :
        Cell.UserInput;

    public string EditableText
    {
        get
        {
            if (!IsEditable) return "";

            if (IsOperatorCell)
                return string.IsNullOrWhiteSpace(Cell.UserInput) ? "." : Cell.UserInput;

            return string.IsNullOrWhiteSpace(Cell.UserInput) ? "X" : Cell.UserInput;
        }
    }

    public AsyncCommand TapCellCommand { get; }

    public Color BackgroundColor => _parent.GetCellColor(Cell, IsSelected);

    public void UpdateDisplay()
    {
        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(EditableText));
        OnPropertyChanged(nameof(IsGiven));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(ShowEditableButton));
        OnPropertyChanged(nameof(ShowReadOnlyLabel));
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(IsBoardCellVisible));
    }
}

public sealed class MathTokenViewModel : ObservableObject
{
    public MathTokenViewModel(string value)
    {
        Value = (value ?? "").Trim();
    }

    public string Value { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
