using System.Text.Json;
using LogikSpiel.Model;
using Microsoft.Maui.Storage;

namespace LogikSpiel.Services;

public sealed class RiddleStateStore : IRiddleStateStore
{
    private const string RiddleKeysIndex = "riddle:keys";
    private const int MaxStoredRiddles = 100;

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
        CleanupOldRiddles(key);
        return Task.CompletedTask;
    }

    private void CleanupOldRiddles(string newKey)
    {
        var allKeys = LoadRiddleKeys();
        allKeys.RemoveAll(k => string.Equals(k, newKey, StringComparison.Ordinal));
        allKeys.Add(newKey);

        if (allKeys.Count > MaxStoredRiddles)
        {
            var keysToRemove = allKeys.Take(allKeys.Count - MaxStoredRiddles).ToList();
            foreach (var key in keysToRemove)
            {
                Preferences.Remove(key);
                allKeys.Remove(key);
            }
        }

        SaveRiddleKeys(allKeys);
    }

    private static List<string> LoadRiddleKeys()
    {
        var raw = Preferences.Get(RiddleKeysIndex, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Where(k => k.StartsWith("riddle:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void SaveRiddleKeys(List<string> keys)
    {
        var unique = keys.Distinct(StringComparer.Ordinal);
        Preferences.Set(RiddleKeysIndex, string.Join("|", unique));
    }

    public Task ClearAsync(string gameId, string difficulty, int level)
    {
        var key = Key(gameId, difficulty, level);
        Preferences.Remove(key);

        var keys = LoadRiddleKeys();
        if (keys.RemoveAll(k => string.Equals(k, key, StringComparison.Ordinal)) > 0)
        {
            SaveRiddleKeys(keys);
        }

        return Task.CompletedTask;
    }
}
