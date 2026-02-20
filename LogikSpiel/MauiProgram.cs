using LogikSpiel.Services;
using LogikSpiel.Services.MathHangman;
using LogikSpiel.Services.NumberRain;
using LogikSpiel.View;
using LogikSpiel.ViewModel;
using SkiaSharp.Views.Maui.Controls.Hosting;
using Microsoft.Extensions.DependencyInjection;

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
        builder.Services.AddSingleton<ILockRiddleGeneratorService, LockRiddleGeneratorService>();
        builder.Services.AddSingleton<IRiddleStateStore, RiddleStateStore>();
        builder.Services.AddSingleton<PascalTriangleGeneratorService>();
        builder.Services.AddSingleton<MathCrossGeneratorService>();
        builder.Services.AddSingleton<IMathHangmanService, MathHangmanService>();
        builder.Services.AddSingleton<NumberRainQuestGeneratorService>();
        builder.Services.AddSingleton<CompleteSequenceGeneratorService>();

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
        builder.Services.AddTransient<CompleteSequencePageViewModel>();

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
        builder.Services.AddTransient<CompleteSequencePage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();
        ValidateServiceRegistrations(app.Services);

        return app;
    }

    private static void ValidateServiceRegistrations(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Core services
        sp.GetRequiredService<IGameCatalogService>();
        sp.GetRequiredService<IUserProfileService>();
        sp.GetRequiredService<INavigationService>();
        sp.GetRequiredService<IDialogService>();
        sp.GetRequiredService<IGameProgressStore>();
        sp.GetRequiredService<ILockRiddleGeneratorService>();
        sp.GetRequiredService<IRiddleStateStore>();

        // ViewModels used by Shell routes
        sp.GetRequiredService<MainPageViewModel>();
        sp.GetRequiredService<GameMapPageViewModel>();
        sp.GetRequiredService<ProfileViewModel>();
        sp.GetRequiredService<PuzzlePageViewModel>();

        // Pages used by Shell routes
        sp.GetRequiredService<MainPage>();
        sp.GetRequiredService<GameMapPage>();
        sp.GetRequiredService<ProfilePage>();
        sp.GetRequiredService<PuzzlePage>();
    }
}
