using LogikSpiel.Core;

namespace LogikSpiel.ViewModel;

public sealed class DigitInputViewModel : ObservableObject
{
    private string _digit = "";
    public string Digit
    {
        get => _digit;
        set
        {
            // Nur eine Ziffer erlauben
            var newValue = value?.Trim() ?? "";
            if (newValue.Length > 1)
                newValue = newValue[^1..]; // Nur letzte Ziffer
            
            if (SetProperty(ref _digit, newValue))
            {
                System.Diagnostics.Debug.WriteLine($"[DigitInputViewModel] Index={Index}, Digit changed to: '{_digit}'");
            }
        }
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetProperty(ref _isLocked, value))
            {
                System.Diagnostics.Debug.WriteLine($"[DigitInputViewModel] Index={Index}, IsLocked changed to: {_isLocked}");
            }
        }
    }

    public int Index { get; set; }

    public void Clear()
    {
        Digit = "";
        IsLocked = false;
    }
}
