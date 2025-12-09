using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class GameHostPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly INavigationService _nav;

    public ObservableCollection<LevelNodeViewModel> LevelNodes { get; } = new();

    private LevelSpec? _spec;
    public LevelSpec? Spec
    {
        get => _spec;
        set { if (!SetProperty(ref _spec, value)) return; OnPropertyChanged(nameof(Title)); }
    }

    public string Title => Spec is null
        ? "Game"
        : $"{Spec.GameId} – {Name(Spec.DifficultyKey)} – Level {Spec.LevelNumber}";

    public AsyncCommand BackCommand { get; }
    public AsyncCommand CompleteLevelCommand { get; }
    public AsyncCommand<LevelNodeViewModel> SelectLevelFromMapCommand { get; }

    public GameHostPageViewModel(IGameCatalogService catalog, IGameProgressStore progressStore, INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _nav = nav;

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        SelectLevelFromMapCommand = new AsyncCommand<LevelNodeViewModel>(async node =>
        {
            if (node is null || !node.IsUnlocked) return;
            Spec = node.Spec;
            await LoadMapAsync();
        });

        CompleteLevelCommand = new AsyncCommand(async () =>
        {
            if (Spec is null) return;

            var progress = await _progressStore.LoadAsync();
            progress.MarkCompleted(Spec);
            await _progressStore.SaveAsync(progress);

            await LoadMapAsync();

            var next = LevelNodes.FirstOrDefault(n => n.Spec.LevelNumber == Spec.LevelNumber + 1);
            if (next is not null && next.IsUnlocked)
                Spec = next.Spec;

            await LoadMapAsync();
        });
    }

    public async Task LoadMapAsync()
    {
        if (Spec is null) return;

        var progress = await _progressStore.LoadAsync();
        var specs = await _catalog.GetLevelsAsync(Spec.GameId, Spec.DifficultyKey);

        LevelNodes.Clear();

        for (int i = 0; i < specs.Count; i++)
        {
            var s = specs[i];
            var node = new LevelNodeViewModel(s, showConnector: i < specs.Count - 1);

            node.IsCompleted = progress.IsCompleted(s);

            if (s.LevelNumber == 1) node.IsUnlocked = true;
            else
            {
                var prev = new LevelSpec(s.GameId, s.DifficultyKey, s.LevelNumber - 1, s.Seed - 1);
                node.IsUnlocked = progress.IsCompleted(prev);
            }

            node.IsCurrent = Spec.LevelNumber == s.LevelNumber;

            LevelNodes.Add(node);
        }
    }

    private static string Name(string key) => key switch
    {
        "easy" => "Einfach",
        "normal" => "Normal",
        "hard" => "Schwer",
        "complex" => "Kompliziert",
        "master" => "Master",
        "god" => "Gott",
        _ => key
    };
}
