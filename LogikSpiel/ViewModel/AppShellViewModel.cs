using LogikSpiel.Core;
using LogikSpiel.Services;

namespace LogikSpiel.ViewModel;

public class AppShellViewModel : ObservableObject
{
    private readonly IUserProfileService _userService;
    private readonly INavigationService _nav;

    private int _coins;
    public int Coins { get => _coins; set => SetProperty(ref _coins, value); }

    private string _userName = "";
    public string UserName { get => _userName; set => SetProperty(ref _userName, value); }

    public AsyncCommand OpenProfileCommand { get; }

    public AppShellViewModel(IUserProfileService userService, INavigationService nav)
    {
        _userService = userService;
        _nav = nav;

        // Wenn sich Daten ändern, laden wir neu
        _userService.UserDataChanged += async () => await RefreshData();

        OpenProfileCommand = new AsyncCommand(async () =>
        {
            // Navigiere zur Profilseite
            await _nav.GoToAsync("Profile"); // Stelle sicher, dass die Route registriert ist!
        });

        // Start-Daten laden
        _ = RefreshData();
    }

    private async Task RefreshData()
    {
        var user = await _userService.GetUserAsync();
        if (user != null)
        {
            Coins = user.Coins;
            UserName = user.Name;
        }
    }
}