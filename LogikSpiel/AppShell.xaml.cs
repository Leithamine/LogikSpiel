using Microsoft.Extensions.DependencyInjection;
using LogikSpiel.View;

namespace LogikSpiel;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        Routing.RegisterRoute("GameLevels", typeof(GameLevelsPage));
        Routing.RegisterRoute("Learn", typeof(LearnPage));
        Routing.RegisterRoute("GameHost", typeof(GameHostPage));

        try
        {
            Items.Add(new ShellContent
            {
                Route = "Home",
                Content = services.GetRequiredService<MainPage>()
            });
        }
        catch (Exception ex)
        {
            Items.Add(new ShellContent
            {
                Route = "Error",
                Content = new ContentPage
                {
                    Content = new ScrollView
                    {
                        Content = new Label
                        {
                            Text = ex.ToString(),
                            Padding = 20
                        }
                    }
                }
            });
        }
    }
}

