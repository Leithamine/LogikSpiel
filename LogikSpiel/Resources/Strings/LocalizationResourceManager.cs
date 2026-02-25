using System.ComponentModel;
using System.Runtime.CompilerServices;
using LogikSpiel.Resources.Strings;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.Resources.Strings;

public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationResourceManager> _instance = new(() => new LocalizationResourceManager());

    public static LocalizationResourceManager Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationResourceManager()
    {
        LocalizationService.CultureChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    public string this[string key] => AppRessources.ResourceManager.GetString(key) ?? key;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
