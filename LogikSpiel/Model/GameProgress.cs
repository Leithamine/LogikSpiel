namespace LogikSpiel.Model;

public sealed class GameProgress
{
    public HashSet<string> Completed { get; set; } = new();

    public static string Key(string gameId, string diff, int level)
        => $"{gameId}:{diff}:L{level}";

    public static string Key(LevelSpec s) => Key(s.GameId, s.DifficultyKey, s.LevelNumber);

    public bool IsCompleted(LevelSpec s) => Completed.Contains(Key(s));
    public bool IsCompleted(string gameId, string diff, int level) => Completed.Contains(Key(gameId, diff, level));

    public void MarkCompleted(LevelSpec s) => Completed.Add(Key(s));
}
