using LogikSpiel.Services;
using LogikSpiel.View;
using LogikSpiel.ViewModel; // Wichtig für OnboardingViewModel

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
        // 1. Wir erstellen SOFORT ein Fenster mit einem Lade-Kreisel.
        // Das verhindert, dass die App beim Start einfriert (weißer Bildschirm).
        var loadingPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#BB86FC"), // Dein Lila
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

        // 2. Wir prüfen die Datenbank asynchron im Hintergrund (Fire & Forget)
        Task.Run(async () =>
        {
            // Kleine Pause, damit die App Zeit hat, das Fenster sauber aufzubauen
            await Task.Delay(200);

            // Datenbank abfragen
            bool hasProfile = await _userService.HasProfileAsync();

            // 3. UI auf dem Haupt-Thread aktualisieren
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (hasProfile)
                {
                    // User existiert -> Haupt-App (Shell) laden
                    window.Page = new AppShell(_services);
                }
                else
                {
                    // Kein User -> Onboarding laden
                    var onboardingVM = _services.GetRequiredService<OnboardingViewModel>();
                    window.Page = new OnboardingPage
                    {
                        BindingContext = onboardingVM
                    };
                }
            });
        });

        return window;
    }
}