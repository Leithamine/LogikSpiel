// File: Services/LockRiddleGeneratorService.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public class LockRiddleGeneratorService
{
    private readonly Random _rnd = new();

    public LockRiddleGame GenerateGame(string difficultyKey)
    {
        var (length, shownDigits, targetHints, rules) = GetSettings(difficultyKey);
        return GenerateWithCoverageGuarantee(length, shownDigits, targetHints, rules);
    }

    private static (int length, int shownDigits, int targetHints, List<(int well, int wrong)> rules) GetSettings(string key)
    {
        key = (key ?? "normal").ToLowerInvariant();

        return key switch
        {
            "easy" => (3, 3, 4, [(0, 0), (1, 0), (0, 1), (0, 2), (2, 0)]),
            "normal" => (4, 4, 5, [(0, 0), (1, 0), (0, 1), (0, 2), (2, 0), (1, 1)]),
            "hard" => (5, 5, 7, [(0, 0), (1, 0), (0, 1), (2, 0), (0, 2), (1, 1), (2, 1), (1, 2)]),
            "master" => (6, 6, 8, [(0, 0), (1, 0), (0, 1), (2, 0), (0, 2), (1, 1), (2, 1), (1, 2), (0, 3), (3, 0)]),
            _ => (4, 4, 5, [(0, 0), (1, 0), (0, 1), (0, 2), (2, 0), (1, 1)])
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // HAUPTALGORITHMUS MIT COVERAGE-GARANTIE
    // ═══════════════════════════════════════════════════════════════

    private LockRiddleGame GenerateWithCoverageGuarantee(
        int length, int shownDigits, int targetHints, List<(int well, int wrong)> rules)
    {
        length = Math.Clamp(length, 3, 6);
        shownDigits = Math.Clamp(shownDigits, length, length);
        targetHints = Math.Clamp(targetHints, 3, 12);

        for (int attempt = 0; attempt < 300; attempt++)
        {
            var (secret, invalid) = GenerateSecret(length);
            var hints = BuildHintsWithCoverageGuarantee(secret, invalid, length, targetHints, rules);

            if (hints == null || hints.Count < 3)
                continue;

            // Eindeutigkeitscheck
            var solver = new ConstraintSolver(length, hints);
            var solutions = solver.FindAllSolutions(maxSolutions: 2);
            string secretStr = string.Concat(secret);

            if (solutions.Count == 1 && solutions[0] == secretStr)
            {
                return new LockRiddleGame
                {
                    SecretCode = secretStr,
                    Hints = hints.OrderBy(_ => _rnd.Next()).ToList(),
                    Difficulty = length
                };
            }
        }

        return GenerateSafeFallback(length, targetHints);
    }

    // ═══════════════════════════════════════════════════════════════
    // HINT-GENERIERUNG MIT COVERAGE-GARANTIE
    // ═══════════════════════════════════════════════════════════════

    private List<LockHint>? BuildHintsWithCoverageGuarantee(
        int[] secret, List<int> invalid, int length, int targetHints, List<(int well, int wrong)> rules)
    {
        var hints = new List<LockHint>();
        var signatures = new HashSet<string>();
        var coveredDigits = new HashSet<int>();
        var coveredPositions = new HashSet<int>();

        // ══════════════════════════════════════════════════════════
        // PHASE 1: COVERAGE - Jede Secret-Ziffer MUSS vorkommen!
        // ══════════════════════════════════════════════════════════

        for (int pos = 0; pos < length; pos++)
        {
            if (coveredDigits.Contains(secret[pos]) && coveredPositions.Contains(pos))
                continue;

            var hint = CreateHintForPosition(secret, invalid, length, pos, coveredDigits);
            if (hint != null && TryAddHint(hints, signatures, hint))
            {
                UpdateCoverage(hint, coveredDigits, coveredPositions, secret);
            }
        }

        // Prüfe ob alle Ziffern abgedeckt sind
        if (coveredDigits.Count < length)
        {
            foreach (int digit in secret.Where(d => !coveredDigits.Contains(d)))
            {
                var hint = CreateHintShowingDigit(secret, invalid, length, digit);
                if (hint != null && TryAddHint(hints, signatures, hint))
                {
                    UpdateCoverage(hint, coveredDigits, coveredPositions, secret);
                }
            }
        }

        if (coveredDigits.Count < length)
            return null;

        // ══════════════════════════════════════════════════════════
        // PHASE 2: MAXIMAL 1x "Keine Zahl korrekt"
        // ══════════════════════════════════════════════════════════

        if (hints.Count < targetHints)
        {
            var nothingHint = CreateHintNothingCorrect(invalid, length);
            TryAddHint(hints, signatures, nothingHint);
        }

        // ══════════════════════════════════════════════════════════
        // PHASE 3: VARIANZ - Zusätzliche Hints nach Regeln
        // ══════════════════════════════════════════════════════════

        var shuffledRules = rules
            .Where(r => !(r.well == 0 && r.wrong == 0))
            .OrderBy(_ => _rnd.Next())
            .ToList();

        foreach (var (well, wrong) in shuffledRules)
        {
            if (hints.Count >= targetHints) break;

            var hint = CreateHintByRule(secret, invalid, length, well, wrong);
            if (hint != null)
            {
                TryAddHint(hints, signatures, hint);
            }
        }

        // ══════════════════════════════════════════════════════════
        // PHASE 4: AUFFÜLLEN falls nötig
        // ══════════════════════════════════════════════════════════

        int fillAttempts = 0;
        while (hints.Count < targetHints && fillAttempts++ < 30)
        {
            var validRules = rules.Where(r => r.well + r.wrong > 0 && r.well + r.wrong <= length).ToList();
            if (validRules.Count == 0) break;

            var rule = validRules[_rnd.Next(validRules.Count)];
            var hint = CreateHintByRule(secret, invalid, length, rule.well, rule.wrong);
            if (hint != null)
            {
                TryAddHint(hints, signatures, hint);
            }
        }

        return hints;
    }

    // ═══════════════════════════════════════════════════════════════
    // HINT-ERSTELLER
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt einen Hint der garantiert die Ziffer an Position 'targetPos' enthält.
    /// </summary>
    private LockHint CreateHintForPosition(int[] secret, List<int> invalid, int length, int targetPos, HashSet<int> alreadyCovered)
    {
        var slots = new string[length];
        var usedDigits = new HashSet<int>();

        // Entscheide: soll die Ziel-Ziffer korrekt platziert werden?
        bool placeCorrectly = _rnd.Next(3) > 0; // 66% korrekt platziert

        if (placeCorrectly)
        {
            slots[targetPos] = secret[targetPos].ToString();
            usedDigits.Add(secret[targetPos]);
        }
        else
        {
            // Ziffer an falscher Position
            var wrongPositions = Enumerable.Range(0, length).Where(p => p != targetPos).ToList();
            if (wrongPositions.Count > 0)
            {
                int wrongPos = wrongPositions[_rnd.Next(wrongPositions.Count)];
                slots[wrongPos] = secret[targetPos].ToString();
                usedDigits.Add(secret[targetPos]);
            }
            else
            {
                slots[targetPos] = secret[targetPos].ToString();
                usedDigits.Add(secret[targetPos]);
            }
        }

        // Restliche Positionen füllen
        var uncoveredDigits = secret.Where(d => !alreadyCovered.Contains(d) && !usedDigits.Contains(d)).ToList();

        for (int pos = 0; pos < length; pos++)
        {
            if (!string.IsNullOrEmpty(slots[pos])) continue;

            // 40% Chance eine noch nicht abgedeckte Secret-Ziffer zu verwenden
            if (uncoveredDigits.Count > 0 && _rnd.Next(5) < 2)
            {
                int digit = uncoveredDigits[_rnd.Next(uncoveredDigits.Count)];
                int secretPos = Array.IndexOf(secret, digit);

                if (secretPos != pos)
                {
                    slots[pos] = digit.ToString();
                    usedDigits.Add(digit);
                    uncoveredDigits.Remove(digit);
                    continue;
                }
            }

            int invDigit = PickInvalidDigit(invalid, usedDigits);
            slots[pos] = invDigit.ToString();
            usedDigits.Add(invDigit);
        }

        return BuildHint(slots, secret);
    }

    /// <summary>
    /// Erstellt einen Hint der garantiert eine bestimmte Ziffer zeigt.
    /// </summary>
    private LockHint CreateHintShowingDigit(int[] secret, List<int> invalid, int length, int digitToShow)
    {
        int secretPos = Array.IndexOf(secret, digitToShow);

        var slots = new string[length];
        var usedDigits = new HashSet<int>();

        bool placeCorrectly = _rnd.Next(2) == 0;

        if (placeCorrectly || secretPos < 0)
        {
            if (secretPos >= 0)
                slots[secretPos] = digitToShow.ToString();
        }
        else
        {
            var wrongPositions = Enumerable.Range(0, length).Where(p => p != secretPos).ToList();
            int wrongPos = wrongPositions[_rnd.Next(wrongPositions.Count)];
            slots[wrongPos] = digitToShow.ToString();
        }
        usedDigits.Add(digitToShow);

        for (int pos = 0; pos < length; pos++)
        {
            if (!string.IsNullOrEmpty(slots[pos])) continue;

            int invDigit = PickInvalidDigit(invalid, usedDigits);
            slots[pos] = invDigit.ToString();
            usedDigits.Add(invDigit);
        }

        return BuildHint(slots, secret);
    }

    /// <summary>
    /// Erstellt einen Hint mit nur Invalid-Ziffern → "Keine Zahl korrekt"
    /// </summary>
    private LockHint CreateHintNothingCorrect(List<int> invalid, int length)
    {
        var slots = new string[length];
        var usedDigits = new HashSet<int>();

        for (int pos = 0; pos < length; pos++)
        {
            int digit = PickInvalidDigit(invalid, usedDigits);
            slots[pos] = digit.ToString();
            usedDigits.Add(digit);
        }

        return new LockHint
        {
            Slots = slots.ToList(),
            Code = string.Join(" ", slots),
            WellPlaced = 0,
            WrongPlaced = 0,
            Description = "Keine Zahl korrekt",
            Icon = "❌"
        };
    }

    /// <summary>
    /// Erstellt einen Hint nach einer bestimmten Regel (well, wrong).
    /// </summary>
    private LockHint? CreateHintByRule(int[] secret, List<int> invalid, int length, int well, int wrong)
    {
        if (well < 0 || wrong < 0 || well + wrong > length || well + wrong == 0)
            return null;

        for (int attempt = 0; attempt < 50; attempt++)
        {
            var slots = new string[length];
            var usedDigits = new HashSet<int>();
            var usedSecretIndices = new HashSet<int>();

            var allPositions = Enumerable.Range(0, length).OrderBy(_ => _rnd.Next()).ToList();
            var wellPositions = allPositions.Take(well).ToList();
            var remainingPositions = allPositions.Skip(well).ToList();

            foreach (int pos in wellPositions)
            {
                slots[pos] = secret[pos].ToString();
                usedDigits.Add(secret[pos]);
                usedSecretIndices.Add(pos);
            }

            bool failed = false;
            for (int i = 0; i < wrong && !failed; i++)
            {
                var availableSecretIndices = Enumerable.Range(0, length)
                    .Where(idx => !usedSecretIndices.Contains(idx) && !usedDigits.Contains(secret[idx]))
                    .ToList();

                if (availableSecretIndices.Count == 0)
                {
                    failed = true;
                    break;
                }

                int secretIdx = availableSecretIndices[_rnd.Next(availableSecretIndices.Count)];
                int digitToPlace = secret[secretIdx];

                var validTargetPositions = remainingPositions
                    .Where(p => string.IsNullOrEmpty(slots[p]) && p != secretIdx)
                    .ToList();

                if (validTargetPositions.Count == 0)
                {
                    failed = true;
                    break;
                }

                int targetPos = validTargetPositions[_rnd.Next(validTargetPositions.Count)];
                slots[targetPos] = digitToPlace.ToString();
                usedDigits.Add(digitToPlace);
                usedSecretIndices.Add(secretIdx);
                remainingPositions.Remove(targetPos);
            }

            if (failed) continue;

            for (int pos = 0; pos < length; pos++)
            {
                if (!string.IsNullOrEmpty(slots[pos])) continue;

                int digit = PickInvalidDigit(invalid, usedDigits);
                slots[pos] = digit.ToString();
                usedDigits.Add(digit);
            }

            return BuildHint(slots, secret);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private (int[] secret, List<int> invalid) GenerateSecret(int length)
    {
        var pool = Enumerable.Range(0, 10).OrderBy(_ => _rnd.Next()).ToList();
        int[] secret = pool.Take(length).ToArray();
        List<int> invalid = pool.Skip(length).ToList();
        return (secret, invalid);
    }

    private int PickInvalidDigit(List<int> invalid, HashSet<int> usedDigits)
    {
        var candidates = invalid.Where(d => !usedDigits.Contains(d)).ToList();

        if (candidates.Count > 0)
            return candidates[_rnd.Next(candidates.Count)];

        return invalid[_rnd.Next(invalid.Count)];
    }

    private bool TryAddHint(List<LockHint> hints, HashSet<string> signatures, LockHint? hint)
    {
        if (hint == null) return false;

        string slotsKey = string.Join("|", hint.Slots.Select(s => string.IsNullOrEmpty(s) ? "_" : s));
        string sig = $"{hint.WellPlaced}:{hint.WrongPlaced}:{slotsKey}";

        if (signatures.Add(sig))
        {
            hints.Add(hint);
            return true;
        }

        return false;
    }

    private void UpdateCoverage(LockHint hint, HashSet<int> coveredDigits, HashSet<int> coveredPositions, int[] secret)
    {
        var secretSet = secret.ToHashSet();

        for (int i = 0; i < hint.Slots.Count && i < secret.Length; i++)
        {
            if (!int.TryParse(hint.Slots[i], out int d)) continue;

            if (secretSet.Contains(d))
                coveredDigits.Add(d);

            if (i < secret.Length && secret[i] == d)
                coveredPositions.Add(i);
        }
    }

    private LockHint BuildHint(string[] slots, int[] secret)
    {
        var normalizedSlots = slots.Select(s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim()).ToList();

        while (normalizedSlots.Count < secret.Length)
            normalizedSlots.Add("");
        if (normalizedSlots.Count > secret.Length)
            normalizedSlots = normalizedSlots.Take(secret.Length).ToList();

        var (well, wrong) = LockHintScoring.Score(normalizedSlots, secret);

        string Plural(int n, string s1, string s2) => n == 1 ? s1 : s2;

        string desc = (well, wrong) switch
        {
            (0, 0) => "Keine Zahl korrekt",
            ( > 0, 0) => $"{well} {Plural(well, "Zahl", "Zahlen")} korrekt und richtig platziert",
            (0, > 0) => $"{wrong} {Plural(wrong, "Zahl", "Zahlen")} korrekt, aber falsch platziert",
            _ => $"{well + wrong} {Plural(well + wrong, "Zahl", "Zahlen")} korrekt: {well} richtig platziert, {wrong} falsch platziert"
        };

        string icon = (well, wrong) switch
        {
            (0, 0) => "❌",
            ( > 0, > 0) => "🔎",
            ( > 0, _) => "🎯",
            _ => "⚠️"
        };

        return new LockHint
        {
            Slots = normalizedSlots,
            Code = string.Join(" ", normalizedSlots.Select(s => string.IsNullOrEmpty(s) ? "•" : s)),
            WellPlaced = well,
            WrongPlaced = wrong,
            Description = desc,
            Icon = icon
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // SICHERER FALLBACK
    // ═══════════════════════════════════════════════════════════════

    private LockRiddleGame GenerateSafeFallback(int length, int targetHints)
    {
        var (secret, invalid) = GenerateSecret(length);
        var hints = new List<LockHint>();
        var signatures = new HashSet<string>();

        var nothingHint = CreateHintNothingCorrect(invalid, length);
        TryAddHint(hints, signatures, nothingHint);

        for (int pos = 0; pos < length && hints.Count < targetHints; pos++)
        {
            var slots = new string[length];
            var usedDigits = new HashSet<int>();

            slots[pos] = secret[pos].ToString();
            usedDigits.Add(secret[pos]);

            for (int p = 0; p < length; p++)
            {
                if (!string.IsNullOrEmpty(slots[p])) continue;
                int d = PickInvalidDigit(invalid, usedDigits);
                slots[p] = d.ToString();
                usedDigits.Add(d);
            }

            var hint = BuildHint(slots, secret);
            TryAddHint(hints, signatures, hint);
        }

        for (int i = 0; i < length && hints.Count < targetHints; i++)
        {
            int nextPos = (i + 1) % length;

            var slots = new string[length];
            var usedDigits = new HashSet<int>();

            slots[nextPos] = secret[i].ToString();
            usedDigits.Add(secret[i]);

            for (int p = 0; p < length; p++)
            {
                if (!string.IsNullOrEmpty(slots[p])) continue;
                int d = PickInvalidDigit(invalid, usedDigits);
                slots[p] = d.ToString();
                usedDigits.Add(d);
            }

            var hint = BuildHint(slots, secret);
            TryAddHint(hints, signatures, hint);
        }

        return new LockRiddleGame
        {
            SecretCode = string.Concat(secret),
            Hints = hints.OrderBy(_ => _rnd.Next()).ToList(),
            Difficulty = length
        };
    }
}

// ═══════════════════════════════════════════════════════════════
// CONSTRAINT SOLVER
// ═══════════════════════════════════════════════════════════════

internal class ConstraintSolver
{
    private readonly int _length;
    private readonly List<LockHint> _hints;
    private readonly List<int>[] _possibleDigits;

    public ConstraintSolver(int length, List<LockHint> hints)
    {
        _length = length;
        _hints = hints;

        _possibleDigits = new List<int>[length];
        for (int i = 0; i < length; i++)
            _possibleDigits[i] = Enumerable.Range(0, 10).ToList();
    }

    public List<string> FindAllSolutions(int maxSolutions = 10)
    {
        PropagateConstraints();

        var solutions = new List<string>();
        var current = new int[_length];
        var used = new bool[10];

        void Search(int pos)
        {
            if (solutions.Count >= maxSolutions) return;

            if (pos == _length)
            {
                if (ValidateSolution(current))
                    solutions.Add(string.Concat(current));
                return;
            }

            foreach (var digit in _possibleDigits[pos])
            {
                if (used[digit]) continue;

                used[digit] = true;
                current[pos] = digit;
                Search(pos + 1);
                used[digit] = false;

                if (solutions.Count >= maxSolutions) return;
            }
        }

        Search(0);
        return solutions;
    }

    private void PropagateConstraints()
    {
        bool changed = true;
        int iterations = 0;

        while (changed && iterations++ < 20)
        {
            changed = false;

            foreach (var hint in _hints)
            {
                var hintDigits = hint.Slots
                    .Where(s => int.TryParse(s, out _))
                    .Select(int.Parse)
                    .ToHashSet();

                // (0,0) => diese Ziffern sind nirgendwo im Code
                if (hint.WellPlaced == 0 && hint.WrongPlaced == 0)
                {
                    for (int pos = 0; pos < _length; pos++)
                    {
                        int before = _possibleDigits[pos].Count;
                        _possibleDigits[pos].RemoveAll(d => hintDigits.Contains(d));
                        if (_possibleDigits[pos].Count < before) changed = true;
                    }
                    continue;
                }

                // well==0 & wrong>0 => jede gezeigte Ziffer kann NICHT an ihrer Position sein
                if (hint.WellPlaced == 0 && hint.WrongPlaced > 0)
                {
                    for (int pos = 0; pos < Math.Min(hint.Slots.Count, _length); pos++)
                    {
                        if (!int.TryParse(hint.Slots[pos], out int digit)) continue;

                        int before = _possibleDigits[pos].Count;
                        _possibleDigits[pos].Remove(digit);
                        if (_possibleDigits[pos].Count < before) changed = true;
                    }
                }
            }
        }
    }

    private bool ValidateSolution(int[] candidate)
    {
        foreach (var hint in _hints)
        {
            var (well, wrong) = LockHintScoring.Score(hint.Slots, candidate);

            if (well != hint.WellPlaced || wrong != hint.WrongPlaced)
                return false;
        }

        return true;
    }
}

// ═══════════════════════════════════════════════════════════════
// SCORING
// ═══════════════════════════════════════════════════════════════

internal static class LockHintScoring
{
    public static (int well, int wrong) Score(IReadOnlyList<string> slots, IReadOnlyList<int> code)
    {
        int well = 0;
        int wrong = 0;
        int n = Math.Min(slots.Count, code.Count);

        var slotInts = new int[slots.Count];
        var hasSlot = new bool[slots.Count];

        for (int i = 0; i < slots.Count; i++)
        {
            if (int.TryParse(slots[i], out int d))
            {
                slotInts[i] = d;
                hasSlot[i] = true;
            }
        }

        var codeUsed = new bool[code.Count];
        var slotUsed = new bool[slots.Count];

        for (int i = 0; i < n; i++)
        {
            if (hasSlot[i] && slotInts[i] == code[i])
            {
                well++;
                codeUsed[i] = true;
                slotUsed[i] = true;
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!hasSlot[i] || slotUsed[i]) continue;

            for (int j = 0; j < code.Count; j++)
            {
                if (!codeUsed[j] && code[j] == slotInts[i])
                {
                    wrong++;
                    codeUsed[j] = true;
                    break;
                }
            }
        }

        return (well, wrong);
    }
}