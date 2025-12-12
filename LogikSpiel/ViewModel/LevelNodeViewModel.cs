using LogikSpiel.Core;
using LogikSpiel.Model;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.ViewModel;

public sealed class LevelNodeViewModel : ObservableObject
{
    public LevelSpec Spec { get; }
    public int LevelNumber => Spec.LevelNumber;

    // Performance: Werte, die sich nie ändern, brauchen kein Observable Property
    public bool ShowConnector { get; }
    public double YOffset { get; }

    // Position für das AbsoluteLayout
    private Rect _bounds;
    public Rect Bounds { get => _bounds; set => SetProperty(ref _bounds, value); }

    // Status-Eigenschaften (ändern sich zur Laufzeit)
    private bool _isUnlocked;
    public bool IsUnlocked { get => _isUnlocked; set => SetProperty(ref _isUnlocked, value); }

    private bool _isCompleted;
    public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }

    private bool _isCurrent;
    public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }

    // Konstruktor: Akzeptiert alle Parameter für den GameMapPageViewModel
    public LevelNodeViewModel(LevelSpec spec, bool showConnector = false, double yOffset = 0)
    {
        Spec = spec;
        ShowConnector = showConnector;
        YOffset = yOffset;
        // Standard-Bounds setzen, um Null-Referenzen zu vermeiden
        _bounds = new Rect(0.5, 0.1, 60, 60);
    }
}