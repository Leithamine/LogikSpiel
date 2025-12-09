using LogikSpiel.Core;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class LearnViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;

    private string? _gameId;
    public string? GameId
    {
        get => _gameId;
        set => SetProperty(ref _gameId, value);
    }

    private string _title = "Regeln";
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    private string _rules = "";
    public string Rules
    {
        get => _rules;
        private set => SetProperty(ref _rules, value);
    }

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

        var game = await _catalog.GetGameAsync(GameId!);

        Title = game?.Title is null ? "Regeln" : $"Regeln – {game.Title}";

        if (game?.RulesText is { Count: > 0 } rules)
        {
            // hübsch als Bullet-Liste
            Rules = string.Join("\n\n", rules.Select(r => "• " + r.Trim()));
        }
        else
        {
            Rules = "Keine Regeln gefunden.";
        }
    }
}
