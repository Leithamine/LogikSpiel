using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class LearnViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly INavigationService _nav;

    private string? _gameId;
    public string? GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private string _title = "Regeln";
    public string Title { get => _title; private set => SetProperty(ref _title, value); }

    public ObservableCollection<string> Rules { get; } = new();

    public AsyncCommand BackCommand { get; }

    public LearnViewModel(IGameCatalogService catalog, INavigationService nav)
    {
        _catalog = catalog;
        _nav = nav;
        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());
    }

    public async Task LoadAsync()
    {
        Rules.Clear();

        if (string.IsNullOrWhiteSpace(GameId))
        {
            Title = "Regeln";
            Rules.Add("Keine Regeln gefunden.");
            return;
        }

        var game = await _catalog.GetGameAsync(GameId!);
        Title = game?.Title is null ? "Regeln" : $"Regeln – {game.Title}";

        if (game?.RulesText is { Count: > 0 } list)
        {
            foreach (var r in list) Rules.Add(r);
        }
        else
        {
            Rules.Add("Keine Regeln gefunden.");
        }
    }
}
