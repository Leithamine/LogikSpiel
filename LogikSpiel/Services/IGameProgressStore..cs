using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface IGameProgressStore
{
    Task<GameProgress> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(GameProgress progress, CancellationToken ct = default);

    // NEU: Diese Zeile hinzufügen!
    Task MarkLevelCompleteAsync(string gameId, string diff, int level);
}