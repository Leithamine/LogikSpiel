using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using LogikSpiel.ViewModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View;

public partial class MathCrossPage : ContentPage, IQueryAttributable, IDisposable
{
    private bool _isLoaded;
    private const double DefaultCellSize = 56;
    private const double MinCellSize = 36;
    private const double PreferredTouchCellSize = 54;
    private const double MaxCellSize = 72;
    private const double CellSpacing = 2;
    private const double BoardInnerPadding = 4;

    private double _uniformCellSize = DefaultCellSize;

    private readonly MathCrossPageViewModel _vm;
    private readonly Action _requestLayoutUpdateHandler;
    private bool _disposed;

    public MathCrossPage(MathCrossPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        _requestLayoutUpdateHandler = BuildGrid;
        _vm.RequestLayoutUpdate += _requestLayoutUpdateHandler;
        BoardContainer.SizeChanged += OnBoardContainerSizeChanged;
        SizeChanged += OnPageSizeChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _vm.RequestLayoutUpdate -= _requestLayoutUpdateHandler;
        BoardContainer.SizeChanged -= OnBoardContainerSizeChanged;
        SizeChanged -= OnPageSizeChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void BuildGrid()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (BindingContext is not MathCrossPageViewModel vm || vm.Game == null) return;

            var game = vm.Game;
            _uniformCellSize = GetUniformCellSize();
            double cellSize = _uniformCellSize;
            double textSize = Math.Clamp(cellSize * 0.42, 13, 28);

            BoardGrid.Children.Clear();
            BoardGrid.RowDefinitions.Clear();
            BoardGrid.ColumnDefinitions.Clear();
            BoardGrid.RowSpacing = CellSpacing;
            BoardGrid.ColumnSpacing = CellSpacing;

            for (int r = 0; r < game.Rows; r++) BoardGrid.RowDefinitions.Add(new RowDefinition { Height = cellSize });
            for (int c = 0; c < game.Cols; c++) BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = cellSize });

            var cellMap = vm.FlatCells.ToDictionary(c => (c.Cell.Row, c.Cell.Col));

            for (int r = 0; r < game.Rows; r++)
            {
                for (int c = 0; c < game.Cols; c++)
                {
                    if (!cellMap.TryGetValue((r, c), out var cellVm))
                        continue;

                    var cell = cellVm.Cell;
                    var border = new Border { StrokeShape = new RoundRectangle { CornerRadius = 4 }, Padding = 0, BindingContext = cellVm };
                    border.SetBinding(Border.StrokeProperty, new Binding(nameof(MathCrossCellViewModel.BorderStroke), source: cellVm));
                    border.SetBinding(Border.StrokeThicknessProperty, new Binding(nameof(MathCrossCellViewModel.BorderThickness), source: cellVm));
                    border.SetBinding(Border.ShadowProperty, new Binding(nameof(MathCrossCellViewModel.FocusGlow), source: cellVm));
                    border.SetBinding(Border.BackgroundColorProperty,
                        new Binding(nameof(MathCrossCellViewModel.CellBackground), source: cellVm));

                    Grid.SetRow(border, cell.Row);
                    Grid.SetColumn(border, cell.Col);

                    if (cellVm.IsEditable)
                    {
                        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = cellVm.TapCellCommand });
                        border.GestureRecognizers.Add(new DragGestureRecognizer { });
                        ((DragGestureRecognizer)border.GestureRecognizers[^1]).DragStarting += OnCellDragStarting;

                        border.GestureRecognizers.Add(new DropGestureRecognizer
                        {
                            AllowDrop = true
                        });
                        ((DropGestureRecognizer)border.GestureRecognizers[^1]).DragOver += OnCellDragOver;
                        ((DropGestureRecognizer)border.GestureRecognizers[^1]).Drop += OnCellDrop;
                    }

                    var lbl = new Label
                    {
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = ResolveTextColor(cell),
                        FontSize = textSize,
                        LineBreakMode = LineBreakMode.NoWrap
                    };
                    lbl.SetBinding(Label.TextProperty, new Binding(nameof(MathCrossCellViewModel.EditableText), source: cellVm));
                    border.Content = lbl;
                    BoardGrid.Children.Add(border);
                }
            }
        });
    }

    private void OnTileDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not NumberBankTileViewModel tileVm) return;
        e.Data.Properties.Add("tileId", tileVm.Tile.Id);
        _vm.SetDragging(true);
    }


    private void OnCellDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is not BindableObject bo || bo.BindingContext is not MathCrossCellViewModel cellVm) return;
        var cell = cellVm.Cell;
        if (!cellVm.IsEditable || string.IsNullOrWhiteSpace(cell.UserInput)) return;

        e.Data.Properties["sourceCell"] = $"{cell.Row}:{cell.Col}";
        _vm.SetDragging(true);
    }

    private MathCrossCellViewModel? ResolveCellVmFromProperties(DataPackagePropertySetView properties)
    {
        if (!properties.TryGetValue("sourceCell", out var srcObj)) return null;
        var src = srcObj?.ToString();
        if (string.IsNullOrWhiteSpace(src)) return null;

        var parts = src.Split(':');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], out int r) || !int.TryParse(parts[1], out int c)) return null;

        return _vm.FlatCells.FirstOrDefault(x => x.Cell.Row == r && x.Cell.Col == c);
    }

    private void OnBankDrop(object? sender, DropEventArgs e)
    {
        _vm.SetDragging(false);
        var sourceVm = ResolveCellVmFromProperties(e.Data.Properties);
        if (sourceVm == null) return;
        _vm.TryReturnCellNumberToBank(sourceVm);
    }

    private void OnCellDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void OnCellDrop(object? sender, DropEventArgs e)
    {
        _vm.SetDragging(false);
        if (sender is not BindableObject bo || bo.BindingContext is not MathCrossCellViewModel target) return;

        if (e.Data.Properties.TryGetValue("tileId", out var tileIdObj) && !string.IsNullOrWhiteSpace(tileIdObj?.ToString()))
        {
            _vm.TryPlaceTileById(tileIdObj?.ToString(), target);
            return;
        }

        var sourceVm = ResolveCellVmFromProperties(e.Data.Properties);
        if (sourceVm == null) return;
        _vm.TryMoveCellToCell(sourceVm, target);
    }

    private void OnBoardContainerSizeChanged(object? sender, EventArgs e) => RecalculateLayoutAndRefresh();
    private void OnPageSizeChanged(object? sender, EventArgs e) => RecalculateLayoutAndRefresh();

    private void RecalculateLayoutAndRefresh()
    {
        if (BoardContainer.Width <= 0 || BoardContainer.Height <= 0) return;
        UpdateLayoutConstraintsFromViewport();
        _uniformCellSize = GetUniformCellSize();
        BuildGrid();
    }

    private void UpdateLayoutConstraintsFromViewport()
    {
        double availableWidth = Math.Max(0, BoardContainer.Width - 2 * BoardInnerPadding);
        double availableHeight = Math.Max(0, BoardContainer.Height - 2 * BoardInnerPadding);
        int maxRows = Math.Max(1, (int)Math.Floor((availableHeight + CellSpacing) / (PreferredTouchCellSize + CellSpacing)));
        int maxCols = Math.Max(1, (int)Math.Floor((availableWidth + CellSpacing) / (PreferredTouchCellSize + CellSpacing)));
        _ = _vm.UpdateLayoutConstraintsAsync(maxRows, maxCols);
    }

    private double GetUniformCellSize()
    {
        if (BoardContainer.Width <= 0 || BoardContainer.Height <= 0) return DefaultCellSize;
        if (BindingContext is not MathCrossPageViewModel vm || vm.Game is not MathCrossGame game) return DefaultCellSize;
        int rows = Math.Max(1, game.Rows);
        int cols = Math.Max(1, game.Cols);
        double availableWidth = Math.Max(0, BoardContainer.Width - 2 * BoardInnerPadding);
        double availableHeight = Math.Max(0, BoardContainer.Height - 2 * BoardInnerPadding);
        double widthBased = (availableWidth - CellSpacing * Math.Max(0, cols - 1)) / cols;
        double heightBased = (availableHeight - CellSpacing * Math.Max(0, rows - 1)) / rows;
        return Math.Clamp(Math.Min(widthBased, heightBased), MinCellSize, MaxCellSize);
    }

    private static Color ResolveTextColor(MathCrossCell cell)
    {
        if (cell.IsGiven || cell.Type == CellType.Equals || cell.Type == CellType.Operator)
            return GetColor("C_MathCell_Fixed_Text", "#E8EDF5");
        return GetColor("C_MathCell_Num_Text", "#F3FAFF");
    }

    private static Color GetColor(string key, string fallbackHex)
    {
        if (Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue(key, out var resource)
            && resource is Color color)
            return color;

        return Color.FromArgb(fallbackHex);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_isLoaded || BindingContext is not MathCrossPageViewModel vm) return;
        _isLoaded = true;

        string diff = query.TryGetValue("difficulty", out var d) ? d?.ToString() ?? "easy" : "easy";
        diff = (diff ?? "easy").Trim().ToLowerInvariant();
        if (diff is not ("easy" or "normal" or "hard" or "master")) diff = "easy";

        int level = query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var lv) ? lv : 1;
        level = Math.Clamp(level, 1, 10000);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            try { await vm.LoadAsync("math_cross", diff, level); }
            catch (Exception ex)
            {
                var dialogService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IDialogService>();
                if (dialogService is not null)
                    await dialogService.AlertAsync(LocalizationService.GetString("MathCross_ErrorTitle"), ex.Message, LocalizationService.GetString("Common_Ok"));
                else
                    await DisplayAlertAsync(LocalizationService.GetString("MathCross_ErrorTitle"), ex.Message, LocalizationService.GetString("Common_Ok"));
            }
        });
    }
}
