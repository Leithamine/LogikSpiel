#nullable enable

namespace LogikSpiel.Services;

public interface IProgressService
{
    Task<ProgressData?> GetProgressAsync(string gameId, string difficultyKey);
    Task SaveProgressAsync(string gameId, string difficultyKey, ProgressData data);
    Task MarkLevelCompletedAsync(string gameId, string difficultyKey, int levelNumber);
}

public sealed class ProgressData
{
    public int CurrentLevel { get; set; } = 1;
    public HashSet<int> CompletedLevels { get; set; } = new();
}
