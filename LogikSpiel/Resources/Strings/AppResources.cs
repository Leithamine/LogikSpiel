using System.Resources;

namespace LogikSpiel.Resources.Strings;

public static class AppResources
{
    private static readonly ResourceManager _resourceManager =
        new("LogikSpiel.Resources.Strings.AppResources", typeof(AppResources).Assembly);

    public static ResourceManager ResourceManager => _resourceManager;
}
