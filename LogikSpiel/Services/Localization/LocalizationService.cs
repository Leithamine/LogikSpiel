using System;
using System.Globalization;
using LogikSpiel.Resources.Strings;
using Microsoft.Maui.Storage;

namespace LogikSpiel.Services.Localization;

public static class LocalizationService
{
    public static event EventHandler? CultureChanged;
    private const string LanguagePreferenceKey = "APP_LANGUAGE";

    public static void ApplySavedCulture()
    {
        var cultureName = Preferences.Get(LanguagePreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(cultureName))
            return;

        SetCulture(cultureName, savePreference: false);
    }

    public static string GetCurrentCultureCode()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public static void SetCulture(string cultureName, bool savePreference = true)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return;

        var culture = CultureInfo.GetCultureInfo(cultureName);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (savePreference)
            Preferences.Set(LanguagePreferenceKey, cultureName);

        CultureChanged?.Invoke(null, EventArgs.Empty);
    }

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
