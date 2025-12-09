using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface IGameProgressStore
{
    Task<GameProgress> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(GameProgress progress, CancellationToken ct = default);
}
