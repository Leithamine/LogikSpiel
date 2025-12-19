using LogikSpiel.View;

namespace LogikSpiel;

public partial class AppShell : Shell
{
    public const string PascalTriangleRoute = "pascaltriangle";

    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        Routing.RegisterRoute("GameMap", typeof(GameMapPage));
        Routing.RegisterRoute("Learn", typeof(LearnPage));
        Routing.RegisterRoute("Profile", typeof(ProfilePage));
        Routing.RegisterRoute("GameHost", typeof(GameHostPage));
        Routing.RegisterRoute("Puzzle", typeof(PuzzlePage));
        Routing.RegisterRoute("pascaltriangle", typeof(PascalTrianglePage));
    }
}
