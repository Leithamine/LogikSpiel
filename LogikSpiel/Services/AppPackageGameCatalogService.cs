using System.Text.Json;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class AppPackageGameCatalogService : IGameCatalogService
{
    private readonly string _fileName;
    private IReadOnlyList<GameDefinition>? _cache;

    private static readonly JsonSerializerOptions _opt = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,       // Wichtig: Erlaubt Kommas am Ende von Listen (z.B. [A, B,])
        ReadCommentHandling = JsonCommentHandling.Skip, // Erlaubt Kommentare im JSON
        WriteIndented = true
    };

    public AppPackageGameCatalogService(string fileName = "games.json")
        => _fileName = fileName;

    public async Task<IReadOnlyList<GameDefinition>> LoadGamesAsync(CancellationToken ct = default)
    {
        if (_cache is not null) return _cache;

        try
        {
            await using var s = await FileSystem.OpenAppPackageFileAsync(_fileName);
            var games = await JsonSerializer.DeserializeAsync<List<GameDefinition>>(s, _opt, ct)
                        ?? new List<GameDefinition>();

            _cache = games;
            return _cache;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);

            // Fallback: App startet trotzdem
            _cache = new List<GameDefinition>();
            return _cache;
        }

    }


    public async Task<GameDefinition?> GetGameAsync(string gameId, CancellationToken ct = default)
        => (await LoadGamesAsync(ct)).FirstOrDefault(g => g.Id == gameId);

    public async Task<IReadOnlyList<LevelSpec>> GetLevelsAsync(string gameId, CancellationToken ct = default)
    {
        var game = await GetGameAsync(gameId, ct);
        if (game is null) return Array.Empty<LevelSpec>();

        var baseSeed = StableHash(gameId) % 100000 + 1000;

        var levels = new List<LevelSpec>(game.LevelCount);
        for (int i = 1; i <= game.LevelCount; i++)
        {
            levels.Add(new LevelSpec(gameId, i, baseSeed + i, 1));
        }
        return levels;
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
            return hash < 0 ? -hash : hash;
        }
    }
}
