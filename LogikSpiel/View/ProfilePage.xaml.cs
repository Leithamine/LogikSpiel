using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Lädt die Daten (und Coins) jedes Mal neu, wenn die Seite angezeigt wird
        await _vm.LoadAsync();
    }
}