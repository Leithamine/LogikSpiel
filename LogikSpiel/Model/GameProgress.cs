using System;
using System.Collections.Generic;
using System.Text;

namespace LogikSpiel.Model;

public sealed class GameProgress
{
    public HashSet<string> Completed { get; set; } = new();
    public Dictionary<string, int> BestScore { get; set; } = new();

    public static string Key(LevelSpec spec) => $"{spec.GameId}:L{spec.LevelNumber}:S{spec.Seed}";

    public bool IsCompleted(LevelSpec spec) => Completed.Contains(Key(spec));

    public void MarkCompleted(LevelSpec spec, int? score = null)
    {
        var k = Key(spec);
        Completed.Add(k);

        if (score is null) return;

        if (!BestScore.TryGetValue(k, out var old) || score.Value > old)
            BestScore[k] = score.Value;
    }

    public int? GetBestScore(LevelSpec spec)
        => BestScore.TryGetValue(Key(spec), out var s) ? s : null;
}


