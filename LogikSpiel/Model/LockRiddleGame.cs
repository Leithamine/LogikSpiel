namespace LogikSpiel.Model;

public class LockRiddleGame
{
    public string SecretCode { get; set; } = "";
    public List<LockHint> Hints { get; set; } = new();
    public int Difficulty { get; set; }
}
