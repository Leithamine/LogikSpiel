using System;
using System.Globalization;
using LogikSpiel.Model;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.Converters;

/// <summary>
/// Converts a <see cref="MathCrossCell"/> into a background color based on resources.
/// </summary>
public class MathCellToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MathCrossCell cell)
            return Colors.Transparent;

        if (cell.Type == CellType.Empty)
            return Colors.Transparent;

        if (cell.IsGiven || cell.Type == CellType.Equals)
            return GetColorFromResource("C_MathCell_Fixed_Bg", Color.FromArgb("#313B4A"));

        return cell.Type switch
        {
            CellType.Number => GetColorFromResource("C_MathCell_Num_Bg", Color.FromArgb("#23445E")),
            CellType.Operator => GetColorFromResource("C_MathCell_Op_Bg", Color.FromArgb("#3A3556")),
            _ => Colors.Transparent
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static Color GetColorFromResource(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue(resourceKey, out var resourceValue)
            && resourceValue is Color color)
        {
            return color;
        }

        return fallbackColor;
    }
}
