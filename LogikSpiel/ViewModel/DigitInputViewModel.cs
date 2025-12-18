using LogikSpiel.Core;

namespace LogikSpiel.ViewModel;

public class DigitInputViewModel : ObservableObject
{
    public int Index { get; set; }

    private string _digit = "";
    public string Digit
    {
        get => _digit;
        set => SetProperty(ref _digit, value);
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set => SetProperty(ref _isLocked, value);
    }
}
