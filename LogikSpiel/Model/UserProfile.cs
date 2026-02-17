// LogikSpiel/Model/UserProfile.cs
using SQLite;

namespace LogikSpiel; // Achte darauf, dass der Namespace zu deinem Projekt passt

public class UserProfile
{
    private readonly object _lock = new();

    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "";
    public int Age { get; set; }
    public int Coins { get; set; } = 100;

    public bool IsMusicEnabled { get; set; } = true;
    public bool IsSoundEnabled { get; set; } = true;

    // Speichert die Zahlen als Text in der Datenbank (z.B. "10,500,123")
    public string UsedNumbersRaw { get; set; } = "";

    [Ignore] // Wird nicht in der DB gespeichert, nur im Programm genutzt
    public List<int> UsedNumbers
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UsedNumbersRaw))
                return new List<int>();

            try
            {
                lock (_lock)
                {
                    return UsedNumbersRaw
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                        .Where(n => n.HasValue)
                        .Select(n => n!.Value)
                        .ToList();
                }
            }
            catch
            {
                return new List<int>();
            }
        }
    }

    // Methode zum Hinzufügen einer Zahl
    public void AddUsedNumber(int number)
    {
        lock (_lock)
        {
            var list = UsedNumbersRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();

            if (!list.Contains(number))
            {
                list.Add(number);
                UsedNumbersRaw = string.Join(",", list);
            }
        }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
