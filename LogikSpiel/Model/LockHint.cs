namespace LogikSpiel.Model;

public class LockHint
{
    // Slots pro Position ("" = leer/ausgeblendet)
    public List<string> Slots { get; set; } = new();

    // Optional (falls du irgendwo Debug/Log brauchst)
    public string Code { get; set; } = "";

    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";

    // flexible Regel
    public int WellPlaced { get; set; }   // richtig + richtige Position
    public int WrongPlaced { get; set; }  // richtig + falsche Position
}
