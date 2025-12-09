using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameHostPage : ContentPage, IQueryAttributable
{
    public GameHostPage() : this(AppServices.Get<GameHostPageViewModel>()) { }

    public GameHostPage(GameHostPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not GameHostPageViewModel vm) return;
        if (query.TryGetValue("spec", out var specObj) && specObj is LevelSpec spec)
            vm.Spec = spec;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (BindingContext is GameHostPageViewModel vm)
                await vm.LoadMapAsync();
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Fehler", ex.ToString(), "OK");
        }
    }
}
