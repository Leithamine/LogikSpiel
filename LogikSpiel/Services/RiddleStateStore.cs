using System.Text.Json;
using LogikSpiel.Model;
using Microsoft.Maui.Storage;

namespace LogikSpiel.Services;

public sealed class RiddleStateStore : IRiddleStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string Key(string gameId, string difficulty, int level)
        => $"riddle:{gameId}:{difficulty}:{level}";

    public Task<LockRiddleGame?> TryLoadAsync(string gameId, string difficulty, int level)
    {
        var key = Key(gameId, difficulty, level);

        if (!Preferences.ContainsKey(key))
            return Task.FromResult<LockRiddleGame?>(null);

        var json = Preferences.Get(key, "");
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult<LockRiddleGame?>(null);

        try
        {
            var game = JsonSerializer.Deserialize<LockRiddleGame>(json, JsonOptions);
            if (game == null || string.IsNullOrWhiteSpace(game.SecretCode) || game.Hints == null || game.Hints.Count == 0)
            {
                Preferences.Remove(key);
                return Task.FromResult<LockRiddleGame?>(null);
            }

            return Task.FromResult<LockRiddleGame?>(game);
        }
        catch
        {
            Preferences.Remove(key);
            return Task.FromResult<LockRiddleGame?>(null);
        }
    }

    public Task SaveAsync(string gameId, string difficulty, int level, LockRiddleGame game)
    {
        var key = Key(gameId, difficulty, level);
        var json = JsonSerializer.Serialize(game, JsonOptions);
        Preferences.Set(key, json);
        return Task.CompletedTask;

    }

    public Task ClearAsync(string gameId, string difficulty, int level)
    {
        Preferences.Remove(Key(gameId, difficulty, level));
        return Task.CompletedTask;
    }
}
