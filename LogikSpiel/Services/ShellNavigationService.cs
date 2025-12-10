namespace LogikSpiel.Services;

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
        => Shell.Current.GoToAsync(route, parameters ?? new Dictionary<string, object>());

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
