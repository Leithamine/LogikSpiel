using LogikSpiel.Core;

namespace LogikSpiel.ViewModel;

public sealed class DifficultyOptionViewModel : ObservableObject
{
    public string Key { get; }
    public string Name { get; }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    public DifficultyOptionViewModel(string key, string name)
    {
        Key = key;
        Name = name;
    }
}
