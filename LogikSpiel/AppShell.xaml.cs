using LogikSpiel.View;

namespace LogikSpiel;

public partial class AppShell : Shell
{
    public const string PascalTriangleRoute = "pascaltriangle";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        Routing.RegisterRoute("GameMapPage", typeof(GameMapPage));
        Routing.RegisterRoute("LearnPage", typeof(LearnPage));
        Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
        Routing.RegisterRoute("GameHost", typeof(GameHostPage));
        Routing.RegisterRoute("PuzzlePage", typeof(PuzzlePage));
        Routing.RegisterRoute("PascalTrianglePage", typeof(PascalTrianglePage));
        Routing.RegisterRoute("MathCrossPage", typeof(MathCrossPage));
        Routing.RegisterRoute("MathHangmanPage", typeof(MathHangmanPage));
        Routing.RegisterRoute("OnboardingPage", typeof(View.OnboardingPage));
    }
}
