using System.Text.Json;
using LogikSpiel.Model;
using Microsoft.Maui.Storage;

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

    public Task MarkLevelCompleteAsync(string gameId, string diff, int level)
    {
        var progress = Preferences.Get(Key, "");
        GameProgress state;

        if (string.IsNullOrWhiteSpace(progress))
        {
            state = new GameProgress();
        }
        else
        {
            try
            {
                state = JsonSerializer.Deserialize<GameProgress>(progress) ?? new GameProgress();
            }
            catch
            {
                state = new GameProgress();
            }
        }

        state.MarkCompleted(new LevelSpec(gameId, diff, level, 0));
        Preferences.Set(Key, JsonSerializer.Serialize(state));
        return Task.CompletedTask;
    }

    public Task SaveAsync(GameProgress progress, CancellationToken ct = default)
    {
        Preferences.Set(Key, JsonSerializer.Serialize(progress));
        return Task.CompletedTask;
    }
}
