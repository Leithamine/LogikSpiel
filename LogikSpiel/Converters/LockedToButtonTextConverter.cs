using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace LogikSpiel.Converters;

public class LockedToButtonTextConverter : IValueConverter
{
    // value: bool IsLocked
    // parameter optional: "UnlockedText|LockedText"
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool locked = value is bool b && b;

        if (parameter is string p && p.Contains("|"))
        {
            var parts = p.Split('|');
            if (parts.Length == 2)
                return locked ? parts[1] : parts[0];
        }

        return locked ? "🔒 Gesperrt" : "🔓 Öffnen";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
