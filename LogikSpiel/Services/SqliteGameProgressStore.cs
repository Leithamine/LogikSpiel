using LogikSpiel.Model;
using LogikSpiel.Services;
using SQLite;

public sealed class SqliteGameProgressStore : IGameProgressStore
{
    private SQLiteAsyncConnection? _db;

    private async Task InitAsync()
    {
        if (_db is not null) return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "LogikSpiel_v1.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<GameProgressEntity>();
    }

    public async Task<GameProgress> LoadAsync(CancellationToken ct = default)
    {
        await InitAsync();
        var allEntries = await _db!.Table<GameProgressEntity>().ToListAsync();

        var progress = new GameProgress();
        foreach (var entry in allEntries)
        {
            if (entry.IsCompleted)
            {
                progress.MarkCompleted(new LevelSpec(entry.GameId, entry.Difficulty, entry.LevelNumber, 0));
            }
        }
        return progress;
    }

    public async Task SaveAsync(GameProgress progress, CancellationToken ct = default)
    {
        await InitAsync();

        foreach (var completedKey in progress.Completed)
        {
            var parts = completedKey.Split(':');
            if (parts.Length >= 3)
            {
                var gameId = parts[0];
                var diff = parts[1];
                var levelStr = parts[2].StartsWith("L") ? parts[2].Substring(1) : parts[2];

                if (int.TryParse(levelStr, out var level))
                {
                    await MarkLevelCompleteAsync(gameId, diff, level);
                }
            }
        }
    }

    public async Task MarkLevelCompleteAsync(string gameId, string diff, int level)
    {
        await InitAsync();

        var existing = await _db!.Table<GameProgressEntity>()
            .Where(x => x.GameId == gameId && x.Difficulty == diff && x.LevelNumber == level)
            .FirstOrDefaultAsync();

        if (existing == null)
        {
            var entity = new GameProgressEntity
            {
                GameId = gameId,
                Difficulty = diff,
                LevelNumber = level,
                IsCompleted = true,
                Stars = 0,
                Score = 0,
                CompletedAt = DateTime.Now
            };
            await _db.InsertAsync(entity);
        }
        else
        {
            if (!existing.IsCompleted)
            {
                existing.IsCompleted = true;
                existing.CompletedAt = DateTime.Now;
                await _db.UpdateAsync(existing);
            }
        }
    }

    // NEU: Fehlende Methoden hinzufügen
    public async Task<bool> IsLevelCompleteAsync(string gameId, string diff, int level)
    {
        await InitAsync();
        var existing = await _db!.Table<GameProgressEntity>()
            .Where(x => x.GameId == gameId && x.Difficulty == diff && x.LevelNumber == level)
            .FirstOrDefaultAsync();
        return existing?.IsCompleted ?? false;
    }

    public async Task<int> GetCompletedCountAsync(string gameId, string diff)
    {
        await InitAsync();
        return await _db!.Table<GameProgressEntity>()
            .Where(x => x.GameId == gameId && x.Difficulty == diff && x.IsCompleted)
            .CountAsync();
    }

    public async Task ClearAllAsync()
    {
        await InitAsync();
        await _db!.DeleteAllAsync<GameProgressEntity>();
    }
}