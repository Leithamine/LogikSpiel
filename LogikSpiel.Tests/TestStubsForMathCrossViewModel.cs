namespace LogikSpiel.Services.Localization;

public static class LocalizationService
{
    public static string GetString(string key) => key;
    public static string Format(string key, params object[] args) => key;
    public static string GetDifficultyLabel(string difficultyKey) => difficultyKey;
}

namespace LogikSpiel.View;

public sealed class GameMapPage;
