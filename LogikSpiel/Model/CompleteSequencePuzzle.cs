#nullable enable

namespace LogikSpiel.Model;

public sealed class CompleteSequencePuzzle
{
    public IReadOnlyList<int> Sequence { get; init; } = Array.Empty<int>();
    public int MissingIndex { get; init; }
    public int CorrectAnswer { get; init; }
    public IReadOnlyList<int> Options { get; init; } = Array.Empty<int>();
}

public sealed class SequenceCell
{
    public string Text { get; init; } = string.Empty;
    public bool IsMissing { get; init; }
}
