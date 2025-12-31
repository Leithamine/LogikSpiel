#nullable enable

namespace LogikSpiel.Model;

/// <summary>
/// Spezifikation für ein einzelnes Level
/// </summary>
public sealed record LevelSpec(
    string GameId,
    string DifficultyKey,
    int LevelNumber,
    int Seed
);
