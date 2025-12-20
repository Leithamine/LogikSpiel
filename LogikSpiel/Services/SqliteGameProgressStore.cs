using LogikSpiel.Model;
using SQLite;

namespace LogikSpiel.Services;

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

    public Task SaveAsync(GameProgress progress, CancellationToken ct = default)
    {
        // Wird nicht benötigt, da wir MarkLevelCompleteAsync nutzen.
        return Task.CompletedTask;
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

}