using LogikSpiel.Model;
using SQLite;

namespace LogikSpiel.Services;

public class SqliteUserProfileService : IUserProfileService
{
    private SQLiteAsyncConnection? _db;
    private const string DbName = "LogikSpiel_v1.db3";

    private async Task InitAsync()
    {
        if (_db is not null) return;
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DbName);
        _db = new SQLiteAsyncConnection(dbPath);
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
        // Wir gehen davon aus, dass es nur einen User gibt (Single Player App)
        var existing = await _db!.Table<UserProfile>().FirstOrDefaultAsync();

        if (existing != null)
        {
            // Update existierender Felder
            existing.Name = user.Name;
            existing.Age = user.Age;
            existing.IsMusicEnabled = user.IsMusicEnabled;
            existing.IsSoundEnabled = user.IsSoundEnabled;
            existing.Coins = user.Coins;
            await _db.UpdateAsync(existing);
        }
        else
        {
            // Neu erstellen
            await _db.InsertAsync(user);
        }
    }

    public async Task<bool> HasProfileAsync()
    {
        await InitAsync();
        var count = await _db!.Table<UserProfile>().CountAsync();
        return count > 0;
    }
}