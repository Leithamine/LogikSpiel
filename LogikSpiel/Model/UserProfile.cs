// LogikSpiel/Model/UserProfile.cs
using SQLite;
using System.Text.Json;

namespace LogikSpiel; // Achte darauf, dass der Namespace zu deinem Projekt passt

public class UserProfile
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "";
    public int Age { get; set; }
    public int Coins { get; set; } = 10000000;

    public bool IsMusicEnabled { get; set; } = true;
    public bool IsSoundEnabled { get; set; } = true;

    // Speichert die Zahlen als Text in der Datenbank (z.B. "10,500,123")
    public string UsedNumbersRaw { get; set; } = "";

    [Ignore] // Wird nicht in der DB gespeichert, nur im Programm genutzt
    public List<int> UsedNumbers
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UsedNumbersRaw)) return new List<int>();
            return UsedNumbersRaw.Split(',').Select(int.Parse).ToList();
        }
    }

    // Methode zum Hinzufügen einer Zahl
    public void AddUsedNumber(int number)
    {
        var list = UsedNumbers;
        if (!list.Contains(number))
        {
            list.Add(number);
            UsedNumbersRaw = string.Join(",", list);
        }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
