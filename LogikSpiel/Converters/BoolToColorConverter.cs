#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.Converters;

/// <summary>
/// Converts a boolean value into a <see cref="Color"/> for UI bindings.
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean into a color. Optional parameter: "TrueColor|FalseColor".
    /// </summary>
    // parameter: "TrueColor|FalseColor"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Safely unwrap nullable booleans; default to false if null or not a bool.
        bool flag = value as bool? ?? false;

        if (parameter is string s && s.Contains('|'))
        {
            var parts = s.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && TryParseColor(parts[0], out var trueColor)
                && TryParseColor(parts[1], out var falseColor))
            {
                return flag ? trueColor : falseColor;
            }
        }

        // Fallback colors when no parameter was provided.
        return flag ? Colors.White : Colors.Transparent;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            color = Color.FromArgb(value);
            return true;
        }
        catch
        {
            color = Colors.Transparent;
            return false;
        }
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
