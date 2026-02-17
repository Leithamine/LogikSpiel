using LogikSpiel.Model;
using SQLite;

namespace LogikSpiel.Services;

public class SqliteUserProfileService : IUserProfileService
{
    private SQLiteAsyncConnection? _db;

    public event Action? UserDataChanged;

    private async Task InitAsync()
    {
        if (_db is not null) return;

        _db = await SqliteConnectionFactory.GetConnectionAsync();
        await _db.CreateTableAsync<UserProfile>();
    }

    public async Task<UserProfile?> GetUserAsync()
    {
        await InitAsync();
        return await _db!.Table<UserProfile>().FirstOrDefaultAsync();
    }

    public async Task SaveUserAsync(UserProfile user)
    {
        await InitAsync();

        var existing = await _db!.Table<UserProfile>()
            .Where(x => x.Id == user.Id)
            .FirstOrDefaultAsync();

        bool success;
        if (existing != null)
        {
            existing.Name = user.Name;
            existing.Age = user.Age;
            existing.IsMusicEnabled = user.IsMusicEnabled;
            existing.IsSoundEnabled = user.IsSoundEnabled;
            existing.Coins = user.Coins;
            existing.UsedNumbersRaw = user.UsedNumbersRaw;
            success = await _db.UpdateAsync(existing) > 0;
        }
        else
        {
            success = await _db.InsertAsync(user) > 0;
        }

        if (success)
        {
            UserDataChanged?.Invoke();
        }
    }

    public async Task<bool> HasProfileAsync()
    {
        await InitAsync();
        var count = await _db!.Table<UserProfile>().CountAsync();
        return count > 0;
    }
}
