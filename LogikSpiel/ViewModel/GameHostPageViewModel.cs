using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class GameHostPageViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    private LevelSpec? _spec;
    public LevelSpec? Spec
    {
        get => _spec;
        set
        {
            if (!SetProperty(ref _spec, value)) return;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => Spec is null
        ? "Game"
        : $"{Spec.GameId} – Level {Spec.LevelNumber}";

    public AsyncCommand BackCommand { get; }

    public GameHostPageViewModel(INavigationService nav)
    {
        _nav = nav;
        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
    }
}
