using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public class OnboardingViewModel : ObservableObject
{
    private readonly IUserProfileService _userService;
    private readonly IServiceProvider _serviceProvider; // NEU: Direkt injiziert

    private string _name = "";
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) Validate(); }
    }

    private string _ageText = "";
    public string AgeText
    {
        get => _ageText;
        set { if (SetProperty(ref _ageText, value)) Validate(); }
    }

    private bool _isValid;
    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public AsyncCommand CompleteOnboardingCommand { get; }

    // Konstruktor angepasst: IServiceProvider wird hier empfangen
    public OnboardingViewModel(IUserProfileService userService, IServiceProvider serviceProvider)
    {
        _userService = userService;
        _serviceProvider = serviceProvider;

        CompleteOnboardingCommand = new AsyncCommand(async () =>
        {
            if (!IsValid) return;

            if (int.TryParse(AgeText, out int age))
            {
                var newUser = new UserProfile
                {
                    Name = Name,
                    Age = age,
                    Coins = 10,
                    CreatedAt = DateTime.Now
                };

                await _userService.SaveUserAsync(newUser);

                // --- FIX FÜR MAINPAGE DEPRECATED ---
                // Statt Application.Current.MainPage = ... nutzen wir das Fenster:
                if (Application.Current?.Windows.Count > 0)
                {
                    // Wir holen das erste (und auf Handy meist einzige) Fenster
                    var window = Application.Current.Windows[0];

                    // Wir setzen die Seite dieses Fensters neu
                    window.Page = new AppShell(_serviceProvider);
                }
            }
        }, () => IsValid);
    }

    private void Validate()
    {
        bool isNameValid = !string.IsNullOrWhiteSpace(Name);
        bool isAgeValid = int.TryParse(AgeText, out int age) && age >= 5 && age <= 99;

        IsValid = isNameValid && isAgeValid;
        CompleteOnboardingCommand.RaiseCanExecuteChanged();
    }
}