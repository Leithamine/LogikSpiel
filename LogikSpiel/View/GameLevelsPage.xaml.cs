using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameLevelsPage : ContentPage, IQueryAttributable
{
    private double _lastWidth;

    public GameLevelsPage(GameLevelsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not GameLevelsViewModel vm) return;
        if (query.TryGetValue("gameId", out var idObj) && idObj is string id)
            vm.GameId = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is GameLevelsViewModel vm)
            await vm.LoadAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Math.Abs(width - _lastWidth) < 1) return;
        _lastWidth = width;

        (BindingContext as GameLevelsViewModel)?.UpdateTileSpan(width);
    }
}
