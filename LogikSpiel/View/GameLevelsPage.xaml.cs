using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameLevelsPage : ContentPage, IQueryAttributable
{
    public GameLevelsPage() : this(AppServices.Get<GameLevelsViewModel>()) { }

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
        try
        {
            if (BindingContext is GameLevelsViewModel vm)
                await vm.LoadAsync();
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Fehler", ex.ToString(), "OK");
        }
    }
}
