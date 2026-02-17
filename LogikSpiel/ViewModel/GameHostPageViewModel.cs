using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.ViewModel;

public sealed class GameHostPageViewModel : ObservableObject
{
    private readonly IGameCatalogService _catalog;
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    public string GameId { get; private set; } = "";
    public string DifficultyKey { get; private set; } = "normal";

    private int _levelNumber = 1;
    public int LevelNumber { get => _levelNumber; private set { if (!SetProperty(ref _levelNumber, value)) return; OnPropertyChanged(nameof(Title)); } }

    private GameDefinition? _game;
    public GameDefinition? Game { get => _game; private set { if (!SetProperty(ref _game, value)) return; OnPropertyChanged(nameof(Title)); } }

    public string Title => Game is null
        ? LocalizationService.GetString("GameHost_TitleDefault")
        : LocalizationService.Format("GameHost_TitleFormat", Game.Title, Name(DifficultyKey), LevelNumber);

    public AsyncCommand BackCommand { get; }
    public AsyncCommand RulesCommand { get; }
    public AsyncCommand SolveCommand { get; }

    public GameHostPageViewModel(
        IGameCatalogService catalog,
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav)
    {
        _catalog = catalog;
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;

        BackCommand = new AsyncCommand(ConfirmBackAsync);

        RulesCommand = new AsyncCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(GameId)) return;
            await _nav.GoToAsync("Learn", new Dictionary<string, object> { ["gameId"] = GameId });
        });

        SolveCommand = new AsyncCommand(async () =>
        {
            if (Game is null) return;

            var spec = new LevelSpec(GameId, DifficultyKey, LevelNumber, 0);
            var progress = await _progressStore.LoadAsync();
            progress.MarkCompleted(spec);
            await _progressStore.SaveAsync(progress); // WICHTIG: Speichern!

            // Prüfe ob nächstes Level existiert
            var maxLevel = Game?.LevelCount ?? 10000;
            if (LevelNumber >= maxLevel)
            {
                await _dialog.AlertAsync(
                    LocalizationService.GetString("GameHost_CompleteTitle"),
                    LocalizationService.GetString("GameHost_CompleteMessage"),
                    LocalizationService.GetString("Common_Ok"));
                await _nav.GoBackAsync();
                return;
            }

            LevelNumber++;
        });
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = gameId;
        DifficultyKey = difficulty;
        LevelNumber = Math.Max(1, level);

        Game = await _catalog.GetGameAsync(GameId);
        OnPropertyChanged(nameof(Title));
    }

    private static string Name(string key) => LocalizationService.GetDifficultyLabel(key);

    private async Task ConfirmBackAsync()
    {
        bool leave = await _dialog.ConfirmAsync(
            LocalizationService.GetString("Common_Back"),
            LocalizationService.GetString("Common_LeavePuzzlePrompt"),
            LocalizationService.GetString("Common_Yes"),
            LocalizationService.GetString("Common_No"));
        if (!leave) return;
        await _nav.GoBackAsync();
    }
}
