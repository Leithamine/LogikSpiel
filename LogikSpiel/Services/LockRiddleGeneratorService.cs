using LogikSpiel.Model;

namespace LogikSpiel.Services;

public class LockRiddleGeneratorService
{
    private readonly Random _rnd = new Random();

    public LockRiddleGame GenerateGame(int difficultyLength)
    {
        // KORREKTUR: Wir nutzen direkt den Wert vom ViewModel (3, 4, 5 oder 6)
        // Sicherstellen, dass es mind. 3 ist
        int length = Math.Max(3, difficultyLength);

        // 1. ZUFÄLLIGE LÖSUNG GENERIEREN
        var availableDigits = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        int[] secretCode = new int[length];

        for (int i = 0; i < length; i++)
        {
            int index = _rnd.Next(availableDigits.Count);
            secretCode[i] = availableDigits[index];
            availableDigits.RemoveAt(index);
        }

        var invalidDigits = new List<int>(availableDigits);

        // 2. HINWEISE GENERIEREN
        var hints = new List<LockHint>();
        int hintCount = length + 2;

        // Basis-Set
        hints.Add(GenerateHint_NothingCorrect(length, invalidDigits));
        hints.Add(GenerateHint_OneCorrectWellPlaced(length, secretCode, invalidDigits));
        hints.Add(GenerateHint_OneCorrectWrongPlaced(length, secretCode, invalidDigits));

        // Auffüllen
        while (hints.Count < hintCount)
        {
            int type = _rnd.Next(4);

            if (length == 3 && type == 2) type = 1;

            switch (type)
            {
                case 0: hints.Add(GenerateHint_OneCorrectWellPlaced(length, secretCode, invalidDigits)); break;
                case 1: hints.Add(GenerateHint_OneCorrectWrongPlaced(length, secretCode, invalidDigits)); break;
                case 2:
                    if (length >= 4) hints.Add(GenerateHint_TwoCorrectWrongPlaced(length, secretCode, invalidDigits));
                    else hints.Add(GenerateHint_OneCorrectWrongPlaced(length, secretCode, invalidDigits));
                    break;
                case 3: hints.Add(GenerateHint_NothingCorrect(length, invalidDigits)); break;
            }
        }

        return new LockRiddleGame
        {
            SecretCode = string.Join("", secretCode),
            Hints = hints.OrderBy(x => _rnd.Next()).ToList(),
            Difficulty = difficultyLength
        };
    }

    // --- LOGIK METHODEN (bleiben gleich) ---
    private LockHint GenerateHint_NothingCorrect(int length, List<int> invalidDigits)
    {
        var code = new int[length];
        for (int i = 0; i < length; i++) code[i] = invalidDigits[_rnd.Next(invalidDigits.Count)];
        return new LockHint { Code = string.Join(" ", code), Description = "Nichts ist korrekt", Icon = "❌" };
    }

    private LockHint GenerateHint_OneCorrectWellPlaced(int length, int[] secret, List<int> invalid)
    {
        var code = new int[length];
        for (int i = 0; i < length; i++) code[i] = invalid[_rnd.Next(invalid.Count)];
        int pos = _rnd.Next(length);
        code[pos] = secret[pos];
        return new LockHint { Code = string.Join(" ", code), Description = "Eine Zahl ist korrekt und am richtigen Platz", Icon = "🎯" };
    }

    private LockHint GenerateHint_OneCorrectWrongPlaced(int length, int[] secret, List<int> invalid)
    {
        var code = new int[length];
        for (int i = 0; i < length; i++) code[i] = invalid[_rnd.Next(invalid.Count)];
        int secretIdx = _rnd.Next(length);
        int digit = secret[secretIdx];
        int targetPos = _rnd.Next(length);
        while (targetPos == secretIdx) targetPos = _rnd.Next(length);
        code[targetPos] = digit;
        return new LockHint { Code = string.Join(" ", code), Description = "Eine Zahl korrekt, aber falscher Platz", Icon = "⚠️" };
    }

    private LockHint GenerateHint_TwoCorrectWrongPlaced(int length, int[] secret, List<int> invalid)
    {
        var code = new int[length];
        for (int i = 0; i < length; i++) code[i] = invalid[_rnd.Next(invalid.Count)];
        int idx1 = _rnd.Next(length);
        int idx2 = _rnd.Next(length);
        while (idx2 == idx1) idx2 = _rnd.Next(length);
        int pos1 = _rnd.Next(length);
        while (pos1 == idx1) pos1 = _rnd.Next(length);
        int pos2 = _rnd.Next(length);
        while (pos2 == idx2 || pos2 == pos1) pos2 = _rnd.Next(length);
        code[pos1] = secret[idx1];
        code[pos2] = secret[idx2];
        return new LockHint { Code = string.Join(" ", code), Description = "Zwei Zahlen korrekt, aber falscher Platz", Icon = "⚡" };
    }
}