using Microsoft.Extensions.DependencyInjection;

namespace LogikSpiel.Services;

public static class AppServices
{
    public static T Get<T>() where T : notnull
    {
        if (Application.Current?.Handler?.MauiContext?.Services is not { } services)
            throw new InvalidOperationException("Services not available. Ensure App is initialized.");
        return services.GetRequiredService<T>();
    }
}
