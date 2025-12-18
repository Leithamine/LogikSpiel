using LogikSpiel.Services;
using LogikSpiel.View;

namespace LogikSpiel;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // Routen registrieren
        Routing.RegisterRoute("GameMap", typeof(GameMapPage));
        Routing.RegisterRoute("Learn", typeof(LearnPage));
        Routing.RegisterRoute("Profile", typeof(ProfilePage));
        Routing.RegisterRoute("GameHost", typeof(GameHostPage));
        Routing.RegisterRoute("Puzzle", typeof(PuzzlePage));
        Routing.RegisterRoute(nameof(UnlockSuccessPage), typeof(UnlockSuccessPage));
    }
}