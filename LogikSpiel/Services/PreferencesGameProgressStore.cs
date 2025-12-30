#nullable enable
using System.Text.Json;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

/// <summary>
/// Speichert Spielfortschritt in den App-Preferences (für einfache Persistenz ohne SQLite)
/// </summary>
public sealed class PreferencesGameProgressStore : IGameProgressStore
{
    private const string Key = "LOGIKSPIEL_PROGRESS_V1";

    // ✅ Cache für bessere Performance
    private GameProgress? _cache;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public Task<GameProgress> LoadAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            // Return cached version if available
            if (_cache != null)
                return Task.FromResult(_cache);

            var json = Preferences.Get(Key, "");

            if (string.IsNullOrWhiteSpace(json))
            {
                _cache = new GameProgress();
                return Task.FromResult(_cache);
            }

            try
            {
                _cache = JsonSerializer.Deserialize<GameProgress>(json, _jsonOptions)
                         ?? new GameProgress();
                return Task.FromResult(_cache);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesGameProgressStore] JSON Error: {ex.Message}");
                _cache = new GameProgress();
                return Task.FromResult(_cache);
            }
        }
    }

    public Task SaveAsync(GameProgress progress, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(progress, _jsonOptions);
                Preferences.Set(Key, json);
                _cache = progress;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesGameProgressStore] Save Error: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ✅ FIX: Vollständige Implementierung (war vorher ein Dummy)
    /// </summary>
    public async Task MarkLevelCompleteAsync(string gameId, string diff, int level)
    {
        if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(diff))
            return;

        var progress = await LoadAsync();

        // Erstelle den Key für dieses Level
        var key = GameProgress.Key(gameId, diff, level);

        // Füge hinzu wenn noch nicht vorhanden
        if (!progress.Completed.Contains(key))
        {
            progress.Completed.Add(key);
            await SaveAsync(progress);

            System.Diagnostics.Debug.WriteLine($"[PreferencesGameProgressStore] Marked complete: {key}");
        }
    }

    /// <summary>
    /// ✅ NEU: Hilfsmethode zum Löschen des gesamten Fortschritts (für Debug/Reset)
    /// </summary>
    public Task ClearAllAsync()
    {
        lock (_lock)
        {
            Preferences.Remove(Key);
            _cache = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ✅ NEU: Hilfsmethode um zu prüfen ob ein Level abgeschlossen ist
    /// </summary>
    public async Task<bool> IsLevelCompleteAsync(string gameId, string diff, int level)
    {
        var progress = await LoadAsync();
        return progress.IsCompleted(gameId, diff, level);
    }

    /// <summary>
    /// ✅ NEU: Zählt abgeschlossene Level für ein Spiel
    /// </summary>
    public async Task<int> GetCompletedCountAsync(string gameId, string diff)
    {
        var progress = await LoadAsync();
        var prefix = $"{gameId}:{diff}:L";
        return progress.Completed.Count(k => k.StartsWith(prefix));
    }
}
