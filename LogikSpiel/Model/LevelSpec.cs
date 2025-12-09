namespace LogikSpiel.Model;

public sealed record LevelSpec(
    string GameId,
    string DifficultyKey,
    int LevelNumber,
    int Seed
);
