using System.Globalization;
using System.Resources;

namespace LogikSpiel.Resources.Strings;

public static class AppResources
{
    private static readonly ResourceManager _resourceManager =
        new("LogikSpiel.Resources.Strings.AppResources", typeof(AppResources).Assembly);

    public static ResourceManager ResourceManager => _resourceManager;

    public static string Common_LevelFormat => GetString(nameof(Common_LevelFormat));
    public static string MathHangman_ExamplesFormat => GetString(nameof(MathHangman_ExamplesFormat));
    public static string Profile_AgeFormat => GetString(nameof(Profile_AgeFormat));
    public static string Profile_IdFormat => GetString(nameof(Profile_IdFormat));

    private static string GetString(string key)
        => _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
