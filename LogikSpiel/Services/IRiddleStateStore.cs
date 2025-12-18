using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface IRiddleStateStore
{
    Task<LockRiddleGame?> TryLoadAsync(string gameId, string difficulty, int level);
    Task SaveAsync(string gameId, string difficulty, int level, LockRiddleGame game);
    Task ClearAsync(string gameId, string difficulty, int level);
}
