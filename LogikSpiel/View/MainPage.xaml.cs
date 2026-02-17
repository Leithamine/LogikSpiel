using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _vm;

    public MainPage(MainPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainPageViewModel vm)
            _ = vm.EnsureLoadedAsync();
    }
}
