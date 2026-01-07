using Microsoft.Maui.Controls;
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

        return new Binding($"[{Key}]", source: LocalizationResourceManager.Instance, Mode = BindingMode.OneWay);
    }
}
