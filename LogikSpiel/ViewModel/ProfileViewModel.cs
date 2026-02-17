using System.Diagnostics;
using System.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.ViewModel;

public class ProfileViewModel : ObservableObject
{
    public sealed record LanguageOption(string Code, string DisplayName);

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
        set { if (SetProperty(ref _isMusicEnabled, value)) _ = SaveSettingsAsync(); }
    }

    private bool _isSoundEnabled;
    public bool IsSoundEnabled
    {
        get => _isSoundEnabled;
        set { if (SetProperty(ref _isSoundEnabled, value)) _ = SaveSettingsAsync(); }
    }

    private readonly IReadOnlyList<LanguageOption> _languages =
    [
        new("de", "Deutsch"),
        new("en", "English"),
        new("es", "Español"),
        new("fr", "Français"),
        new("it", "Italiano"),
        new("pt", "Português"),
        new("ar", "العربية")
    ];

    public IReadOnlyList<LanguageOption> Languages => _languages;

    private LanguageOption? _selectedLanguage;
    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value) || value is null)
                return;

            ApplyLanguage(value);
        }
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
            await _dialog.AlertAsync(
                LocalizationService.GetString("Profile_StatsDialogTitle"),
                LocalizationService.GetString("Profile_StatsDialogMessage"));
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
            _isMusicEnabled = _user.IsMusicEnabled;
            // Wir müssen der UI manuell sagen, dass sich der Wert geändert hat
            OnPropertyChanged(nameof(IsMusicEnabled));

            _isSoundEnabled = _user.IsSoundEnabled;
            OnPropertyChanged(nameof(IsSoundEnabled));

            // Wichtig: UI benachrichtigen, dass sich Coins/ID geändert haben könnten
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Coins));
        }

        var currentLanguage = LocalizationService.GetCurrentCultureCode();
        var matchingLanguage = _languages.FirstOrDefault(language => language.Code == currentLanguage)
                               ?? _languages.FirstOrDefault(language => language.Code == "en");
        if (matchingLanguage is not null)
            SelectedLanguage = matchingLanguage;
    }

    private void ApplyLanguage(LanguageOption language)
    {
        if (language.Code == LocalizationService.GetCurrentCultureCode())
            return;

        LocalizationService.SetCulture(language.Code);
    }

    private async Task SaveSettingsAsync()
    {
        if (_user == null) return;

        try
        {
            _user.IsMusicEnabled = IsMusicEnabled;
            _user.IsSoundEnabled = IsSoundEnabled;

            await _userService.SaveUserAsync(_user);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler beim Speichern der Profileinstellungen: {ex}");

            await _dialog.AlertAsync(
                LocalizationService.GetString("Common_ErrorTitle"),
                $"{LocalizationService.GetString("Profile_SaveErrorMessage")}\n{ex.Message}",
                LocalizationService.GetString("Common_Ok"));
        }
    }
}
