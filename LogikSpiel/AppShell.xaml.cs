#nullable enable
using LogikSpiel.View;

namespace LogikSpiel;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider sp)
    {
        InitializeComponent();

        // ✅ Home Tab / Startseite über DI bauen
        Items.Add(new TabBar
        {
            Items =
            {
                new ShellContent
                {
                    Title = "Home",
                    Route = "Main",
                    Icon = "applogo.png",
                    ContentTemplate = new DataTemplate(() => sp.GetRequiredService<MainPage>())
                }
            }
        });

        // ✅ Routes
        Routing.RegisterRoute(nameof(GameMapPage), typeof(GameMapPage));
        Routing.RegisterRoute(nameof(LearnPage), typeof(LearnPage));
        Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        Routing.RegisterRoute(nameof(GameHostPage), typeof(GameHostPage));
        Routing.RegisterRoute(nameof(PuzzlePage), typeof(PuzzlePage));
        Routing.RegisterRoute(nameof(PascalTrianglePage), typeof(PascalTrianglePage));
        Routing.RegisterRoute(nameof(MathCrossPage), typeof(MathCrossPage));
        Routing.RegisterRoute(nameof(MathHangmanPage), typeof(MathHangmanPage));
        Routing.RegisterRoute(nameof(NumberRainPage), typeof(NumberRainPage));
        Routing.RegisterRoute(nameof(CompleteSequencePage), typeof(CompleteSequencePage));
        Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
    }
}
