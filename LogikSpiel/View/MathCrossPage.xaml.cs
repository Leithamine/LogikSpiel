using LogikSpiel.Core;
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
    private const double DefaultCellSize = 40;
    private const double MinCellSize = 22;
    private const double MaxCellSize = 46;
    private const double CellSpacing = 4;

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

    ~MathCrossPage()
    {
        Dispose();
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
            double cellSize = _uniformCellSize;
            double textSize = Math.Clamp(cellSize * 0.34, 11, 22);

            BoardGrid.Children.Clear();
            BoardGrid.RowDefinitions.Clear();
            BoardGrid.ColumnDefinitions.Clear();

            for (int r = 0; r < game.Rows; r++)
                BoardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });

            for (int c = 0; c < game.Cols; c++)
                BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });


            BoardGrid.WidthRequest = game.Cols * cellSize + CellSpacing * Math.Max(0, game.Cols - 1);
            BoardGrid.HeightRequest = game.Rows * cellSize + CellSpacing * Math.Max(0, game.Rows - 1);

            foreach (var cellVm in vm.FlatCells)
            {
                var cell = cellVm.Cell;

                var border = new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 6 },
                    Padding = 0
                };

                border.SetBinding(Border.StrokeProperty,
                    new Binding(nameof(MathCrossCellViewModel.BorderStroke), source: cellVm));
                border.SetBinding(Border.StrokeThicknessProperty,
                    new Binding(nameof(MathCrossCellViewModel.BorderThickness), source: cellVm));
                border.SetBinding(Border.ShadowProperty,
                    new Binding(nameof(MathCrossCellViewModel.FocusGlow), source: cellVm));

                border.SetBinding(Border.BackgroundColorProperty,
                    new Binding(nameof(MathCrossCellViewModel.Cell),
                        source: cellVm,
                        converter: (IValueConverter)Resources["MathCellToColorConverter"]));

                Grid.SetRow(border, cell.Row);
                Grid.SetColumn(border, cell.Col);

                if (cellVm.IsEditable)
                {
                    var btn = new Button
                    {
                        Padding = 0,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = textSize,
                        BackgroundColor = Colors.Transparent,
                        TextColor = ResolveTextColor(cell),
                        BorderWidth = 0
                    };
                    btn.SetBinding(Button.TextProperty, new Binding(nameof(MathCrossCellViewModel.EditableText), source: cellVm));
                    btn.SetBinding(Button.CommandProperty, new Binding(nameof(MathCrossCellViewModel.TapCellCommand), source: cellVm));
                    border.Content = btn;
                }
                else
                {
                    var lbl = new Label
                    {
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = ResolveTextColor(cell),
                        FontSize = textSize
                    };
                    lbl.SetBinding(Label.TextProperty, new Binding(nameof(MathCrossCellViewModel.DisplayText), source: cellVm));
                    border.Content = lbl;
                }

                BoardGrid.Children.Add(border);
            }
        });
    }

    private void OnBoardContainerSizeChanged(object? sender, EventArgs e)
    {
        RecalculateLayoutAndRefresh();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        RecalculateLayoutAndRefresh();
    }

    private void RecalculateLayoutAndRefresh()
    {
        if (BoardContainer.Width <= 0 || BoardContainer.Height <= 0)
            return;

        _uniformCellSize = GetUniformCellSize();

        int rows = Math.Max(1, (int)Math.Floor((BoardContainer.Height + CellSpacing) / (_uniformCellSize + CellSpacing)));
        int cols = Math.Max(1, (int)Math.Floor((BoardContainer.Width + CellSpacing) / (_uniformCellSize + CellSpacing)));

        _ = _vm.UpdateLayoutConstraintsAsync(rows, cols);
        BuildGrid();
    }

    private double GetUniformCellSize()
    {
        if (BoardContainer.Width <= 0 || BoardContainer.Height <= 0)
            return DefaultCellSize;

        if (BindingContext is not MathCrossPageViewModel vm || vm.Game is not MathCrossGame game)
            return DefaultCellSize;

        int rows = Math.Max(1, game.Rows);
        int cols = Math.Max(1, game.Cols);

        double widthBased = (BoardContainer.Width - CellSpacing * Math.Max(0, cols - 1)) / cols;
        double heightBased = (BoardContainer.Height - CellSpacing * Math.Max(0, rows - 1)) / rows;

        double sizeToFit = Math.Min(widthBased, heightBased);
        return Math.Clamp(sizeToFit, MinCellSize, MaxCellSize);
    }

    private static Color ResolveTextColor(MathCrossCell cell)
    {
        if (cell.IsGiven || cell.Type == CellType.Equals)
            return GetColor("C_MathCell_Fixed_Text", "#E8EDF5");

        return cell.Type switch
        {
            CellType.Number => GetColor("C_MathCell_Num_Text", "#F3FAFF"),
            CellType.Operator => GetColor("C_MathCell_Op_Text", "#F7F4FF"),
            _ => Colors.White
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_isLoaded || BindingContext is not MathCrossPageViewModel vm) return;
        _isLoaded = true;

        string diff = query.TryGetValue("difficulty", out var d) ? d?.ToString() ?? "easy" : "easy";
        diff = (diff ?? "easy").Trim().ToLowerInvariant();
        if (diff is not ("easy" or "normal" or "hard" or "master"))
            diff = "easy";

        int level = query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var lv) ? lv : 1;

        const int maxLevel = 10000;
        level = Math.Clamp(level, 1, maxLevel);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            try { await vm.LoadAsync("math_cross", diff, level); }
            catch (Exception ex)
            {
                var errorTitle = LocalizationService.GetString("MathCross_ErrorTitle");
                var okLabel = LocalizationService.GetString("Common_Ok");
                var dialogService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IDialogService>();
                if (dialogService is not null)
                {
                    await dialogService.AlertAsync(errorTitle, ex.Message, okLabel);
                }
                else
                {
                    await DisplayAlertAsync(errorTitle, ex.Message, okLabel);
                }
            }
        });
    }
}
