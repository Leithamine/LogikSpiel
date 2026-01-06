#nullable enable
using System;
namespace LogikSpiel.Model.NumberRain;

public enum NumberRainQuestMode
{
    Count,
    Timed,
    Avoid,
    Multi,
    Combo,
    Survival,
    Dynamic,
    Switch
}

public sealed record NumberRainGoal(string Label, Func<int, int?, bool> Predicate, int TargetCount);

public sealed record NumberRainRule(string Label, Func<int, int?, bool> Predicate);

public sealed class NumberRainQuest
{
    public string Description { get; init; } = string.Empty;
    public NumberRainQuestMode Mode { get; init; } = NumberRainQuestMode.Count;
    public IReadOnlyList<NumberRainGoal> Goals { get; init; } = Array.Empty<NumberRainGoal>();
    public IReadOnlyList<NumberRainRule> Rules { get; init; } = Array.Empty<NumberRainRule>();
    public Func<int, int?, bool>? AvoidPredicate { get; init; }
    public string? AvoidLabel { get; init; }
    public int ComboTarget { get; init; }
    public int TimeLimitSeconds { get; init; }
    public int SwitchIntervalSeconds { get; init; }
    public int MinHits { get; init; }
    public int MaxMisses { get; init; }
}
