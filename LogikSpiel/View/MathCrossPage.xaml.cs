using System.Collections.Generic;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MathCrossPage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;

    public MathCrossPage(MathCrossPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        vm.PropertyChanged += (_, e) =>
        {
            // ✅ Nur Game reicht, Grid wird damit neu berechnet
            if (e.PropertyName == nameof(MathCrossPageViewModel.Game))
                MainThread.BeginInvokeOnMainThread(BuildBoardGrid);
        };

        BoardHost.SizeChanged += (_, _) => BuildBoardGrid();
    }

    private void BuildBoardGrid()
    {
        if (BindingContext is not MathCrossPageViewModel vm || vm.Game == null) return;

        if (BoardHost.Width <= 0 || BoardHost.Height <= 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(80);
                BuildBoardGrid();
            });
            return;
        }

        int rows = Math.Max(1, vm.Game.Rows);
        int cols = Math.Max(1, vm.Game.Cols);

        double hSpace = BoardGrid.ColumnSpacing;
        double vSpace = BoardGrid.RowSpacing;

        // Ziel: große Zellen, scrollen ist erlaubt.
        int visibleColsTarget = vm.Game.UseExtendedEquations ? 7 : 6;

        const double safety = 30;
        double availableWidth = Math.Max(0, BoardHost.Width - safety);

        double cellW = (availableWidth - (visibleColsTarget - 1) * hSpace) / visibleColsTarget;

        double max = vm.Game.UseExtendedEquations ? 92 : 110;
        vm.CellSize = Math.Clamp(Math.Floor(cellW), 44, max);
        vm.CellFontSize = vm.CellSize * 0.42;

        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();

        for (int r = 0; r < rows; r++)
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = vm.CellSize });

        for (int c = 0; c < cols; c++)
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = vm.CellSize });

        BoardGrid.WidthRequest = cols * vm.CellSize + (cols - 1) * hSpace;
        BoardGrid.HeightRequest = rows * vm.CellSize + (rows - 1) * vSpace;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_isLoaded || BindingContext is not MathCrossPageViewModel vm) return;
        _isLoaded = true;

        string diff = query.TryGetValue("difficulty", out var d) ? d?.ToString() ?? "easy" : "easy";
        int level = query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var lv) ? lv : 1;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await vm.LoadAsync("math_cross", diff, level); }
            catch (Exception ex) { await DisplayAlertAsync("Fehler", ex.Message, "OK"); }
        });
    }
}
