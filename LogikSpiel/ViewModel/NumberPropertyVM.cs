using System;

namespace LogikSpiel.ViewModel;

public sealed class NumberPropertyVM
{
    public string Key { get; }
    public string KidDescription { get; }
    public string Examples { get; }
    public bool HasExamples => !string.IsNullOrWhiteSpace(Examples);
    public bool ShowExamples => HasExamples && !string.Equals(Key, "Quersumme", StringComparison.OrdinalIgnoreCase);

    public string TitleLine => $"Diese Zahl ist {Key}.";

    public NumberPropertyVM(string key, string kidDescription, string examples)
    {
        Key = key;
        KidDescription = kidDescription;
        Examples = examples;
    }
}
