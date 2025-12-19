using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b) return Colors.Transparent;
        if (parameter is not string p) return Colors.Transparent;

        var parts = p.Split(':');
        if (parts.Length != 2) return Colors.Transparent;

        var hex = b ? parts[0] : parts[1];
        try { return Color.FromArgb(hex); }
        catch { return Colors.Transparent; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
