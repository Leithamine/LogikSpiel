using System;
using System.Collections.Generic;
using System.Text;

namespace LogikSpiel.Model;

public sealed record GameTile(
    string Id,
    string Title,
    string Image,          // z.B. "dotnet_bot.png"
    string GameId,         // z.B. "Codebreaker"
    int LevelNumber,       // z.B. 1
    int Seed,              // z.B. 123 (deterministisch = testbar)
    string Route,          // z.B. "GameHost"
    int? RequiresLevelNumber = null // optional: sperrt Level bis vorherige gelöst
);


