#nullable enable

namespace LogikSpiel.Model;

/// <summary>
/// Model für Spiel-Definition aus games.json
/// </summary>
public sealed class GameDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";

    /// <summary>
    /// Die Shell-Route für die Spielseite (z.B. "PascalTrianglePage")
    /// WICHTIG: Muss mit Routing.RegisterRoute() übereinstimmen!
    /// </summary>
    public string Route { get; set; } = "";

    public int LevelCount { get; set; } = 50;
    public List<string> Difficulties { get; set; } = new();
    public List<string> Rules { get; set; } = new();
}
