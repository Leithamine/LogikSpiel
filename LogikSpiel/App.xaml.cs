#nullable enable
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using LogikSpiel.View;
using LogikSpiel.ViewModel;

namespace LogikSpiel;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly IUserProfileService _userService;

    public App(IServiceProvider services)
    {
        LocalizationService.ApplySavedCulture();
        InitializeComponent();
        _services = services;
        _userService = services.GetRequiredService<IUserProfileService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
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

        Task.Run(async () =>
        {
            await Task.Delay(300);

            bool hasProfile = false;
            try
            {
                hasProfile = await _userService.HasProfileAsync();
            }
            catch
            {
                // falls DB/Storage in Release kurz braucht
                hasProfile = false;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (hasProfile)
                {
                    // ✅ Shell über DI holen (wichtig!)
                    window.Page = _services.GetRequiredService<AppShell>();
                }
                else
                {
                    // ✅ OnboardingPage über DI holen (damit VM injected wird)
                    window.Page = _services.GetRequiredService<OnboardingPage>();
                }
            });
        });

        return window;
    }
}
