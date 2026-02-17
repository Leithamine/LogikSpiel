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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            bool hasProfile = false;

            try
            {
                await Task.Delay(300, cts.Token);

                hasProfile = await _userService.HasProfileAsync();
            }
            catch (OperationCanceledException)
            {
                hasProfile = false;
            }
            catch
            {
                hasProfile = false;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (hasProfile)
                {
                    window.Page = _services.GetRequiredService<AppShell>();
                }
                else
                {
                    window.Page = _services.GetRequiredService<OnboardingPage>();
                }
            });
        });

        return window;
    }
}
