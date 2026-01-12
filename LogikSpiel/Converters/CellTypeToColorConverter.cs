using System;
using System.Globalization;
using LogikSpiel.Model;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.Converters;

/// <summary>
/// Converts a <see cref="CellType"/> value into a background <see cref="Color"/>.
/// </summary>
public class CellTypeToColorConverter : IValueConverter
{
    /// <summary>
    /// Maps the given cell type to a specific color used by the UI.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Return a default color when the binding value is missing or invalid.
        if (value is not CellType cellType)
            return Colors.LightGray;

        // Parameter kann IsGiven sein (aber wir haben keinen direkten Zugriff hier)
        // Daher: Simplere Logik

        return cellType switch
        {
            // Empty cells should be transparent (no background).
            CellType.Empty => Colors.Transparent,
            // "=" cells get a light blue to make them stand out.
            CellType.Equals => Color.FromArgb("#E8F4F8"),  // Hellblau für "="
            // Numbers are plain white.
            CellType.Number => Colors.White,
            // Operators have a light yellow background.
            CellType.Operator => Color.FromArgb("#FFF9E6"), // Hellgelb für Operatoren
            // Fallback for any unknown type.
            _ => Colors.White
        };
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
