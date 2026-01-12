using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LogikSpiel.Converters;

/// <summary>
/// Converts a lock state into the localized button label shown in the UI.
/// </summary>
public class LockedToButtonTextConverter : IValueConverter
{
    /// <summary>
    /// Maps a boolean lock state to a display string. Optional parameter: "UnlockedText|LockedText".
    /// </summary>
    // value: bool IsLocked
    // parameter optional: "UnlockedText|LockedText"
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Interpret the value as a boolean lock flag.
        bool locked = value is bool b && b;

        if (parameter is string p && p.Contains("|"))
        {
            // Split custom label pair (Unlocked|Locked).
            var parts = p.Split('|');
            if (parts.Length == 2)
                return locked ? parts[1] : parts[0];
        }

        // Default labels when no parameter is supplied.
        return locked ? "🔒 Gesperrt" : "🔓 Öffnen";
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
