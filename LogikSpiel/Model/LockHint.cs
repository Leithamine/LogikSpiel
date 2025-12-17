namespace LogikSpiel.Model;

public class LockHint
{
    // WICHTIG: { get; set; } ist zwingend nötig für Bindings!
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
}