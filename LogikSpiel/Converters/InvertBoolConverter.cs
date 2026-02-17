#nullable enable
using System.Globalization;

namespace LogikSpiel.Converters;

/// <summary>
/// Inverts boolean values for binding scenarios (true -> false, false -> true).
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    /// <summary>
    /// Returns the inverted boolean or false for non-boolean values.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    /// <summary>
    /// Convert-back performs the same inversion to keep symmetry.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
