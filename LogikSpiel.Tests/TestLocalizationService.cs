namespace LogikSpiel.Services.Localization;

public static class LocalizationService
{
    public static event EventHandler? CultureChanged;

    public static string GetString(string key) => key;

    public static string Format(string key, params object[] args)
        => args.Length == 0 ? key : $"{key}:{string.Join(',', args)}";

    public static string GetDifficultyLabel(string difficultyKey) => difficultyKey;

    public static void RaiseCultureChangedForTests()
        => CultureChanged?.Invoke(null, EventArgs.Empty);
}
