using System;
using System.Collections.Generic;
using System.Text;

namespace LogikSpiel.Model;

public sealed class GameTile
{
    public string GameId { get; init; } = "";
    public int LevelNumber { get; init; }
    public int Seed { get; init; }
    public string DifficultyKey { get; init; } = "normal";

    public string Title { get; init; } = "";
    public string Image { get; init; } = "";
    public string Route { get; init; } = "";
}



