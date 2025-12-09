using Microsoft.Extensions.DependencyInjection;

namespace LogikSpiel.Services;

public static class AppServices
{
    public static T Get<T>() where T : notnull
        => Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<T>();
}