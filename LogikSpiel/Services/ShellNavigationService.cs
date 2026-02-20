using System.Diagnostics;

namespace LogikSpiel.Services;

public sealed class ShellNavigationService : INavigationService
{
    public async Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        try
        {
            await Shell.Current.GoToAsync(route, parameters ?? new Dictionary<string, object>());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NAVIGATION ERROR: route='{route}', ex={ex}");
            throw;
        }
    }

    public async Task GoBackAsync()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NAVIGATION ERROR: route='..', ex={ex}");
            throw;
        }
    }
}
