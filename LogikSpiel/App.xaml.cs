using LogikSpiel.Services;
using LogikSpiel.View;
using LogikSpiel.ViewModel;

namespace LogikSpiel;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly IUserProfileService _userService;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        _userService = services.GetRequiredService<IUserProfileService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // 1. Lade-Screen erstellen
        var loadingPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#BB86FC"),
            Content = new ActivityIndicator
            {
                IsRunning = true,
                Color = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Scale = 2
            }
        };

        var window = new Window(loadingPage);

        // 2. Prüfung im Hintergrund
        Task.Run(async () =>
        {
            // Kurze Pause, damit der Ladekreis sichtbar wird
            await Task.Delay(500);

            // Datenbank prüfen
            bool hasProfile = await _userService.HasProfileAsync();

            // 3. UI Update auf dem Haupt-Thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (hasProfile)
                {
                    // KORREKTUR: Wir übergeben _services wieder, da deine AppShell das verlangt
                    window.Page = new AppShell(_services);
                }
                else
                {
                    // ViewModel holen und übergeben
                    var onboardingVM = _services.GetRequiredService<OnboardingViewModel>();
                    window.Page = new OnboardingPage(onboardingVM);
                }
            });
        });

        return window;
    }
}