using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameMapPage : ContentPage, IQueryAttributable
{
    public GameMapPage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<GameMapPageViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not GameMapPageViewModel vm) return;
        if (query.TryGetValue("gameId", out var idObj) && idObj is string id)
            vm.GameId = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (BindingContext is GameMapPageViewModel vm)
            {
                // beim ersten Mal Load, beim Zurückkommen Refresh
                await vm.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Fehler", ex.ToString(), "OK");
        }
    }
}
