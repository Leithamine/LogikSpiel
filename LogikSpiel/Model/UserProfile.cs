using SQLite;

namespace LogikSpiel.Model;

public class UserProfile
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString(); // Eindeutige ID

    public string Name { get; set; } = "";
    public int Age { get; set; }

    public int Coins { get; set; } = 0; // Startwert

    public bool IsMusicEnabled { get; set; } = true;
    public bool IsSoundEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}