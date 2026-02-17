using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.Core;

public abstract class ObservableObject : INotifyPropertyChanged
{
    private readonly EventHandler _cultureChangedHandler;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected ObservableObject()
    {
        _cultureChangedHandler = HandleCultureChanged;
        LocalizationService.CultureChanged += _cultureChangedHandler;
    }

    ~ObservableObject()
    {
        LocalizationService.CultureChanged -= _cultureChangedHandler;
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
        => OnCultureChanged();

    protected virtual void OnCultureChanged()
        => OnPropertyChanged(string.Empty);

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(name);
        return true;
    }
}
