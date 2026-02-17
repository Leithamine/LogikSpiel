using LogikSpiel.Model;
using SQLite;

namespace LogikSpiel.Services;

public sealed class SqliteGameProgressStore : IGameProgressStore
{
    private SQLiteAsyncConnection? _db;

    private async Task InitAsync()
    {
        if (_db is not null) return;

        _db = await SqliteConnectionFactory.GetConnectionAsync();
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
        ArgumentNullException.ThrowIfNull(progress);

        await InitAsync();

        foreach (var completedKey in progress.Completed)
        {
            ct.ThrowIfCancellationRequested();

            var parts = completedKey.Split(':', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                continue;
            }

            var gameId = parts[0];
            var diff = parts[1];
            var levelToken = parts[2];
            var levelStr = levelToken.StartsWith("L", StringComparison.OrdinalIgnoreCase)
                ? levelToken[1..]
                : levelToken;

            if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(diff))
            {
                continue;
            }

            if (int.TryParse(levelStr, out var level) && level > 0)
            {
                await MarkLevelCompleteAsync(gameId, diff, level);
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
        else if (!existing.IsCompleted)
        {
            existing.IsCompleted = true;
            existing.CompletedAt = DateTime.Now;
            await _db.UpdateAsync(existing);
        }
    }

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
