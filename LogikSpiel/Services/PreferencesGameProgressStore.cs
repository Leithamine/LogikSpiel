using System.Text.Json;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class PreferencesGameProgressStore : IGameProgressStore
{
    private const string Key = "LOGIKSPIEL_PROGRESS_V1";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<GameProgress> LoadAsync(CancellationToken ct = default)
    {
        var json = Preferences.Get(Key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(new GameProgress());

        try
        {
            var progress = JsonSerializer.Deserialize<GameProgress>(json, _jsonOptions);
            return Task.FromResult(progress ?? new GameProgress());
        }
        catch
        {
            return Task.FromResult(new GameProgress());
        }
    }

    public Task SaveAsync(GameProgress progress, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(progress, _jsonOptions);
        Preferences.Set(Key, json);
        return Task.CompletedTask;
    }
}
