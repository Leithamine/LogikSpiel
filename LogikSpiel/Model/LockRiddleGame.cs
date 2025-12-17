namespace LogikSpiel.Model;

public class LockRiddleGame
{
    public string SecretCode { get; set; } = "";  // Die Lösung (z.B. "987")
    public List<LockHint> Hints { get; set; } = new();
    public int Difficulty { get; set; }
}