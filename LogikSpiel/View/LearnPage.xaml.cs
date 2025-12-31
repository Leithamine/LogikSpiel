using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class LearnPage : ContentPage, IQueryAttributable
{
    public LearnPage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<LearnPageViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not LearnPageViewModel vm) return;
        if (query.TryGetValue("gameId", out var idObj) && idObj is string id)
            vm.GameId = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LearnPageViewModel vm)
            await vm.LoadAsync();
    }
}
