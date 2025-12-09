using LogikSpiel.Core;
using LogikSpiel.Model;

namespace LogikSpiel.ViewModel;

public sealed class GameTileItemViewModel : ObservableObject
{
    public GameTile Model { get; }

    public string Title => Model.Title;
    public string Image => Model.Image;
    public string Route => Model.Route;

    public LevelSpec Spec => new(Model.GameId, Model.LevelNumber, Model.Seed);

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (!SetProperty(ref _isLocked, value)) return;
            OnPropertyChanged(nameof(IsPlayable));
        }
    }

    private bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    private int? _bestScore;
    public int? BestScore
    {
        get => _bestScore;
        set => SetProperty(ref _bestScore, value);
    }

    public bool IsPlayable => !IsLocked;

    public GameTileItemViewModel(GameTile model) => Model = model;
}
