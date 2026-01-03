// LogikSpiel/Model/MathHangman/MathHangmanModel.cs
#nullable enable
namespace LogikSpiel.Model.MathHangman;

public sealed record NumberProperty(string Key, string Article, string KidDescription, string Examples);

public sealed record ShopHint(string Id, string Title, string Description, int BasePrice);
