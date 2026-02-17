using System.Text.Json;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class AppPackageGameCatalogService : IGameCatalogService, IDisposable
{
    private readonly string _fileName;
    private IReadOnlyList<GameDefinition>? _cache;
    private DateTime _cacheTime;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions _opt = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public AppPackageGameCatalogService(string fileName = "games.json")
        => _fileName = fileName;

    public void InvalidateCache()
    {
        DisposeCache();
        _cache = null;
    }

    public async Task<IReadOnlyList<GameDefinition>> LoadGamesAsync(CancellationToken ct = default)
    {
        if (_cache is not null && DateTime.Now - _cacheTime < _cacheDuration)
            return _cache;

        await using var s = await FileSystem.OpenAppPackageFileAsync(_fileName);
        var games = await JsonSerializer.DeserializeAsync<List<GameDefinition>>(s, _opt, ct)
                    ?? new List<GameDefinition>();

        DisposeCache();
        _cache = games;
        _cacheTime = DateTime.Now;
        return _cache;
    }

    public async Task<GameDefinition?> GetGameAsync(string gameId, CancellationToken ct = default)
        => (await LoadGamesAsync(ct)).FirstOrDefault(g => g.Id == gameId);

    public async Task<IReadOnlyList<LevelSpec>> GetLevelsAsync(string gameId, string difficultyKey, CancellationToken ct = default)
    {
        var game = await GetGameAsync(gameId, ct);
        if (game is null) return Array.Empty<LevelSpec>();

        var baseSeed = StableHash($"{gameId}:{difficultyKey}") % 100000 + 1000;

        var levels = new List<LevelSpec>(game.LevelCount);
        for (int i = 1; i <= game.LevelCount; i++)
        {
            levels.Add(new LevelSpec(gameId, difficultyKey, i, baseSeed + i));
        }
        return levels;
    }

    public void Dispose()
    {
        DisposeCache();
        _cache = null;
        GC.SuppressFinalize(this);
    }

    private void DisposeCache()
    {
        if (_cache is null)
            return;

        foreach (var game in _cache)
            game.Dispose();
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            const int fnvOffset = (int)2166136261;
            const int fnvPrime = 16777619;
            int hash = fnvOffset;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return (int)(Math.Abs((long)hash));
        }
    }
}
