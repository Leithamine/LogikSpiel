using LogikSpiel.Services;
using LogikSpiel.View;

namespace LogikSpiel;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        Routing.RegisterRoute("GameLevels", typeof(LogikSpiel.View.GameLevelsPage));
        Routing.RegisterRoute("Learn", typeof(LogikSpiel.View.LearnPage));
        Routing.RegisterRoute("GameHost", typeof(LogikSpiel.View.GameHostPage));

        Items.Add(new ShellContent
        {
            Route = "Home",
            Content = services.GetRequiredService<MainPage>()
        });
    }
}
