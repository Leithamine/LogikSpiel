#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    // parameter: "TrueColor|FalseColor"
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value as bool? ?? false;

        if (parameter is string s && s.Contains('|'))
        {
            var parts = s.Split('|', 2, StringSplitOptions.TrimEntries);
            var trueColor = Color.FromArgb(parts[0]);
            var falseColor = Color.FromArgb(parts[1]);
            return flag ? trueColor : falseColor;
        }

        return flag ? Colors.White : Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
