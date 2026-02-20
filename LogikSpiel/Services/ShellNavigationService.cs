using System.Diagnostics;

namespace LogikSpiel.Services;

public sealed class ShellNavigationService : INavigationService
{
    public async Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        try
        {
            if (Shell.Current is null)
                throw new InvalidOperationException("Shell.Current is null. Navigation is not available yet.");

            await Shell.Current.GoToAsync(route, parameters ?? new Dictionary<string, object>());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Navigation] GoToAsync failed for route '{route}': {ex}");
            throw;
        }
    }

    public async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current is null)
                throw new InvalidOperationException("Shell.Current is null. Back navigation is not available.");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Navigation] GoBackAsync failed: {ex}");
            throw;
        }
    }
}
