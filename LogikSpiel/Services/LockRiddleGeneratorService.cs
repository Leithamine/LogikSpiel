// File: Services/LockRiddleGeneratorService.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Core;
using LogikSpiel.Model;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.Services;

public class LockRiddleGeneratorService
{
    private Random _rnd = new();

    private sealed record DifficultyStyle(
        bool AllowSinglePinDisambiguation,
        bool AllowSinglePinFill,
        List<(int well, int wrong)> PreferredDisambiguationRules
    );

    public LockRiddleGame GenerateGame(string difficultyKey, int levelNumber)
    {
        var normalizedLevel = Math.Max(1, levelNumber);
        int seed = SeedHelper.CalculateSeed("lockriddle", difficultyKey, normalizedLevel);
        _rnd = new Random(seed);

        var (length, minCoveredDigits, targetHints, rules, style) = GetSettings(difficultyKey);

        return GenerateWithHardGuarantee(length, minCoveredDigits, targetHints, rules, style, _rnd, seed);
    }

    public LockRiddleGame GenerateGame(string difficultyKey)
        => GenerateGame(difficultyKey, levelNumber: 1);


    public bool IsGameValid(LockRiddleGame game)
    {
        if (game == null) return false;
        if (string.IsNullOrWhiteSpace(game.SecretCode)) return false;
        if (game.Hints == null || game.Hints.Count < 3) return false;

        if (game.SecretCode.Length is < 3 or > 6) return false;
        if (!game.SecretCode.All(char.IsDigit)) return false;
        if (game.SecretCode.Distinct().Count() != game.SecretCode.Length) return false;

        var secret = game.SecretCode.Select(c => c - '0').ToArray();
        int length = secret.Length;

        foreach (var h in game.Hints)
        {
            if (h == null || h.Slots == null) return false;
            if (h.Slots.Count != length) return false;

            // keine leeren Slots, nur digits (Wiederholungen im Hinweis sind erlaubt)
            var digits = new List<int>(length);
            foreach (var s in h.Slots)
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                if (!int.TryParse(s.Trim(), out var d)) return false;
                digits.Add(d);
            }
            var (well, wrong) = LockHintScoring.Score(h.Slots, secret);
            if (well != h.WellPlaced || wrong != h.WrongPlaced)
                return false;
        }

        if (!SatisfiesNothingCorrectCoverageRule(game.Hints))
            return false;

        var solver = new ConstraintSolver(length, game.Hints);
        var sols = solver.FindAllSolutions(maxSolutions: 2);
        return sols.Count == 1 && sols[0] == game.SecretCode;
    }

    private static (int length, int minCoveredDigits, int targetHints, List<(int well, int wrong)> rules, DifficultyStyle style) GetSettings(string key)
    {
        key = (key ?? "normal").ToLowerInvariant();

        return key switch
        {
            "easy" => (
                length: 3,
                minCoveredDigits: 3,
                targetHints: 4,
                rules: [(0, 0), (1, 0), (0, 1), (0, 2), (2, 0), (1, 1)],
                style: new DifficultyStyle(
                    AllowSinglePinDisambiguation: true,
                    AllowSinglePinFill: true,
                    PreferredDisambiguationRules: [(1, 1), (0, 2), (2, 0)]
                )
            ),

            "normal" => (
                length: 4,
                minCoveredDigits: 4,
                targetHints: 6,
                rules: [(0, 0), (1, 0), (0, 1), (0, 2), (2, 0), (1, 1), (2, 1)],
                style: new DifficultyStyle(
                    AllowSinglePinDisambiguation: true,
                    AllowSinglePinFill: true,
                    PreferredDisambiguationRules: [(1, 1), (2, 0), (0, 2), (2, 1)]
                )
            ),

            "hard" => (
                length: 5,
                minCoveredDigits: 4,
                targetHints: 8,
                rules: [(0, 0), (1, 0), (0, 1), (2, 0), (0, 2), (1, 1), (2, 1), (1, 2), (3, 0), (0, 3)],
                style: new DifficultyStyle(
                    AllowSinglePinDisambiguation: true,
                    AllowSinglePinFill: false,
                    PreferredDisambiguationRules: [(2, 1), (1, 2), (3, 0), (0, 3), (1, 1), (2, 0), (0, 2)]
                )
            ),

            // ✅ Master: (0,0) soll enthalten sein; falls unique invalid digits nicht reichen,
            // wird der (0,0)-Hinweis mit Wiederholungen erzeugt.
            "master" => (
                length: 6,
                minCoveredDigits: 5,
                targetHints: 9,
                rules: [(0, 0), (2, 0), (0, 2), (1, 1), (2, 1), (1, 2), (3, 0), (0, 3), (2, 2)],
                style: new DifficultyStyle(
                    AllowSinglePinDisambiguation: false,
                    AllowSinglePinFill: false,
                    PreferredDisambiguationRules: [(2, 2), (2, 1), (1, 2), (3, 0), (0, 3), (1, 1), (2, 0), (0, 2)]
                )
            ),

            _ => (
                length: 4,
                minCoveredDigits: 4,
                targetHints: 6,
                rules: [(0, 0), (1, 0), (0, 1), (0, 2), (2, 0), (1, 1), (2, 1)],
                style: new DifficultyStyle(
                    AllowSinglePinDisambiguation: true,
                    AllowSinglePinFill: true,
                    PreferredDisambiguationRules: [(1, 1), (2, 0), (0, 2), (2, 1)]
                )
            )
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // HARTE GARANTIE: immer eindeutig + keine Leer-Slots
    // ═══════════════════════════════════════════════════════════════

    private LockRiddleGame GenerateWithHardGuarantee(
        int length, int minCoveredDigits, int targetHints, List<(int well, int wrong)> rules, DifficultyStyle style, Random rnd, int baseSeed)
    {
        length = Math.Clamp(length, 3, 6);
        minCoveredDigits = Math.Clamp(minCoveredDigits, 2, length);
        targetHints = Math.Clamp(targetHints, 3, 12);

        int maxHints = Math.Clamp(targetHints + 5, targetHints, 12);

        int maxAttempts = 700;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var (secret, invalid) = GenerateSecret(length, rnd);

            var hints = BuildHintsWithCoverageGuarantee(secret, invalid, length, minCoveredDigits, targetHints, rules, rnd);
            if (hints == null || hints.Count < 3) continue;

            var signatures = new HashSet<string>(hints.Select(SignatureOf));

            EnsureMinimumHints(secret, invalid, length, hints, signatures, rules, targetHints, maxHints, style, rnd);

            var unique = ForceUniqueness(secret, invalid, length, hints, signatures, maxHints, style, rnd);
            if (unique == null) continue;
            if (!SatisfiesNothingCorrectCoverageRule(unique)) continue;

            var solver = new ConstraintSolver(length, unique);
            var solutions = solver.FindAllSolutions(maxSolutions: 2);
            string secretStr = string.Concat(secret);

            if (solutions.Count == 1 && solutions[0] == secretStr)
            {
                return new LockRiddleGame
                {
                    SecretCode = secretStr,
                    Hints = unique.OrderBy(_ => rnd.Next()).ToList(),
                    Difficulty = length
                };
            }
        }

        // Ultima Ratio (immer eindeutig)
        var ultraSafe = GenerateUltraSafe(length, targetHints, rules, rnd);
        if (ultraSafe != null) return ultraSafe;

        // Deterministischer Fallback: feste Seed-Reihe statt sofort Exception
        var fallback = GenerateDeterministicFallback(length, targetHints, rules, baseSeed);
        if (fallback != null) return fallback;

        throw new InvalidOperationException("Konnte kein eindeutiges Lock-Riddle erzeugen.");
    }


    private LockRiddleGame? GenerateDeterministicFallback(int length, int targetHints, List<(int well, int wrong)> rules, int baseSeed)
    {
        for (int i = 0; i < 256; i++)
        {
            int seed = StableDeterministicSeed(baseSeed, length, targetHints, rules.Count, i);
            var rnd = new Random(seed);
            var candidate = GenerateUltraSafe(length, targetHints, rules, rnd);
            if (candidate != null)
                return candidate;
        }

        return null;
    }


    private static int StableDeterministicSeed(int baseSeed, int length, int targetHints, int ruleCount, int iteration)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + baseSeed;
            h = h * 31 + length;
            h = h * 31 + targetHints;
            h = h * 31 + ruleCount;
            h = h * 31 + iteration;
            return h & 0x7fffffff;
        }
    }

    private static string SignatureOf(LockHint hint)
    {
        string slotsKey = string.Join("|", hint.Slots.Select(s => s.Trim()));
        return $"{hint.WellPlaced}:{hint.WrongPlaced}:{slotsKey}";
    }

    private void EnsureMinimumHints(
        int[] secret,
        List<int> invalid,
        int length,
        List<LockHint> hints,
        HashSet<string> signatures,
        List<(int well, int wrong)> rules,
        int minHints,
        int maxHints,
        DifficultyStyle style,
        Random rnd)
    {
        int attempts = 0;
        int maxAttempts = 200;
        while (hints.Count < minHints && hints.Count < maxHints && attempts++ < maxAttempts)
        {
            var validRules = rules
                .Where(r => r.well + r.wrong >= 2 && r.well + r.wrong <= length)
                .ToList();

            if (validRules.Count == 0)
                validRules = rules.Where(r => r.well + r.wrong > 0 && r.well + r.wrong <= length).ToList();

            if (validRules.Count == 0) break;

            var rule = validRules[rnd.Next(validRules.Count)];
            var hint = CreateHintByRule(secret, invalid, length, rule.well, rule.wrong, rnd);
            if (hint != null) TryAddHint(hints, signatures, hint);
        }

        // Single-Pin nur wenn erlaubt UND nur wenn genug invalid digits für unique rest (length-1 <= invalid.Count)
        if (style.AllowSinglePinFill && invalid.Count >= (length - 1))
        {
            for (int pos = 0; hints.Count < minHints && hints.Count < maxHints && pos < length; pos++)
            {
                var hint = CreateSinglePinHintUnique(secret, invalid, length, pos, rnd);
                if (hint != null) TryAddHint(hints, signatures, hint);
            }
        }
    }

    private List<LockHint>? ForceUniqueness(
        int[] secret,
        List<int> invalid,
        int length,
        List<LockHint> hints,
        HashSet<string> signatures,
        int maxHints,
        DifficultyStyle style,
        Random rnd)
    {
        string secretStr = string.Concat(secret);

        // Prüfen ob die bestehenden Hints bereits eindeutig sind (auch wenn maxHints schon erreicht)
        {
            var initSolver = new ConstraintSolver(length, hints);
            var initSols = initSolver.FindAllSolutions(maxSolutions: 2);
            if (initSols.Count == 1 && initSols[0] == secretStr)
                return hints;
        }

        // Max 80 Iterationen, dann aufgeben
        for (int step = 0; step < 80 && hints.Count < maxHints; step++)
        {
            var solver = new ConstraintSolver(length, hints);
            var sols = solver.FindAllSolutions(maxSolutions: 3);

            if (sols.Count == 1 && sols[0] == secretStr)
                return hints;

            if (sols.Count < 2)
                return null;

            string alt = sols[1];
            var altDigits = alt.Select(c => c - '0').ToArray();

            // 1) informative Kombi-Hints
            bool added = false;
            var preferred = style.PreferredDisambiguationRules
                .Where(r => r.well + r.wrong >= 2 && r.well + r.wrong <= length)
                .ToList();

            for (int t = 0; t < 120 && hints.Count < maxHints; t++)
            {
                if (preferred.Count == 0) break;

                var rule = preferred[rnd.Next(preferred.Count)];
                var candidate = CreateHintByRule(secret, invalid, length, rule.well, rule.wrong, rnd);
                if (candidate == null) continue;

                var sig = SignatureOf(candidate);
                if (signatures.Contains(sig)) continue;

                var (aw, ar) = LockHintScoring.Score(candidate.Slots, altDigits);
                if (aw != candidate.WellPlaced || ar != candidate.WrongPlaced)
                {
                    hints.Add(candidate);
                    signatures.Add(sig);
                    added = true;
                    break;
                }
            }

            if (added) continue;

            // 2) Single-Pin nur wenn erlaubt
            if (style.AllowSinglePinDisambiguation && invalid.Count >= (length - 1) && hints.Count < maxHints)
            {
                int diffPos = -1;
                for (int i = 0; i < length; i++)
                {
                    if (alt[i] != secretStr[i]) { diffPos = i; break; }
                }
                if (diffPos < 0) return null;

                var disHint = CreateSinglePinHintUnique(secret, invalid, length, diffPos, rnd);
                if (disHint != null) TryAddHint(hints, signatures, disHint);
                continue;
            }

            // WICHTIG: Wenn wir hier angekommen sind und step ist hoch, aufgeben
            // Aber die Schleife läuft sowieso nur bis 79, also einfach:
            if (step >= 79)
                return null;
        }

        // Wenn wir hier rausfallen (Schleife zu Ende), haben wir keine Lösung gefunden
        return null;
    }

    private LockRiddleGame? GenerateUltraSafe(int length, int targetHints, List<(int well, int wrong)> rules, Random rnd)
    {
        var (secret, invalid) = GenerateSecret(length, rnd);

        var hints = new List<LockHint>();
        var signatures = new HashSet<string>();

        // (0,0)-Hinweis bevorzugt unique, bei Bedarf mit Wiederholungen
        if (invalid.Count >= length)
        {
            var nothing = CreateHintNothingCorrect(invalid, length, rnd);
            if (nothing != null) TryAddHint(hints, signatures, nothing);
        }

        // Kombi-Hints hinzufügen bis eindeutig
        int attempts = 0;
        while (hints.Count < Math.Min(targetHints, 12) && attempts++ < 400)
        {
            var valid = rules.Where(r => r.well + r.wrong >= 2 && r.well + r.wrong <= length).ToList();
            if (valid.Count == 0) valid = rules.Where(r => r.well + r.wrong > 0 && r.well + r.wrong <= length).ToList();
            if (valid.Count == 0) break;

            var rule = valid[rnd.Next(valid.Count)];
            var h = CreateHintByRule(secret, invalid, length, rule.well, rule.wrong, rnd);
            if (h != null) TryAddHint(hints, signatures, h);

            var solver = new ConstraintSolver(length, hints);
            var sols = solver.FindAllSolutions(maxSolutions: 2);
            if (sols.Count == 1 && sols[0] == string.Concat(secret))
                break;
        }

        // Worst case: sichere Hints je Position über Regeln (2,0) + (0,2) etc. (kein SinglePin bei length=6)
        var finalSolver = new ConstraintSolver(length, hints);
        var finalSols = finalSolver.FindAllSolutions(maxSolutions: 2);
        if (!(finalSols.Count == 1 && finalSols[0] == string.Concat(secret)))
        {
            hints.Clear();
            signatures.Clear();

            // Für length<=5 dürfen wir ggf. single-pin unique nutzen, wenn möglich
            if (invalid.Count >= (length - 1))
            {
                for (int pos = 0; pos < length; pos++)
                {
                    var sp = CreateSinglePinHintUnique(secret, invalid, length, pos, rnd);
                    if (sp != null) TryAddHint(hints, signatures, sp);
                }
            }
            else
            {
                // length=6: sichere Kombi-Hints (2,2) / (2,1) etc.
                var fallbackRules = new List<(int well, int wrong)> { (2, 2), (2, 1), (1, 2), (3, 0), (0, 3), (1, 1), (2, 0), (0, 2) };
                fallbackRules = fallbackRules.Where(r => r.well + r.wrong <= length && r.well + r.wrong >= 2).ToList();

                int guard = 0;
                while (hints.Count < Math.Min(targetHints, 12) && guard++ < 400)
                {
                    var r = fallbackRules[rnd.Next(fallbackRules.Count)];
                    var h = CreateHintByRule(secret, invalid, length, r.well, r.wrong, rnd);
                    if (h != null) TryAddHint(hints, signatures, h);

                    var solver = new ConstraintSolver(length, hints);
                    var sols = solver.FindAllSolutions(maxSolutions: 2);
                    if (sols.Count == 1 && sols[0] == string.Concat(secret))
                        break;
                }
            }
        }
        if (hints.Count == 0)
        {
            for (int pos = 0; pos < length && invalid.Count >= (length - 1); pos++)
            {
                var sp = CreateSinglePinHintUnique(secret, invalid, length, pos, rnd);
                if (sp != null)
                {
                    var (well, wrong) = LockHintScoring.Score(sp.Slots, secret);
                    if (well == sp.WellPlaced && wrong == sp.WrongPlaced)
                        hints.Add(sp);
                }
            }
        }

        if (hints.Count == 0)
            return null;
        return new LockRiddleGame
        {
            SecretCode = string.Concat(secret),
            Hints = hints.OrderBy(_ => rnd.Next()).ToList(),
            Difficulty = length
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // BUILD HINTS WITH COVERAGE (keine leeren Slots)
    // ═══════════════════════════════════════════════════════════════

    private List<LockHint>? BuildHintsWithCoverageGuarantee(
        int[] secret,
        List<int> invalid,
        int length,
        int minCoveredDigits,
        int targetHints,
        List<(int well, int wrong)> rules,
        Random rnd)
    {
        var hints = new List<LockHint>();
        var signatures = new HashSet<string>();
        var coveredDigits = new HashSet<int>();

        // Phase 1: Coverage
        for (int pos = 0; pos < length; pos++)
        {
            if (coveredDigits.Count >= minCoveredDigits) break;
            if (coveredDigits.Contains(secret[pos])) continue;

            var hint = CreateHintForPositionUnique(secret, invalid, length, pos, coveredDigits, rnd);
            if (hint != null && TryAddHint(hints, signatures, hint))
                UpdateCoverage(hint, coveredDigits, secret);
        }

        if (coveredDigits.Count < minCoveredDigits)
        {
            foreach (int digit in secret.Where(d => !coveredDigits.Contains(d)))
            {
                if (coveredDigits.Count >= minCoveredDigits) break;

                var hint = CreateHintShowingDigitUnique(secret, invalid, length, digit, rnd);
                if (hint != null && TryAddHint(hints, signatures, hint))
                    UpdateCoverage(hint, coveredDigits, secret);
            }
        }

        if (coveredDigits.Count < minCoveredDigits)
            return null;

        // Phase 2: (0,0)-Hinweis erzwingen, bei Master (length=6) auch mit Wiederholungen.
        if (hints.Count < targetHints || length == 6)
        {
            var nothingHint = CreateHintNothingCorrect(invalid, length, rnd);
            if (nothingHint != null)
                TryAddHint(hints, signatures, nothingHint);
        }

        // Phase 3: Regeln
        var shuffledRules = rules
            .Where(r => r.well + r.wrong > 0 && r.well + r.wrong <= length)
            .OrderBy(_ => rnd.Next())
            .ToList();

        foreach (var (well, wrong) in shuffledRules)
        {
            if (hints.Count >= targetHints) break;

            var hint = CreateHintByRule(secret, invalid, length, well, wrong, rnd);
            if (hint != null)
                TryAddHint(hints, signatures, hint);
        }

        // Phase 4: Auffüllen
        int fillAttempts = 0;
        while (hints.Count < targetHints && fillAttempts++ < 120)
        {
            var validRules = rules.Where(r => r.well + r.wrong > 0 && r.well + r.wrong <= length).ToList();
            if (validRules.Count == 0) break;

            var rule = validRules[rnd.Next(validRules.Count)];
            var hint = CreateHintByRule(secret, invalid, length, rule.well, rule.wrong, rnd);
            if (hint != null)
                TryAddHint(hints, signatures, hint);
        }

        if (!SatisfiesNothingCorrectCoverageRule(hints))
            return null;

        return hints;
    }

    // ═══════════════════════════════════════════════════════════════
    // HINT CREATORS (immer voll gefüllt)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Hint enthält garantiert Secret-Ziffer an targetPos. Bei length=6 wird zusätzlich garantiert,
    /// dass mindestens 2 Secret-Ziffern im Hint vorkommen (sonst bräuchte man 5 invalids -> unmöglich ohne Wiederholung).
    /// </summary>
    private LockHint? CreateHintForPositionUnique(int[] secret, List<int> invalid, int length, int targetPos, HashSet<int> alreadyCovered, Random rnd)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            var slots = new string[length];
            var usedDigits = new HashSet<int>();

            // 1) Target secret digit setzen (richtig oder falsch platziert)
            bool placeCorrectly = rnd.Next(3) > 0;

            if (placeCorrectly)
            {
                slots[targetPos] = secret[targetPos].ToString();
            }
            else
            {
                var wrongPositions = Enumerable.Range(0, length).Where(p => p != targetPos).ToList();
                int wrongPos = wrongPositions[rnd.Next(wrongPositions.Count)];
                slots[wrongPos] = secret[targetPos].ToString();
            }
            usedDigits.Add(secret[targetPos]);

            // 2) Für length=6: mindestens 2 Secret digits im Hint
            if (length == 6)
            {
                // wähle eine andere Secret-Ziffer (idealerweise noch nicht covered)
                var candidates = Enumerable.Range(0, length)
                    .Where(i => i != targetPos)
                    .Select(i => (idx: i, digit: secret[i]))
                    .Where(x => !usedDigits.Contains(x.digit))
                    .OrderByDescending(x => alreadyCovered.Contains(x.digit) ? 0 : 1)
                    .ToList();

                if (candidates.Count > 0)
                {
                    var pick = candidates[rnd.Next(candidates.Count)];
                    // platziere bevorzugt falsch (damit es nicht zu trivial wird)
                    var freePos = Enumerable.Range(0, length)
                        .Where(p => string.IsNullOrEmpty(slots[p]) && p != pick.idx)
                        .ToList();

                    if (freePos.Count > 0)
                    {
                        int p = freePos[rnd.Next(freePos.Count)];
                        slots[p] = pick.digit.ToString();
                        usedDigits.Add(pick.digit);
                    }
                    else
                    {
                        // notfalls korrekt, wenn keine falsche Position frei
                        if (string.IsNullOrEmpty(slots[pick.idx]))
                        {
                            slots[pick.idx] = pick.digit.ToString();
                            usedDigits.Add(pick.digit);
                        }
                    }
                }
            }

            // 3) Rest mit UNIQUE invalid digits füllen
            for (int pos = 0; pos < length; pos++)
            {
                if (!string.IsNullOrEmpty(slots[pos])) continue;

                if (!TryPickInvalidUnique(invalid, usedDigits, rnd, out int inv))
                    goto retry;

                slots[pos] = inv.ToString();
                usedDigits.Add(inv);
            }

            // 4) final: keine Wiederholung garantiert
            if (slots.Select(int.Parse).Distinct().Count() != length) goto retry;

            return BuildHint(slots, secret);

        retry:
            continue;
        }

        return null;
    }

    private LockHint? CreateHintShowingDigitUnique(int[] secret, List<int> invalid, int length, int digitToShow, Random rnd)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            int secretPos = Array.IndexOf(secret, digitToShow);

            var slots = new string[length];
            var usedDigits = new HashSet<int>();

            bool placeCorrectly = rnd.Next(2) == 0;

            if (placeCorrectly && secretPos >= 0)
            {
                slots[secretPos] = digitToShow.ToString();
            }
            else
            {
                var wrongPositions = Enumerable.Range(0, length).Where(p => p != secretPos).ToList();
                int wrongPos = wrongPositions[rnd.Next(wrongPositions.Count)];
                slots[wrongPos] = digitToShow.ToString();
            }
            usedDigits.Add(digitToShow);

            // length=6: mindestens 2 Secret digits zeigen
            if (length == 6)
            {
                var candidates = secret.Where(d => d != digitToShow && !usedDigits.Contains(d)).ToList();
                if (candidates.Count > 0)
                {
                    int d2 = candidates[rnd.Next(candidates.Count)];
                    int idx2 = Array.IndexOf(secret, d2);

                    var freePos = Enumerable.Range(0, length)
                        .Where(p => string.IsNullOrEmpty(slots[p]) && p != idx2)
                        .ToList();

                    if (freePos.Count > 0)
                    {
                        int p = freePos[rnd.Next(freePos.Count)];
                        slots[p] = d2.ToString();
                        usedDigits.Add(d2);
                    }
                }
            }

            for (int pos = 0; pos < length; pos++)
            {
                if (!string.IsNullOrEmpty(slots[pos])) continue;

                if (!TryPickInvalidUnique(invalid, usedDigits, rnd, out int inv))
                    goto retry;

                slots[pos] = inv.ToString();
                usedDigits.Add(inv);
            }

            if (slots.Select(int.Parse).Distinct().Count() != length) goto retry;
            return BuildHint(slots, secret);

        retry:
            continue;
        }

        return null;
    }

    /// <summary>
    /// (0,0)-Hinweis: bevorzugt ohne Wiederholungen, fällt bei length=6 auf Wiederholungen zurück.
    /// </summary>
    private LockHint? CreateHintNothingCorrect(List<int> invalid, int length, Random rnd)
    {
        if (invalid.Count == 0) return null;

        var slots = new string[length];

        if (invalid.Count >= length)
        {
            var usedDigits = new HashSet<int>();
            for (int pos = 0; pos < length; pos++)
            {
                if (!TryPickInvalidUnique(invalid, usedDigits, rnd, out int inv))
                    return null;

                slots[pos] = inv.ToString();
                usedDigits.Add(inv);
            }
        }
        else
        {
            for (int pos = 0; pos < length; pos++)
                slots[pos] = invalid[rnd.Next(invalid.Count)].ToString();
        }

        return new LockHint
        {
            Slots = slots.ToList(),
            Code = string.Join(" ", slots),
            WellPlaced = 0,
            WrongPlaced = 0,
            Description = LocalizationService.GetString("LockRiddle_NoDigitCorrect"),
            Icon = "❌"
        };
    }

    /// <summary>
    /// Single-Pin (1,0) geht ohne Wiederholungen nur, wenn invalid.Count >= length-1.
    /// (für length=6 ist das NICHT der Fall -> Methode gibt null zurück)
    /// </summary>
    private LockHint? CreateSinglePinHintUnique(int[] secret, List<int> invalid, int length, int pos, Random rnd)
    {
        if (invalid.Count < (length - 1)) return null;

        var slots = new string[length];
        var used = new HashSet<int>();

        slots[pos] = secret[pos].ToString();
        used.Add(secret[pos]);

        for (int i = 0; i < length; i++)
        {
            if (i == pos) continue;

            if (!TryPickInvalidUnique(invalid, used, rnd, out int inv))
                return null;

            slots[i] = inv.ToString();
            used.Add(inv);
        }

        if (slots.Select(int.Parse).Distinct().Count() != length) return null;
        return BuildHint(slots, secret);
    }

    private LockHint? CreateHintByRule(int[] secret, List<int> invalid, int length, int well, int wrong, Random rnd)
    {
        if (well < 0 || wrong < 0 || well + wrong > length || well + wrong == 0)
            return null;

        // Für length=6: (well+wrong) muss >=2 sein, sonst bräuchten wir 5 invalid unique.
        if (length == 6 && (well + wrong) < 2)
            return null;

        for (int attempt = 0; attempt < 120; attempt++)
        {
            var slots = new string[length];
            var usedDigits = new HashSet<int>();
            var usedSecretIndices = new HashSet<int>();

            var allPositions = Enumerable.Range(0, length).OrderBy(_ => rnd.Next()).ToList();
            var wellPositions = allPositions.Take(well).ToList();
            var remainingPositions = allPositions.Skip(well).ToList();

            foreach (int pos in wellPositions)
            {
                slots[pos] = secret[pos].ToString();
                usedDigits.Add(secret[pos]);
                usedSecretIndices.Add(pos);
            }

            bool failed = false;

            // Wrong-placed digits
            for (int i = 0; i < wrong && !failed; i++)
            {
                var availableSecretIndices = Enumerable.Range(0, length)
                    .Where(idx => !usedSecretIndices.Contains(idx) && !usedDigits.Contains(secret[idx]))
                    .ToList();

                if (availableSecretIndices.Count == 0) { failed = true; break; }

                int secretIdx = availableSecretIndices[rnd.Next(availableSecretIndices.Count)];
                int digitToPlace = secret[secretIdx];

                var validTargetPositions = remainingPositions
                    .Where(p => string.IsNullOrEmpty(slots[p]) && p != secretIdx)
                    .ToList();

                if (validTargetPositions.Count == 0) { failed = true; break; }

                int targetPos = validTargetPositions[rnd.Next(validTargetPositions.Count)];
                slots[targetPos] = digitToPlace.ToString();
                usedDigits.Add(digitToPlace);
                usedSecretIndices.Add(secretIdx);
                remainingPositions.Remove(targetPos);
            }

            if (failed) continue;

            // Fill rest with UNIQUE invalid digits only
            for (int pos = 0; pos < length; pos++)
            {
                if (!string.IsNullOrEmpty(slots[pos])) continue;

                if (!TryPickInvalidUnique(invalid, usedDigits, rnd, out int inv))
                {
                    failed = true;
                    break;
                }

                slots[pos] = inv.ToString();
                usedDigits.Add(inv);
            }

            if (failed) continue;

            // final unique check
            if (slots.Select(int.Parse).Distinct().Count() != length) continue;

            return BuildHint(slots, secret);
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private (int[] secret, List<int> invalid) GenerateSecret(int length, Random rnd)
    {
        var pool = Enumerable.Range(0, 10).OrderBy(_ => rnd.Next()).ToList();
        int[] secret = pool.Take(length).ToArray();
        List<int> invalid = pool.Skip(length).ToList();
        return (secret, invalid);
    }

    private static bool TryPickInvalidUnique(List<int> invalid, HashSet<int> usedDigits, Random rnd, out int digit)
    {
        var candidates = invalid.Where(d => !usedDigits.Contains(d)).ToList();
        if (candidates.Count == 0)
        {
            digit = -1;
            return false;
        }
        digit = candidates[rnd.Next(candidates.Count)];
        return true;
    }

    private static bool SatisfiesNothingCorrectCoverageRule(IReadOnlyList<LockHint> hints)
    {
        var allHints = hints.ToList();

        foreach (var zeroHint in allHints.Where(h => h.WellPlaced == 0 && h.WrongPlaced == 0))
        {
            var zeroDigits = zeroHint.Slots
                .Select(slot => int.TryParse(slot, out var digit) ? digit : (int?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .Distinct()
                .ToHashSet();

            if (zeroDigits.Count == 0)
                return false;

            // Qualitätsregel für Lesbarkeit:
            // In JEDEM anderen Hinweis soll mindestens eine Ziffer aus dem (0,0)-Hinweis vorkommen,
            // damit die "falschen" Ziffern klar über das gesamte Hint-Set hinweg wiedererkennbar sind.
            foreach (var otherHint in allHints.Where(h => h != zeroHint))
            {
                bool hasSharedDigit = otherHint.Slots
                    .Select(slot => int.TryParse(slot, out var digit) ? digit : (int?)null)
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .Any(zeroDigits.Contains);

                if (!hasSharedDigit)
                    return false;
            }
        }

        return true;
    }

    private bool TryAddHint(List<LockHint> hints, HashSet<string> signatures, LockHint? hint)
    {
        if (hint == null) return false;

        // Safety: keine leeren Slots
        if (hint.Slots.Count == 0 || hint.Slots.Any(s => string.IsNullOrWhiteSpace(s)))
            return false;

        string sig = SignatureOf(hint);
        if (signatures.Add(sig))
        {
            hints.Add(hint);
            return true;
        }
        return false;
    }

    private void UpdateCoverage(LockHint hint, HashSet<int> coveredDigits, int[] secret)
    {
        var secretSet = secret.ToHashSet();
        for (int i = 0; i < hint.Slots.Count && i < secret.Length; i++)
        {
            if (!int.TryParse(hint.Slots[i], out int d)) continue;
            if (secretSet.Contains(d))
                coveredDigits.Add(d);
        }
    }

    private LockHint BuildHint(string[] slots, int[] secret)
    {
        // slots sind hier immer voll
        var normalizedSlots = slots.Select(s => s.Trim()).ToList();
        var (well, wrong) = LockHintScoring.Score(normalizedSlots, secret);

        string Plural(int n, string s1, string s2) => n == 1 ? s1 : s2;

        string desc = (well, wrong) switch
        {
            (0, 0) => LocalizationService.GetString("LockRiddle_NoDigitCorrect"),
            (1, 0) => LocalizationService.GetString("LockRiddle_OneCorrectWellPlaced"),
            (0, 1) => LocalizationService.GetString("LockRiddle_OneCorrectWrongPlaced"),
            ( > 0, 0) => LocalizationService.Format("LockRiddle_HintWellPlacedFormat", well, Plural(well, LocalizationService.GetString("LockRiddle_NumberSingular"), LocalizationService.GetString("LockRiddle_NumberPlural"))),
            (0, > 0) => LocalizationService.Format("LockRiddle_HintWrongPlacedFormat", wrong, Plural(wrong, LocalizationService.GetString("LockRiddle_NumberSingular"), LocalizationService.GetString("LockRiddle_NumberPlural"))),
            _ => LocalizationService.Format("LockRiddle_HintMixedFormat", well + wrong, Plural(well + wrong, LocalizationService.GetString("LockRiddle_NumberSingular"), LocalizationService.GetString("LockRiddle_NumberPlural")), well, wrong)
        };

        // Add visual indicators
        string visualPrefix = "";
        for (int i = 0; i < well; i++) visualPrefix += "✔️";
        for (int i = 0; i < wrong; i++) visualPrefix += "🟡";
        
        if (!string.IsNullOrEmpty(visualPrefix))
        {
            desc = $"{visualPrefix} {desc}";
        }

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
            Code = string.Join(" ", normalizedSlots),
            WellPlaced = well,
            WrongPlaced = wrong,
            Description = desc,
            Icon = icon
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
