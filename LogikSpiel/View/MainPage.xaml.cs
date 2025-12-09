using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MainPage : ContentPage
{
    private double _lastWidth;

    public MainPage(MainPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (BindingContext is MainPageViewModel vm)
                await vm.EnsureInitializedAsync();
        }
        catch (Exception ex)
        {
            // zeigt dir endlich die echte Ursache statt Debugger.Break
            await this.DisplayAlertAsync("Startup-Fehler", ex.ToString(), "OK");
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Math.Abs(width - _lastWidth) < 1) return;
        _lastWidth = width;

        (BindingContext as MainPageViewModel)?.UpdateTileSpan(width);
    }
}
