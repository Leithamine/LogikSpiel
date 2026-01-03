#nullable enable

namespace LogikSpiel.ViewModel;

/// <summary>
/// ViewModel für eine Zahlen-Eigenschaft im MathHangman Spiel
/// </summary>
public sealed class NumberPropertyVM
{
    public string Key { get; }
    public string KidDescription { get; }
    public string Examples { get; }
    public bool HasExamples => !string.IsNullOrWhiteSpace(Examples);
    public string TitleLine => $"✓ {Key}";

    public NumberPropertyVM(string key, string kidDescription, string examples)
    {
        Key = key;
        KidDescription = kidDescription;
        Examples = examples;
    }
}
