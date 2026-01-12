using System.Globalization;

namespace LogikSpiel.Converters;

/// <summary>
/// Returns true when the bound string is not null/empty/whitespace.
/// </summary>
public sealed class StringNotNullOrEmptyConverter : IValueConverter
{
    /// <summary>
    /// Checks if the provided value is a non-empty string.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s);

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when at least one slot string contains a digit or non-empty value.
/// </summary>
public sealed class SlotsHasDigitsConverter : IValueConverter
{
    /// <summary>
    /// Determines if any slot entry is non-empty.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Look for any slot with content.
        if (value is IEnumerable<string> slots)
            return slots.Any(s => !string.IsNullOrWhiteSpace(s));

        // Default to false when no slot data is available.
        return false;
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns true when all slot strings are empty/whitespace.
/// </summary>
public sealed class SlotsHasNoDigitsConverter : IValueConverter
{
    /// <summary>
    /// Determines if all slot entries are empty.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Return true only if every slot has no content.
        if (value is IEnumerable<string> slots)
            return !slots.Any(s => !string.IsNullOrWhiteSpace(s));

        // If we have no data, treat it as empty.
        return true;
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Shows a bullet when the bound string is empty, otherwise the trimmed string.
/// </summary>
public sealed class StringOrBulletConverter : IValueConverter
{
    /// <summary>
    /// Returns the trimmed string when present, otherwise a bullet character.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Use a bullet placeholder when there is no input yet.
        return value is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : "•";
    }

    /// <summary>
    /// Convert-back is not supported for this one-way converter.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
