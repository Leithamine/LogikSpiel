using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface IUserProfileService
{
    Task<UserProfile?> GetUserAsync();
    Task SaveUserAsync(UserProfile user);
    Task<bool> HasProfileAsync();
    event Action UserDataChanged;
}