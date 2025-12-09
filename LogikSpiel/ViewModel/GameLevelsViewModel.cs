using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public sealed class GameLevelsViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly INavigationService _nav;

    public ObservableCollection<DifficultyOptionViewModel> Difficulties { get; } = new();
    public ObservableCollection<LevelNodeViewModel> LevelNodes { get; } = new();

    private string? _gameId;
    public string? GameId { get => _gameId; set => SetProperty(ref _gameId, value); }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game?.Title ?? "Level wählen";

    private string _selectedDifficultyKey = "normal";
    public string SelectedDifficultyKey
    {
        get => _selectedDifficultyKey;
        set { if (!SetProperty(ref _selectedDifficultyKey, value)) return; _ = ReloadLevelsAsync(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            SelectLevelCommand.RaiseCanExecuteChanged();
            LearnCommand.RaiseCanExecuteChanged();
            BackCommand.RaiseCanExecuteChanged();
        }
    }

    public AsyncCommand<DifficultyOptionViewModel> SelectDifficultyCommand { get; }
    public AsyncCommand<LevelNodeViewModel> SelectLevelCommand { get; }
    public AsyncCommand LearnCommand { get; }
    public AsyncCommand BackCommand { get; }

    public GameLevelsViewModel(IGameCatalogService catalog, IGameProgressStore progressStore, INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _nav = nav;

        Difficulties.Add(new("easy", "Einfach"));
        Difficulties.Add(new("normal", "Normal"));
        Difficulties.Add(new("hard", "Schwer"));
        Difficulties.Add(new("complex", "Kompliziert"));
        Difficulties.Add(new("master", "Master"));
        Difficulties.Add(new("god", "Gott"));
        MarkSelected("normal");

        SelectDifficultyCommand = new AsyncCommand<DifficultyOptionViewModel>(async d =>
        {
            if (d is null) return;
            MarkSelected(d.Key);
            SelectedDifficultyKey = d.Key;
            await Task.CompletedTask;
        });

        SelectLevelCommand = new AsyncCommand<LevelNodeViewModel>(async node =>
        {
            if (node is null || !node.IsUnlocked) return;
            await _nav.GoToAsync("GameHost", new Dictionary<string, object> { ["spec"] = node.Spec });
        }, node => !IsBusy && node is not null);

        LearnCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _nav.GoToAsync("Learn", new Dictionary<string, object> { ["gameId"] = GameId! });
        }, () => !IsBusy && !string.IsNullOrWhiteSpace(GameId));

        BackCommand = new AsyncCommand(() => _nav.GoBackAsync(), () => !IsBusy);
    }

    private void MarkSelected(string key)
    {
        foreach (var d in Difficulties) d.IsSelected = d.Key == key;
    }

    public async Task LoadAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(GameId)) return;

        IsBusy = true;
        try
        {
            Game = await _catalog.GetGameAsync(GameId!);
            await ReloadLevelsAsync();
        }
        finally { IsBusy = false; }
    }

    public async Task ReloadLevelsAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId)) return;

        var progress = await _progressStore.LoadAsync();
        var specs = await _catalog.GetLevelsAsync(GameId!, SelectedDifficultyKey);

        LevelNodes.Clear();

        for (int i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var node = new LevelNodeViewModel(spec, showConnector: i < specs.Count - 1);

            node.IsCompleted = progress.IsCompleted(spec);

            if (spec.LevelNumber == 1) node.IsUnlocked = true;
            else
            {
                var prev = new LevelSpec(spec.GameId, spec.DifficultyKey, spec.LevelNumber - 1, spec.Seed - 1);
                node.IsUnlocked = progress.IsCompleted(prev);
            }

            node.IsCurrent = spec.LevelNumber == 1 && node.IsUnlocked && !node.IsCompleted;

            LevelNodes.Add(node);
        }

        // Current Marker: erstes nicht-abgeschlossenes, aber unlocked
        var current = LevelNodes.FirstOrDefault(n => n.IsUnlocked && !n.IsCompleted) ?? LevelNodes.FirstOrDefault();
        if (current is not null)
        {
            foreach (var n in LevelNodes) n.IsCurrent = false;
            current.IsCurrent = true;
        }
    }
}
