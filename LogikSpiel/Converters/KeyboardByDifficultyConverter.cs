#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using LogikSpiel.ViewModel;

namespace LogikSpiel.Converters;

/// <summary>
/// Chooses a keyboard layout based on the current difficulty settings.
/// </summary>
public sealed class KeyboardByDifficultyConverter : IValueConverter
{
    /// <summary>
    /// Returns a numeric keyboard when negatives or decimals are allowed,
    /// otherwise a telephone keypad for simpler input.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // The view model exposes which kinds of input should be allowed.
        if (value is MathCrossPageViewModel vm)
        {
            // Hard/Master brauchen Minus und/oder Dezimal -> Numeric
            if (vm.AllowNegativeInput || vm.AllowDecimalInput)
                return Keyboard.Numeric;

            // Easy/Normal -> Telefon-Pad
            return Keyboard.Telephone;
        }

        // Fallback to numeric if the binding is missing or unexpected.
        return Keyboard.Numeric;
    }

    /// <summary>
    /// Convert-back is not supported because keyboard selection is one-way.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
