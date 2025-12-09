namespace LogikSpiel.Model;

public sealed class GameProgress
{
    public HashSet<string> Completed { get; set; } = new();

    public static string Key(LevelSpec s) => $"{s.GameId}:{s.DifficultyKey}:L{s.LevelNumber}";

    public bool IsCompleted(LevelSpec s) => Completed.Contains(Key(s));

    public void MarkCompleted(LevelSpec s) => Completed.Add(Key(s));
}
