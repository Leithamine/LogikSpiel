using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class LearnViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;

    private string? _gameId;
    public string? GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private GameDefinition? _game;
    public GameDefinition? Game
    {
        get => _game;
        private set
        {
            if (!SetProperty(ref _game, value)) return;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Rules)); // Wichtig: UI über neue Regeln informieren
        }
    }

    public string Title => Game?.Title ?? "Regeln";

    // FIX: Typen müssen übereinstimmen (List vs List)
    public IReadOnlyList<string> Rules => Game?.RulesText ?? new List<string>();

    public AsyncCommand BackCommand { get; }

    public LearnViewModel(IGameCatalogService catalog, INavigationService nav)
    {
        _catalog = catalog;
        _nav = nav;
        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;
        Game = await _catalog.GetGameAsync(GameId!);
    }
}