using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public class ProfileViewModel : ObservableObject
{
    private readonly IUserProfileService _userService;
    private readonly IDialogService _dialog;

    private UserProfile? _user;

    public string Id => _user?.Id ?? "-";
    public int Coins => _user?.Coins ?? 0;

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private int _age;
    public int Age { get => _age; set => SetProperty(ref _age, value); }

    private bool _isMusicEnabled;
    public bool IsMusicEnabled
    {
        get => _isMusicEnabled;
        set
        {
            if (SetProperty(ref _isMusicEnabled, value)) SaveSettings();
        }
    }

    private bool _isSoundEnabled;
    public bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set
        {
            if (SetProperty(ref _isSoundEnabled, value)) SaveSettings();
        }
    }

    public AsyncCommand SaveDataCommand { get; }

    public ProfileViewModel(IUserProfileService userService, IDialogService dialog)
    {
        _userService = userService;
        _dialog = dialog;

        SaveDataCommand = new AsyncCommand(async () =>
        {
            if (_user == null) return;

            // Validierung
            if (string.IsNullOrWhiteSpace(Name) || Age < 5 || Age > 99)
            {
                await _dialog.AlertAsync("Fehler", "Bitte gib einen gültigen Namen und ein Alter zwischen 5 und 99 ein.");
                return;
            }

            _user.Name = Name;
            _user.Age = Age;

            await _userService.SaveUserAsync(_user);
            await _dialog.AlertAsync("Gespeichert", "Deine Daten wurden aktualisiert.");
        });
    }

    public async Task LoadAsync()
    {
        _user = await _userService.GetUserAsync();
        if (_user != null)
        {
            Name = _user.Name;
            Age = _user.Age;
            IsMusicEnabled = _user.IsMusicEnabled;
            IsSoundEnabled = _user.IsSoundEnabled;
            // Notify other properties
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
        // Hier könnte man auch einen AudioService benachrichtigen
    }
}