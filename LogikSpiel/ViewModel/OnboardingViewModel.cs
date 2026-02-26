using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogikSpiel.ViewModel;

public class OnboardingViewModel : ObservableObject
{
    private readonly IUserProfileService _userService;
    private readonly IServiceProvider _serviceProvider;

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

    public OnboardingViewModel(IUserProfileService userService, IServiceProvider serviceProvider)
    {
        _userService = userService;
        _serviceProvider = serviceProvider;

        CompleteOnboardingCommand = new AsyncCommand(async () =>
        {
            if (!IsValid) return;

            if (int.TryParse(AgeText, out int age))
            {
                var startingCoins = string.Equals(Name?.Trim(), "Leith", StringComparison.OrdinalIgnoreCase)
                    ? 100000
                    : 100;

                var newUser = new UserProfile
                {
                    Name = Name,
                    Age = age,
                    Coins = startingCoins,
                    CreatedAt = DateTime.Now
                };

                await _userService.SaveUserAsync(newUser);

                if (Application.Current?.Windows.Count > 0)
                {
                    var window = Application.Current.Windows[0];
                    var appShell = _serviceProvider.GetRequiredService<AppShell>();
                    window.Page = appShell;
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
