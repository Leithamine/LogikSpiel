using LogikSpiel.Core;
using LogikSpiel.Model;

namespace LogikSpiel.ViewModel;

public sealed class LevelNodeViewModel : ObservableObject
{
    public LevelSpec Spec { get; }
    public int LevelNumber => Spec.LevelNumber;
    public bool ShowConnector { get; }

    private bool _isCompleted;
    public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }

    private bool _isUnlocked;
    public bool IsUnlocked { get => _isUnlocked; set => SetProperty(ref _isUnlocked, value); }

    private bool _isCurrent;
    public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }

    public LevelNodeViewModel(LevelSpec spec, bool showConnector)
    {
        Spec = spec;
        ShowConnector = showConnector;
    }
}
