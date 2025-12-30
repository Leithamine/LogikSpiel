#nullable enable
using LogikSpiel.ViewModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View;

public partial class MathCrossPage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;
    private const double CellSize = 42;

    public MathCrossPage(MathCrossPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.RequestLayoutUpdate += () => MainThread.BeginInvokeOnMainThread(BuildGrid);
        vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.Game)) MainThread.BeginInvokeOnMainThread(BuildGrid); };
    }

    private void BuildGrid()
    {
        if (BindingContext is not MathCrossPageViewModel vm || vm.Game == null) return;

        var game = vm.Game;

        BoardGrid.Children.Clear();
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();

        for (int r = 0; r < game.Rows; r++)
            BoardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellSize) });

        for (int c = 0; c < game.Cols; c++)
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });

        foreach (var cellVm in vm.FlatCells)
        {
            var cell = cellVm.Cell;

            var border = new Border
            {
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#CCCCCC"),
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                Padding = 0
            };

            border.SetBinding(Border.BackgroundColorProperty,
                new Binding(nameof(MathCrossCellViewModel.BackgroundColor), source: cellVm));

            Grid.SetRow(border, cell.Row);
            Grid.SetColumn(border, cell.Col);

            if (cellVm.IsEditable)
            {
                var btn = new Button
                {
                    Padding = 0,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 15,
                    BackgroundColor = Colors.Transparent,
                    TextColor = Color.FromArgb("#222"),
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
                    TextColor = Color.FromArgb("#333"),
                    FontSize = 15
                };
                lbl.SetBinding(Label.TextProperty, new Binding(nameof(MathCrossCellViewModel.DisplayText), source: cellVm));
                border.Content = lbl;
            }

            BoardGrid.Children.Add(border);
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_isLoaded || BindingContext is not MathCrossPageViewModel vm) return;
        _isLoaded = true;

        string diff = query.TryGetValue("difficulty", out var d) ? d?.ToString() ?? "easy" : "easy";
        int level = query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var lv) ? lv : 1;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            try { await vm.LoadAsync("math_cross", diff, level); }
            catch (Exception ex) { await DisplayAlertAsync("Fehler", ex.Message, "OK"); }
        });
    }
}
