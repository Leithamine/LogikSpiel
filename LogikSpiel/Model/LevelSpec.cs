namespace LogikSpiel.Model;

public sealed record LevelSpec(
    string GameId,
    int LevelNumber,
    int Seed,
    int Difficulty = 1
);
