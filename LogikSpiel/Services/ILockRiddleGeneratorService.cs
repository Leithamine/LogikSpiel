using LogikSpiel.Model;

namespace LogikSpiel.Services;

public interface ILockRiddleGeneratorService
{
    LockRiddleGame GenerateGame(string difficultyKey);
    bool IsGameValid(LockRiddleGame game);
}
