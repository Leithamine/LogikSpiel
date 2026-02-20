namespace LogikSpiel.Services.Localization;

public static class LocalizationService
{
    public static string GetString(string key) => key;
    public static string Format(string key, params object[] args) => $"{key}:{string.Join(',', args)}";
}
