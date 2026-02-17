using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class NumberRainPage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;

    public NumberRainPage(NumberRainPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // WICHTIG: Arena-Gr��e f�r Spawn/Bounds
        RainArena.SizeChanged += (_, _) =>
        {
            if (BindingContext is NumberRainPageViewModel viewModel)
                viewModel.UpdateArenaSize(RainArena.Width, RainArena.Height);
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded && BindingContext is NumberRainPageViewModel vm)
        {
            _ = vm.LoadAsync("number_rain", "normal", 1);
            _isLoaded = true;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not NumberRainPageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() ?? "number_rain" : "number_rain";
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "normal" : "normal";

        int level = 1;
        if (query.TryGetValue("level", out var lvObj) && int.TryParse(lvObj?.ToString(), out var parsedLevel))
            level = parsedLevel;

        Dispatcher.Dispatch(async () =>
        {
            const int maxLevel = 10000;
            if (level > maxLevel) level = maxLevel;
            await vm.LoadAsync(gameId, difficulty, level);
        });
    }
}
