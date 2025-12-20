#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using LogikSpiel.ViewModel;

namespace LogikSpiel.Converters;

public sealed class KeyboardByDifficultyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MathCrossPageViewModel vm)
        {
            // Hard/Master brauchen Minus und/oder Dezimal -> Numeric
            if (vm.AllowNegativeInput || vm.AllowDecimalInput)
                return Keyboard.Numeric;

            // Easy/Normal -> Telefon-Pad
            return Keyboard.Telephone;
        }

        return Keyboard.Numeric;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
