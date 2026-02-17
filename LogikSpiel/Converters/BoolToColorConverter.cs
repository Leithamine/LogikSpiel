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
            // Parse the two color strings from the parameter.
            var parts = s.Split('|', 2, StringSplitOptions.TrimEntries);
            var trueColor = Color.FromArgb(parts[0]);
            var falseColor = Color.FromArgb(parts[1]);
            // Return the matching color based on the boolean flag.
            return flag ? trueColor : falseColor;
        }

        // Fallback colors when no parameter was provided.
        return flag ? Colors.White : Colors.Transparent;
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
