using System.Globalization;
using System.Resources;

namespace LogikSpiel.Resources.Strings;

public static class AppResources
{
    public const string Common_LevelFormat = nameof(Common_LevelFormat);
    public const string MathHangman_ExamplesFormat = nameof(MathHangman_ExamplesFormat);
    public const string Profile_AgeFormat = nameof(Profile_AgeFormat);
    public const string Profile_IdFormat = nameof(Profile_IdFormat);

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
