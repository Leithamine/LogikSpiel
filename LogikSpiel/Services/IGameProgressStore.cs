using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface IGameProgressStore
{
    Task<GameProgress> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(GameProgress progress, CancellationToken ct = default);
    Task MarkLevelCompleteAsync(string gameId, string diff, int level);

    // NEU:
    Task ClearAllAsync();
    Task<bool> IsLevelCompleteAsync(string gameId, string diff, int level);
    Task<int> GetCompletedCountAsync(string gameId, string diff);
}