using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface IGameCatalogService
{
    Task<IReadOnlyList<GameDefinition>> LoadGamesAsync(CancellationToken ct = default);
    Task<GameDefinition?> GetGameAsync(string gameId, CancellationToken ct = default);
    Task<IReadOnlyList<LevelSpec>> GetLevelsAsync(string gameId, string difficultyKey, CancellationToken ct = default);
}
