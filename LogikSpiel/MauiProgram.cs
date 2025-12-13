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
        //SQLitePCL.Batteries_V2.Init();
        // Services
        builder.Services.AddSingleton<IGameCatalogService>(sp => new AppPackageGameCatalogService("games.json"));
        builder.Services.AddSingleton<IUserProfileService, SqliteUserProfileService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<IGameProgressStore, SqliteGameProgressStore>();
        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<LearnViewModel>();
        builder.Services.AddTransient<GameHostPageViewModel>();
        builder.Services.AddTransient<GameMapPageViewModel>();
        builder.Services.AddTransient<PuzzlePageViewModel>();

        // Pages
        builder.Services.AddTransient<LearnPage>();
        builder.Services.AddTransient<GameHostPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<ProfilePage>();
        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
