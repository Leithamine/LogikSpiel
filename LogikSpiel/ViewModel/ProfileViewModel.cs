using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public class ProfileViewModel : ObservableObject
{
    private readonly IUserProfileService _userService;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    private UserProfile? _user;

    // --- Properties für die Anzeige ---
    public string Id => _user?.Id ?? "-";
    public int Coins => _user?.Coins ?? 0;

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private int _age;
    public int Age { get => _age; set => SetProperty(ref _age, value); }

    // --- Einstellungen ---
    private bool _isMusicEnabled;
    public bool IsMusicEnabled
    {
        get => _isMusicEnabled;
        set { if (SetProperty(ref _isMusicEnabled, value)) SaveSettings(); }
    }

    private bool _isSoundEnabled;
    public bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set { if (SetProperty(ref _isSoundEnabled, value)) SaveSettings(); }
    }

    // --- Commands ---
    public AsyncCommand BackCommand { get; }
    public AsyncCommand ShowStatsCommand { get; }

    // --- Konstruktor ---
    public ProfileViewModel(IUserProfileService userService, IDialogService dialog, INavigationService nav)
    {
        _userService = userService;
        _dialog = dialog;
        _nav = nav;

        // Zurück zur vorherigen Seite
        BackCommand = new AsyncCommand(() => _nav.GoBackAsync());

        // Statistik Button (Platzhalter)
        ShowStatsCommand = new AsyncCommand(async () =>
        {
            await _dialog.AlertAsync("Statistik", "Hier kommen bald deine Spiel-Statistiken hin!");
        });
    }

    // --- Methoden ---
    public async Task LoadAsync()
    {
        _user = await _userService.GetUserAsync();
        if (_user != null)
        {
            Name = _user.Name;
            Age = _user.Age;
            IsMusicEnabled = _user.IsMusicEnabled;
            IsSoundEnabled = _user.IsSoundEnabled;

            // Wichtig: UI benachrichtigen, dass sich Coins/ID geändert haben könnten
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Coins));
        }
    }

    private async void SaveSettings()
    {
        if (_user == null) return;
        _user.IsMusicEnabled = IsMusicEnabled;
        _user.IsSoundEnabled = IsSoundEnabled;
        await _userService.SaveUserAsync(_user);
    }
}