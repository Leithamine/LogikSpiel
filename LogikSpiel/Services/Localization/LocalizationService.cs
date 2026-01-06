using System.Globalization;
using LogikSpiel.Resources.Strings;

namespace LogikSpiel.Services.Localization;

public static class LocalizationService
{
    public static string GetString(string key)
        => AppResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, GetString(key), args);

    public static string GetDifficultyLabel(string difficultyKey) => difficultyKey switch
    {
        "easy" => GetString("Difficulty_Easy"),
        "normal" => GetString("Difficulty_Normal"),
        "hard" => GetString("Difficulty_Hard"),
        "master" => GetString("Difficulty_Master"),
        "complex" => GetString("Difficulty_Complex"),
        "god" => GetString("Difficulty_God"),
        _ => difficultyKey
    };
}
