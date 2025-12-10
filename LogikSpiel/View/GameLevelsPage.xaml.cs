using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameLevelsPage : ContentPage, IQueryAttributable
{
    public GameLevelsPage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<GameLevelsViewModel>();
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
}
