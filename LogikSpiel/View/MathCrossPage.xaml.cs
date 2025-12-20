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
            if (e.PropertyName == nameof(MathCrossPageViewModel.Game))
                MainThread.BeginInvokeOnMainThread(BuildBoardGrid);
        };

        BoardHost.SizeChanged += (_, _) => BuildBoardGrid();
    }

    private void BuildBoardGrid()
    {
        if (BindingContext is not MathCrossPageViewModel vm || vm.Game == null) return;
        if (BoardHost.Width <= 0 || BoardHost.Height <= 0) return;

        int rows = Math.Max(1, vm.Game.Rows);
        int cols = Math.Max(1, vm.Game.Cols);

        double hSpace = BoardGrid.ColumnSpacing;
        double vSpace = BoardGrid.RowSpacing;

        double availableWidth = BoardHost.Width - BoardHost.Padding.HorizontalThickness;
        double availableHeight = BoardHost.Height - BoardHost.Padding.VerticalThickness;

        double cellW = (availableWidth - (cols - 1) * hSpace) / cols;
        double cellH = (availableHeight - (rows - 1) * vSpace) / rows;

        double size = Math.Floor(Math.Min(cellW, cellH));

        // Adjust cell size based on grid complexity
        // For 7-cell equations (Hard/Master), cells might need to be smaller
        double maxSize = vm.Game.UseExtendedEquations ? 60 : 80;
        vm.CellSize = Math.Clamp(size, 30, maxSize);
        vm.CellFontSize = vm.CellSize * 0.38;

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

        // ÄNDERUNG: Try-Catch um den asynchronen Aufruf
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await vm.LoadAsync("math_cross", diff, level);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden: {ex}");
                // Optional: Hier dem User eine Meldung zeigen, statt abzustürzen
            }
        });
    }
}