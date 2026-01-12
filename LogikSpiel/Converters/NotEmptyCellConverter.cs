#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using LogikSpiel.Model;

namespace LogikSpiel.Converters;

/// <summary>
/// Returns true when a <see cref="CellType"/> is not empty.
/// </summary>
public sealed class NotEmptyCellConverter : IValueConverter
{
    /// <summary>
    /// Checks if the cell is any type other than <see cref="CellType.Empty"/>.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CellType t && t != CellType.Empty;

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the cell is not empty (for visibility binding)
/// </summary>
public sealed class CellTypeToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a cell type to a boolean used for visibility bindings.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Only non-empty cells should be visible.
        if (value is CellType cellType)
        {
            return cellType != CellType.Empty;
        }
        // Default to false when the binding value is missing.
        return false;
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
