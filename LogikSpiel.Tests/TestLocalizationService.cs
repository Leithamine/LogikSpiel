namespace LogikSpiel.Services.Localization;

public static class LocalizationService
{
    public static string GetString(string key) => key;

    public static string Format(string key, params object[] args)
        => args.Length == 0 ? key : $"{key}:{string.Join(',', args)}";
}
