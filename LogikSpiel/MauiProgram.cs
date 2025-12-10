using LogikSpiel.Services;
using LogikSpiel.View;
using LogikSpiel.ViewModel;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace LogikSpiel;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp();

        // Services
        builder.Services.AddSingleton<IGameCatalogService>(sp => new AppPackageGameCatalogService("games.json"));
        builder.Services.AddSingleton<IGameProgressStore, PreferencesGameProgressStore>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();

        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<GameLevelsViewModel>();
        builder.Services.AddTransient<LearnViewModel>();
        builder.Services.AddTransient<GameHostPageViewModel>();
        builder.Services.AddTransient<GameMapPageViewModel>();
        builder.Services.AddTransient<PuzzlePageViewModel>();

        // Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<GameLevelsPage>();
        builder.Services.AddTransient<LearnPage>();
        builder.Services.AddTransient<GameHostPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
