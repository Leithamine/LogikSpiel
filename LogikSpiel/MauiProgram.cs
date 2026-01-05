using LogikSpiel.Services;
using LogikSpiel.Services.MathHangman;
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

        SQLitePCL.Batteries_V2.Init();

        // Services
        builder.Services.AddSingleton<IGameCatalogService>(sp => new AppPackageGameCatalogService("games.json"));
        builder.Services.AddSingleton<IUserProfileService, SqliteUserProfileService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<IGameProgressStore, SqliteGameProgressStore>();
        builder.Services.AddSingleton<LockRiddleGeneratorService>();
        builder.Services.AddSingleton<IRiddleStateStore, RiddleStateStore>();
        builder.Services.AddSingleton<PascalTriangleGeneratorService>();
        builder.Services.AddSingleton<MathCrossGeneratorService>();
        builder.Services.AddSingleton<IMathHangmanService, MathHangmanService>();

        // ViewModels
        builder.Services.AddTransient<MainPageViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<LearnPageViewModel>();
        builder.Services.AddTransient<GameHostPageViewModel>();
        builder.Services.AddTransient<GameMapPageViewModel>();
        builder.Services.AddTransient<PuzzlePageViewModel>();
        builder.Services.AddTransient<PascalTrianglePageViewModel>();
        builder.Services.AddTransient<MathCrossPageViewModel>();
        builder.Services.AddTransient<MathHangmanPageViewModel>();
        builder.Services.AddTransient<NumberRainPageViewModel>();

        // Pages (✅ ALLE Pages registrieren, die Shell/DI bauen soll)
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<GameMapPage>();

        builder.Services.AddTransient<LearnPage>();
        builder.Services.AddTransient<GameHostPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<PuzzlePage>();
        builder.Services.AddTransient<PascalTrianglePage>();
        builder.Services.AddTransient<MathCrossPage>();
        builder.Services.AddTransient<MathHangmanPage>();
        builder.Services.AddTransient<NumberRainPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
