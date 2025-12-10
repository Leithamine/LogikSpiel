using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<MainPageViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainPageViewModel vm)
            await vm.EnsureLoadedAsync();
    }
}
