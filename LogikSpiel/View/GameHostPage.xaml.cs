using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameHostPage : ContentPage, IQueryAttributable
{
    public GameHostPage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<GameHostPageViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not GameHostPageViewModel vm) return;

        var gameId = query.TryGetValue("gameId", out var g) ? g as string : null;
        var diff = query.TryGetValue("difficulty", out var d) ? d as string : "normal";
        int level = 1;
        if (query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var parsedLevel))
            level = parsedLevel;

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            const int maxLevel = 10000;
            if (level > maxLevel) level = maxLevel;
            _ = vm.LoadAsync(gameId!, diff ?? "normal", level);
        }
    }
}
