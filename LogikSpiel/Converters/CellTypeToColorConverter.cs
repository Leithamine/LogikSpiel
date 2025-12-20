using System;
using System.Globalization;
using LogikSpiel.Model;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.Converters;

public class CellTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CellType cellType)
            return Colors.LightGray;

        // Parameter kann IsGiven sein (aber wir haben keinen direkten Zugriff hier)
        // Daher: Simplere Logik

        return cellType switch
        {
            CellType.Empty => Colors.Transparent,
            CellType.Equals => Color.FromArgb("#E8F4F8"),  // Hellblau für "="
            CellType.Number => Colors.White,
            CellType.Operator => Color.FromArgb("#FFF9E6"), // Hellgelb für Operatoren
            _ => Colors.White
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}