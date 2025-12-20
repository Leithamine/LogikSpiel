#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using LogikSpiel.Model;

namespace LogikSpiel.Converters;

public sealed class NotEmptyCellConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CellType t && t != CellType.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true if the cell is not empty (for visibility binding)
/// </summary>
public sealed class CellTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CellType cellType)
        {
            return cellType != CellType.Empty;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}