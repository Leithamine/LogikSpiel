#nullable enable
using System.Collections.ObjectModel;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.ViewModel;

public sealed class CompleteSequencePageViewModel : ObservableObject
{
    private readonly IGameProgressStore _progressStore;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IUserProfileService _userService;
    private readonly CompleteSequenceGeneratorService _generator;

    private CompleteSequencePuzzle? _puzzle;
    private UserProfile? _userProfile;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    public string GameId { get; private set; } = "complete_sequence";
    public string DifficultyKey { get; private set; } = "easy";

    private int _levelNumber = 1;
    public int LevelNumber
    {
        get => _levelNumber;
        private set
        {
            if (SetProperty(ref _levelNumber, value))
                OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => LocalizationService.Format("Sequence_TitleFormat", DiffName(DifficultyKey), LevelNumber);

    private int _lives = 3;
    public int Lives
    {
        get => _lives;
        private set
        {
            if (SetProperty(ref _lives, value))
                OnPropertyChanged(nameof(LivesText));
        }
    }

    public string LivesText => LocalizationService.Format("Sequence_LivesFormat", Lives);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }
    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<SequenceCell> SequenceCells { get; } = new();
    public ObservableCollection<int> Options { get; } = new();

    public AsyncCommand BackCommand { get; }
    public AsyncCommand<int> SelectOptionCommand { get; }

    public CompleteSequencePageViewModel(
        IGameProgressStore progressStore,
        IDialogService dialog,
        INavigationService nav,
        IUserProfileService userService,
        CompleteSequenceGeneratorService generator)
    {
        _progressStore = progressStore;
        _dialog = dialog;
        _nav = nav;
        _userService = userService;
        _generator = generator;

        BackCommand = new AsyncCommand(ConfirmBackAsync);
        SelectOptionCommand = new AsyncCommand<int>(SelectOptionAsync);
    }

    public async Task LoadAsync(string gameId, string difficulty, int level)
    {
        GameId = string.IsNullOrWhiteSpace(gameId) ? "complete_sequence" : gameId;
        DifficultyKey = string.IsNullOrWhiteSpace(difficulty) ? "easy" : difficulty;
        LevelNumber = Math.Max(1, level);
        OnPropertyChanged(nameof(Title));

        _userProfile = await _userService.GetUserAsync();
        Coins = _userProfile?.Coins ?? 0;

        await StartNewRoundAsync();
    }

    private Task StartNewRoundAsync()
    {
        Lives = 3;

        int seed = StableHash($"{GameId}:{DifficultyKey}") + (LevelNumber * 17);
        _puzzle = _generator.Generate(DifficultyKey, seed);

        SequenceCells.Clear();
        Options.Clear();

        for (int i = 0; i < _puzzle.Sequence.Count; i++)
        {
            bool missing = i == _puzzle.MissingIndex;
            SequenceCells.Add(new SequenceCell
            {
                Text = missing ? "?" : _puzzle.Sequence[i].ToString(),
                IsMissing = missing
            });
        }

        foreach (var option in _puzzle.Options)
            Options.Add(option);

        return Task.CompletedTask;
    }

    private async Task SelectOptionAsync(int option)
    {
        if (IsBusy || _puzzle == null) return;
        IsBusy = true;

        if (option == _puzzle.CorrectAnswer)
        {
            int reward = RewardForDifficulty(DifficultyKey);

            if (_userProfile != null)
            {
                _userProfile.Coins += reward;
                Coins = _userProfile.Coins;
                await _userService.SaveUserAsync(_userProfile);
            }

            int completedLevel = LevelNumber;
            await _progressStore.MarkLevelCompleteAsync(GameId, DifficultyKey, completedLevel);

            await _dialog.AlertAsync(
                LocalizationService.GetString("Sequence_CorrectTitle"),
                LocalizationService.Format("Sequence_CorrectMessageFormat", _puzzle.CorrectAnswer, reward));

            LevelNumber = completedLevel + 1;
            await StartNewRoundAsync();
        }
        else
        {
            Lives = Math.Max(0, Lives - 1);

            if (Lives > 0)
            {
                await _dialog.AlertAsync(
                    LocalizationService.GetString("Sequence_WrongTitle"),
                    LocalizationService.Format("Sequence_WrongMessageFormat", Lives));
            }
            else
            {
                bool retry = await _dialog.ConfirmAsync(
                    LocalizationService.GetString("Sequence_GameOverTitle"),
                    LocalizationService.GetString("Sequence_GameOverMessage"),
                    LocalizationService.GetString("Common_Retry"),
                    LocalizationService.GetString("Common_Back"));

                if (retry)
                {
                    await StartNewRoundAsync();
                }
                else
                {
                    await _nav.GoBackAsync();
                }
            }
        }

        IsBusy = false;
    }

    private static int RewardForDifficulty(string difficultyKey) => difficultyKey switch
    {
        "easy" => 10,
        "normal" => 20,
        "hard" => 35,
        "master" => 60,
        _ => 10
    };

    private static string DiffName(string key) => LocalizationService.GetDifficultyLabel(key);

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

    private static int StableHash(string s)
    {
        unchecked
        {
            const int fnvOffset = (int)2166136261;
            const int fnvPrime = 16777619;
            int hash = fnvOffset;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return (int)Math.Abs((long)hash);
        }
    }
}
