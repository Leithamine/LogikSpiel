using System.Text.Json;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class PreferencesGameProgressStore : IGameProgressStore
{
    private const string Key = "LOGIKSPIEL_PROGRESS_V1";

    public Task<GameProgress> LoadAsync(CancellationToken ct = default)
    {
        var json = Preferences.Get(Key, "");
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(new GameProgress());

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<GameProgress>(json) ?? new GameProgress());
        }
        catch
        {
            return Task.FromResult(new GameProgress());
        }
    }

    public Task SaveAsync(GameProgress progress, CancellationToken ct = default)
    {
        Preferences.Set(Key, JsonSerializer.Serialize(progress));
        return Task.CompletedTask;
    }
}
