#nullable enable
using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LogikSpiel.Converters;

/// <summary>Invertiert Boolean (true -> false, false -> true)</summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}