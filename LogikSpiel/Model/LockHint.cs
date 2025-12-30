namespace LogikSpiel.Model;

public class LockHint
{
    public List<string> Slots { get; set; } = new();

    public string Code { get; set; } = "";

    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";

    public int WellPlaced { get; set; }
    public int WrongPlaced { get; set; }
}
