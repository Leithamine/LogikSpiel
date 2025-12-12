using SQLite;

namespace LogikSpiel.Model;

public class GameProgressEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string GameId { get; set; } = ""; // <--- HIER GEÄNDERT (= "";)

    [Indexed]
    public string Difficulty { get; set; } = ""; // <--- HIER GEÄNDERT (= "";)

    public int LevelNumber { get; set; }

    public bool IsCompleted { get; set; }

    public int Stars { get; set; }
    public int Score { get; set; }
    public DateTime CompletedAt { get; set; }
}