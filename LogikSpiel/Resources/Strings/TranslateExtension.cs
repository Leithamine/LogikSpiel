using System.Globalization;
using System.Reflection;
using Microsoft.Maui.Controls.Xaml;
namespace LogikSpiel.Resources.Strings;

[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
            return string.Empty;

        var value = AppResources.ResourceManager.GetString(Key, CultureInfo.CurrentUICulture);
        return value ?? Key;
    }
}
