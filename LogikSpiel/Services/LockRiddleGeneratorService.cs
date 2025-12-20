#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

// ────────────────────────────────────────────────────────────────
// LockRiddleGeneratorService.cs
// ────────────────────────────────────────────────────────────────
// Wichtige Fixes gegenüber deinen letzten Versionen:
// 1) Hint-Deduplizierung (Signature) ist POSITIONSBASIERT (SlotsKey),
//    damit wertvolle Positions-Constraints nicht wegfallen.
// 2) BuildHint berechnet Well/Wrong immer aus Slots vs. Secret (Wahrheit).
// 3) ConstraintSolver.ValidateSolution nutzt DIESELBE Scoring-Logik (shared).
// ────────────────────────────────────────────────────────────────

public class LockRiddleGeneratorService
{
    private readonly Random _rnd = new();

    public LockRiddleGame GenerateGame(string difficultyKey)
    {
        var (length, shownDigits, targetHints, rules) = GetSettings(difficultyKey);
        return GenerateIntelligent(length, shownDigits, targetHints, rules);
    }

    private static (int length, int shownDigits, int targetHints, List<(int well, int wrong)> rules) GetSettings(string key)
    {
        key = (key ?? "normal").ToLowerInvariant();

        return key switch
        {
            "easy" => (3, 3, 4,
            [
                (0,0), (1,0), (0,1), (0,2), (2,0)
            ]),

            "normal" => (4, 4, 5,
            [
                (0,0), (1,0), (0,1), (0,2), (2,0), (1,1)
            ]),

            "hard" => (5, 5, 7,
            [
                (0,0), (1,0), (0,1), (2,0), (0,2), (1,1), (2,1), (1,2)
            ]),

            "master" => (6, 6, 8,
            [
                (0,0), (1,0), (0,1), (2,0), (0,2), (1,1), (2,1), (1,2), (0,3), (3,0)
            ]),

            _ => (4, 4, 5,
            [
                (0,0), (1,0), (0,1), (0,2), (2,0), (1,1)
            ])
        };
    }

    // ═══════════════════════════════════════════════════════════
    // INTELLIGENTE GENERIERUNG
    // ═══════════════════════════════════════════════════════════

    private LockRiddleGame GenerateIntelligent(int length, int shownDigits, int targetHints, List<(int well, int wrong)> rules)
    {
        length = Math.Clamp(length, 3, 6);
        shownDigits = Math.Clamp(shownDigits, 1, length);
        targetHints = Math.Clamp(targetHints, 3, 12);

        for (int attempt = 0; attempt < 200; attempt++)
        {
            // 1) Secret + invalid generieren (unique digits)
            var (secret, invalid) = GenerateSecret(length);

            // 2) Hints bauen
            var hints = BuildIntelligentHints(secret, invalid, length, shownDigits, targetHints, rules);
            if (hints == null || hints.Count < 3) continue;

            // 3) Eindeutigkeitscheck
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

        return GenerateFallbackGame(length, shownDigits, targetHints);
    }

    // ═══════════════════════════════════════════════════════════
    // INTELLIGENTE HINT-GENERIERUNG (Phasen)
    // ═══════════════════════════════════════════════════════════

    private List<LockHint>? BuildIntelligentHints(
        int[] secret,
        List<int> invalid,
        int length,
        int shownDigits,
        int targetHints,
        List<(int well, int wrong)> rules)
    {
        var hints = new List<LockHint>();
        var signatures = new HashSet<string>(); // positionsbasierte Signatur!
        var coveredDigits = new HashSet<int>();
        var coveredPositions = new HashSet<int>();

        // Phase 1: Basis
        AddBaseHints(hints, signatures, secret, invalid, length, shownDigits, targetHints);
        UpdateCoverage(hints, coveredDigits, coveredPositions, secret);

        // Phase 2: Coverage (alle Secret-Ziffern sollen vorkommen)
        AddCoverageHints(hints, signatures, secret, invalid, length, shownDigits, coveredDigits, targetHints);
        UpdateCoverage(hints, coveredDigits, coveredPositions, secret);

        // Phase 3: Positions-Hints
        AddPositionHints(hints, signatures, secret, invalid, length, shownDigits, coveredPositions, targetHints);
        UpdateCoverage(hints, coveredDigits, coveredPositions, secret);

        // Phase 4: Strategisch auffüllen
        AddStrategicHints(hints, signatures, secret, invalid, length, shownDigits, rules, targetHints);

        // Absicherung: Jede Ziffer muss mindestens einmal in den Slots vorkommen
        EnsureMissingDigitsCovered(hints, signatures, secret, invalid, length, shownDigits, coveredDigits, coveredPositions, targetHints);

        UpdateCoverage(hints, coveredDigits, coveredPositions, secret);

        // Qualitätsprüfung (minimal)
        if (hints.Count < 3) return null;
        if (coveredDigits.Count < length) return null;

        return hints;
    }

    // ───────────────────────────────────────────────────────────
    // Phase 1: Basis-Hints
    // ───────────────────────────────────────────────────────────

    private void AddBaseHints(
        List<LockHint> hints,
        HashSet<string> signatures,
        int[] secret,
        List<int> invalid,
        int length,
        int shownDigits,
        int maxHints)
    {
        if (hints.Count >= maxHints) return;

        // Immer: Ein "nichts korrekt"
        TryAddHint(hints, signatures, CreateHint_NothingCorrect(length, shownDigits, secret, invalid));

        if (hints.Count >= maxHints) return;

        // Starter
        if (length <= 4)
        {
            TryAddHint(hints, signatures, CreateHint_ByRule(length, shownDigits, secret, invalid, 1, 0));
        }
        else
        {
            TryAddHint(hints, signatures, CreateHint_ByRule(length, shownDigits, secret, invalid, 1, 0));
            if (hints.Count < maxHints)
                TryAddHint(hints, signatures, CreateHint_ByRule(length, shownDigits, secret, invalid, 0, 1));
        }
    }

    // ───────────────────────────────────────────────────────────
    // Phase 2: Coverage-Hints
    // ───────────────────────────────────────────────────────────

    private void AddCoverageHints(
        List<LockHint> hints,
        HashSet<string> signatures,
        int[] secret,
        List<int> invalid,
        int length,
        int shownDigits,
        HashSet<int> coveredDigits,
        int maxHints)
    {
        var missing = secret.Where(d => !coveredDigits.Contains(d)).ToHashSet();
        int attempts = 0;

        while (missing.Count > 0 && hints.Count < maxHints && attempts < 50)
        {
            attempts++;

            int countToReveal = Math.Min(missing.Count, _rnd.Next(1, 3));
            var digitsToReveal = missing.OrderBy(_ => _rnd.Next()).Take(countToReveal).ToList();

            var hint = CreateHint_CoverageOptimized(length, shownDigits, secret, invalid, digitsToReveal);

            if (hint != null && TryAddHint(hints, signatures, hint))
            {
                foreach (var slot in hint.Slots)
                {
                    if (int.TryParse(slot, out int d) && missing.Contains(d))
                        missing.Remove(d);
                }
            }
        }
    }

    // ───────────────────────────────────────────────────────────
    // Phase 3: Positions-Hints
    // ───────────────────────────────────────────────────────────

    private void AddPositionHints(
        List<LockHint> hints,
        HashSet<string> signatures,
        int[] secret,
        List<int> invalid,
        int length,
        int shownDigits,
        HashSet<int> coveredPositions,
        int maxHints)
    {
        if (hints.Count >= maxHints) return;

        var criticalPositions = Enumerable.Range(0, length)
            .Where(p => !coveredPositions.Contains(p))
            .OrderBy(_ => _rnd.Next())
            .Take(2)
            .ToList();

        foreach (var pos in criticalPositions)
        {
            if (hints.Count >= maxHints) break;

            var hint = CreateHint_PositionFocused(length, shownDigits, secret, invalid, pos);
            TryAddHint(hints, signatures, hint);
        }
    }

    // ───────────────────────────────────────────────────────────
    // Phase 4: Strategische Hints
    // ───────────────────────────────────────────────────────────

    private void AddStrategicHints(
        List<LockHint> hints,
        HashSet<string> signatures,
        int[] secret,
        List<int> invalid,
        int length,
        int shownDigits,
        List<(int well, int wrong)> rules,
        int targetHints)
    {
        if (hints.Count >= targetHints) return;

        var prioritizedRules = rules
            .OrderByDescending(r => r.well + r.wrong)
            .ThenBy(_ => _rnd.Next())
            .ToList();

        int attempts = 0;
        foreach (var rule in prioritizedRules)
        {
            if (hints.Count >= targetHints) break;
            if (attempts++ > 30) break;

            if (rule.well == 0 && rule.wrong == 0)
                TryAddHint(hints, signatures, CreateHint_NothingCorrect(length, shownDigits, secret, invalid));
            else
                TryAddHint(hints, signatures, CreateHint_ByRule(length, shownDigits, secret, invalid, rule.well, rule.wrong));
        }

        attempts = 0;
        while (hints.Count < targetHints && attempts++ < 20)
        {
            var rule = prioritizedRules[_rnd.Next(Math.Min(prioritizedRules.Count, 5))];

            if (rule.well == 0 && rule.wrong == 0)
                TryAddHint(hints, signatures, CreateHint_NothingCorrect(length, shownDigits, secret, invalid));
            else
                TryAddHint(hints, signatures, CreateHint_ByRule(length, shownDigits, secret, invalid, rule.well, rule.wrong));
        }
    }

    // ═══════════════════════════════════════════════════════════
    // OPTIMIERTE HINT-ERSTELLER
    // ═══════════════════════════════════════════════════════════

    private LockHint? CreateHint_CoverageOptimized(
        int length,
        int shownDigits,
        int[] secret,
        List<int> invalid,
        List<int> digitsToReveal)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var slots = Enumerable.Repeat("", length).ToArray();
            var shownPos = PickDistinctPositions(length, shownDigits);
            var usedHintDigits = new HashSet<int>();

            bool anyPlaced = false;

            foreach (var digit in digitsToReveal)
            {
                if (shownPos.Count == 0) break;

                int secretPos = Array.IndexOf(secret, digit);
                if (secretPos < 0) continue;

                bool placeCorrect = _rnd.Next(3) == 0 && shownPos.Contains(secretPos);

                if (placeCorrect)
                {
                    slots[secretPos] = digit.ToString();
                    shownPos.Remove(secretPos);
                    usedHintDigits.Add(digit);
                    anyPlaced = true;
                }
                else
                {
                    var wrongPos = shownPos.Where(p => p != secretPos).ToList();
                    if (wrongPos.Count > 0)
                    {
                        int pos = wrongPos[_rnd.Next(wrongPos.Count)];
                        slots[pos] = digit.ToString();
                        shownPos.Remove(pos);
                        usedHintDigits.Add(digit);
                        anyPlaced = true;
                    }
                }
            }

            foreach (var pos in shownPos)
            {
                if (!string.IsNullOrEmpty(slots[pos])) continue;

                int d = PickInvalidDistinct(invalid, usedHintDigits);
                usedHintDigits.Add(d);
                slots[pos] = d.ToString();
            }

            if (anyPlaced)
                return BuildHint(slots, secret);
        }

        return null;
    }

    // ───────────────────────────────────────────────────────────
    // Phase 2.5: fehlende Ziffern gezielt abdecken
    // ───────────────────────────────────────────────────────────

    private void EnsureMissingDigitsCovered(
        List<LockHint> hints,
        HashSet<string> signatures,
        int[] secret,
        List<int> invalid,
        int length,
        int shownDigits,
        HashSet<int> coveredDigits,
        HashSet<int> coveredPositions,
        int maxHints)
    {
        var missing = secret.Where(d => !coveredDigits.Contains(d)).ToList();
        int attempts = 0;

        while (missing.Count > 0 && hints.Count < maxHints + 2 && attempts++ < 80)
        {
            int digit = missing[_rnd.Next(missing.Count)];
            int targetPos = Array.IndexOf(secret, digit);
            if (targetPos < 0) targetPos = _rnd.Next(length);

            var hint = CreateHint_PositionFocused(length, shownDigits, secret, invalid, targetPos);
            if (hint != null && TryAddHint(hints, signatures, hint))
            {
                UpdateCoverage(hints, coveredDigits, coveredPositions, secret);
                missing = secret.Where(d => !coveredDigits.Contains(d)).ToList();
            }
        }
    }

    private LockHint? CreateHint_PositionFocused(
        int length,
        int shownDigits,
        int[] secret,
        List<int> invalid,
        int targetPos)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var slots = Enumerable.Repeat("", length).ToArray();
            var shownPos = new List<int> { targetPos };

            while (shownPos.Count < shownDigits)
            {
                int p = _rnd.Next(length);
                if (!shownPos.Contains(p))
                    shownPos.Add(p);
            }

            var usedHintDigits = new HashSet<int>();
            bool anyPlaced = false;

            // Target: 50% korrekt, 50% andere Secret-Ziffer
            if (_rnd.Next(2) == 0)
            {
                slots[targetPos] = secret[targetPos].ToString();
                usedHintDigits.Add(secret[targetPos]);
                anyPlaced = true;
            }
            else
            {
                var otherSecretDigits = secret.Where((d, i) => i != targetPos && !usedHintDigits.Contains(d)).ToList();
                if (otherSecretDigits.Count > 0)
                {
                    int d = otherSecretDigits[_rnd.Next(otherSecretDigits.Count)];
                    slots[targetPos] = d.ToString();
                    usedHintDigits.Add(d);
                    anyPlaced = true;
                }
            }

            foreach (var pos in shownPos)
            {
                if (!string.IsNullOrEmpty(slots[pos])) continue;

                if (_rnd.Next(3) == 0)
                {
                    var available = secret.Where((d, i) => i != pos && !usedHintDigits.Contains(d)).ToList();
                    if (available.Count > 0)
                    {
                        int d = available[_rnd.Next(available.Count)];
                        slots[pos] = d.ToString();
                        usedHintDigits.Add(d);
                        anyPlaced = true;
                        continue;
                    }
                }

                int invD = PickInvalidDistinct(invalid, usedHintDigits);
                usedHintDigits.Add(invD);
                slots[pos] = invD.ToString();
            }

            if (anyPlaced)
                return BuildHint(slots, secret);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // "STANDARD"-ERSTELLER
    // ═══════════════════════════════════════════════════════════

    private LockHint CreateHint_NothingCorrect(int length, int shownDigits, int[] secret, List<int> invalid)
    {
        var slots = Enumerable.Repeat("", length).ToArray();
        var shownPos = PickDistinctPositions(length, shownDigits);
        var usedDigits = new HashSet<int>();

        foreach (int p in shownPos)
        {
            int d = PickInvalidDistinct(invalid, usedDigits);
            usedDigits.Add(d);
            slots[p] = d.ToString();
        }

        return BuildHint(slots, secret);
    }

    private LockHint? CreateHint_ByRule(int length, int shownDigits, int[] secret, List<int> invalid, int well, int wrong)
    {
        if (well < 0 || wrong < 0 || well + wrong > shownDigits)
            return null;

        for (int tries = 0; tries < 50; tries++)
        {
            var slots = Enumerable.Repeat("", length).ToArray();
            var shownPos = PickDistinctPositions(length, shownDigits);

            var wellPos = PickSubset(shownPos, well);
            var freePos = shownPos.Where(p => !wellPos.Contains(p)).ToList();

            var usedHintDigits = new HashSet<int>();
            var usedSecretIdx = new HashSet<int>();

            // Well placed
            foreach (int p in wellPos)
            {
                slots[p] = secret[p].ToString();
                usedHintDigits.Add(secret[p]);
                usedSecretIdx.Add(p);
            }

            // Wrong placed
            if (freePos.Count < wrong) continue;

            bool failed = false;
            for (int k = 0; k < wrong; k++)
            {
                var candSecretIdx = Enumerable.Range(0, length)
                    .Where(i => !usedSecretIdx.Contains(i) && !usedHintDigits.Contains(secret[i]))
                    .ToList();

                if (candSecretIdx.Count == 0) { failed = true; break; }

                int sIdx = candSecretIdx[_rnd.Next(candSecretIdx.Count)];
                var candPos = freePos.Where(p => p != sIdx).ToList();
                if (candPos.Count == 0) { failed = true; break; }

                int tPos = candPos[_rnd.Next(candPos.Count)];

                slots[tPos] = secret[sIdx].ToString();
                usedHintDigits.Add(secret[sIdx]);
                usedSecretIdx.Add(sIdx);
                freePos.Remove(tPos);
            }

            if (failed) continue;

            // Rest füllen mit invalid
            foreach (int p in shownPos)
            {
                if (!string.IsNullOrEmpty(slots[p])) continue;

                int d = PickInvalidDistinct(invalid, usedHintDigits);
                usedHintDigits.Add(d);
                slots[p] = d.ToString();
            }

            // Wahrheit wird aus Slots berechnet
            return BuildHint(slots, secret);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════

    private (int[] secret, List<int> invalid) GenerateSecret(int length)
    {
        var pool = Enumerable.Range(0, 10).ToList();
        int[] secret = new int[length];

        for (int i = 0; i < length; i++)
        {
            int idx = _rnd.Next(pool.Count);
            secret[i] = pool[idx];
            pool.RemoveAt(idx);
        }

        // pool enthält jetzt die invalid digits
        return (secret, pool);
    }

    private bool TryAddHint(List<LockHint> hints, HashSet<string> signatures, LockHint? hint)
    {
        if (hint == null) return false;

        // ✅ Positionsbasierte Signatur: verhindert das Wegwerfen guter Hints
        string slotsKey = string.Join("|", hint.Slots.Select(s => string.IsNullOrEmpty(s) ? "_" : s));
        string sig = $"{hint.WellPlaced}:{hint.WrongPlaced}:{slotsKey}";

        if (signatures.Add(sig))
        {
            hints.Add(hint);
            return true;
        }

        return false;
    }

    private void UpdateCoverage(List<LockHint> hints, HashSet<int> coveredDigits, HashSet<int> coveredPositions, int[] secret)
    {
        var secretSet = secret.ToHashSet();

        foreach (var hint in hints)
        {
            for (int i = 0; i < hint.Slots.Count && i < secret.Length; i++)
            {
                if (!int.TryParse(hint.Slots[i], out int d)) continue;

                if (secretSet.Contains(d))
                    coveredDigits.Add(d);

                if (secret[i] == d)
                    coveredPositions.Add(i);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // BUILD: Wahrheit aus Slots berechnen (shared scoring)
    // ═══════════════════════════════════════════════════════════

    private LockHint BuildHint(string[] slots, int[] secret)
    {
        // Normalize slot list to avoid nulls/short arrays after Serialization/Deserialization
        var normalizedSlots = slots
            .Select(s => string.IsNullOrWhiteSpace(s) ? "" : s.Trim())
            .ToList();

        if (normalizedSlots.Count < secret.Length)
        {
            for (int i = normalizedSlots.Count; i < secret.Length; i++)
                normalizedSlots.Add("");
        }
        else if (normalizedSlots.Count > secret.Length)
        {
            normalizedSlots = normalizedSlots.Take(secret.Length).ToList();
        }

        var (well, wrong) = LockHintScoring.Score(normalizedSlots, secret);
        string Plural(int n, string s1, string s2) => n == 1 ? s1 : s2;

        string desc;
        if (well == 0 && wrong == 0)
        {
            desc = "Keine Zahl korrekt";
        }
        else if (well > 0 && wrong == 0)
        {
            desc = $"{well} {Plural(well, "Zahl", "Zahlen")} korrekt und richtig platziert";
        }
        else if (well == 0 && wrong > 0)
        {
            desc = $"{wrong} {Plural(wrong, "Zahl", "Zahlen")} korrekt, aber falsch platziert";
        }
        else
        {
            int total = well + wrong;
            desc = $"{total} {Plural(total, "Zahl", "Zahlen")} korrekt: " +
                   $"{well} richtig platziert, {wrong} falsch platziert";
        }

        string icon =
            (well == 0 && wrong == 0) ? "❌"
            : (well > 0 && wrong > 0) ? "🔎"
            : (well > 0) ? "🎯"
            : "⚠️";

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

    private List<int> PickDistinctPositions(int length, int count)
    {
        count = Math.Clamp(count, 0, length);

        var all = Enumerable.Range(0, length).ToList();
        var res = new List<int>(count);

        for (int i = 0; i < count; i++)
        {
            int idx = _rnd.Next(all.Count);
            res.Add(all[idx]);
            all.RemoveAt(idx);
        }

        return res;
    }

    private HashSet<int> PickSubset(List<int> source, int count)
    {
        count = Math.Clamp(count, 0, source.Count);

        var temp = new List<int>(source);
        var res = new HashSet<int>();

        for (int i = 0; i < count && temp.Count > 0; i++)
        {
            int idx = _rnd.Next(temp.Count);
            res.Add(temp[idx]);
            temp.RemoveAt(idx);
        }

        return res;
    }

    private int PickInvalidDistinct(List<int> invalid, HashSet<int> used)
    {
        // invalid enthält NIE Secret-Ziffern (kommt aus Pool nach Secret-Entnahme)
        var candidates = invalid.Where(d => !used.Contains(d)).ToList();
        if (candidates.Count == 0)
            return invalid[_rnd.Next(invalid.Count)];

        return candidates[_rnd.Next(candidates.Count)];
    }

    // ═══════════════════════════════════════════════════════════
    // FALLBACK
    // ═══════════════════════════════════════════════════════════

    private LockRiddleGame GenerateFallbackGame(int length, int shownDigits, int targetHints)
    {
        var (secret, invalid) = GenerateSecret(length);

        var hints = new List<LockHint>();
        var signatures = new HashSet<string>();

        TryAddHint(hints, signatures, CreateHint_NothingCorrect(length, shownDigits, secret, invalid));

        // einfache, stabile Hints: jeweils 1 well auf verschiedenen Positionen (wenn möglich)
        for (int i = 0; i < length && hints.Count < targetHints; i++)
        {
            var slots = Enumerable.Repeat("", length).ToArray();

            // zeige genau shownDigits Positionen (inkl. i, falls möglich)
            var shownPos = PickDistinctPositions(length, Math.Min(shownDigits, length));
            if (!shownPos.Contains(i))
            {
                // erzwinge i rein, ersetze die erste Position
                if (shownPos.Count > 0) shownPos[0] = i;
                else shownPos.Add(i);
                shownPos = shownPos.Distinct().Take(Math.Min(shownDigits, length)).ToList();
            }

            var usedDigits = new HashSet<int>();

            slots[i] = secret[i].ToString();
            usedDigits.Add(secret[i]);

            foreach (var p in shownPos)
            {
                if (p == i) continue;

                int d = PickInvalidDistinct(invalid, usedDigits);
                usedDigits.Add(d);
                slots[p] = d.ToString();
            }

            TryAddHint(hints, signatures, BuildHint(slots, secret));
        }

        return new LockRiddleGame
        {
            SecretCode = string.Concat(secret),
            Hints = hints.OrderBy(_ => _rnd.Next()).ToList(),
            Difficulty = length
        };
    }
}

// ────────────────────────────────────────────────────────────────
// Shared scoring: gleiche Logik für Generator & Solver
// ────────────────────────────────────────────────────────────────
internal static class LockHintScoring
{
    public static (int well, int wrong) Score(IReadOnlyList<string> slots, IReadOnlyList<int> code)
    {
        int well = 0;
        int wrong = 0;

        int n = Math.Min(slots.Count, code.Count);

        // parse
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

        // well
        for (int i = 0; i < n; i++)
        {
            if (hasSlot[i] && slotInts[i] == code[i])
            {
                well++;
                codeUsed[i] = true;
                slotUsed[i] = true;
            }
        }

        // wrong
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

// ═══════════════════════════════════════════════════════════
// ConstraintSolver
// ═══════════════════════════════════════════════════════════

public class ConstraintSolver
{
    private readonly int _length;
    private readonly List<LockHint> _hints;
    private readonly List<int>[] _possibleDigits; // pro Position: Kandidaten

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
        var used = new bool[10]; // Unique digits (wie Generator)

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
