namespace LogikSpiel.Model;

public sealed record GameDefinition(
    string Id,
    string Title,
    string Image,
    int LevelCount,
    List<string>? RulesText
);
