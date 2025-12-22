using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

public sealed class MathCrossGeneratorService
{
    private const int BigSize = 24;

    private sealed record Settings(
        string DifficultyKey,
        int MinVal,
        int MaxVal,
        bool AllowNegative,
        string[] Ops,
        int MinEquations,
        int MaxEquations,
        double GivenPercent,
        int MaxAbsResult,
        int MaxSpan,
        int EquationLength // 5 (Easy/Normal), 7 (Hard), 9 (Master)
    );

    private readonly struct Delta
    {
        public readonly int Dr;
        public readonly int Dc;
        public Delta(int dr, int dc) { Dr = dr; Dc = dc; }
    }

    private static readonly Delta[] Dirs = { new(0, 1), new(1, 0) };

    public MathCrossGame GenerateGame(string difficultyKey, int seed)
    {
        var s = GetSettings(difficultyKey);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5.0);

        MathCrossGame? best = null;
        int bestEquationCount = -1;

        for (int attempt = 0; attempt < 120 && DateTime.UtcNow < deadline; attempt++)
        {
            try
            {
                var rnd = new Random(seed + attempt * 997);

                // ✅ in ALLEN Schwierigkeiten: nur 7 oder 8 Gleichungen
                int targetEquations = rnd.Next(s.MinEquations, s.MaxEquations + 1);

                var big = CreateEmptyGrid(BigSize, BigSize);
                int placedCount = 0;

                if (!TryPlaceFirstEquation(big, rnd, s, ref placedCount))
                    continue;

                if (!TryGrowEquations(big, rnd, s, ref placedCount, targetEquations))
                    continue;

                var cropped = Crop(big, s);
                if (cropped.Rows == 0 || cropped.Cols == 0) continue;
                if (!IsSingleComponent(cropped)) continue;
                if (cropped.Equations.Count < s.MinEquations) continue;

                var finalized = FinalizeGame(cropped, rnd, s);

                if (finalized.Equations.Count >= targetEquations)
                    return finalized;

                if (finalized.Equations.Count > bestEquationCount)
                {
                    bestEquationCount = finalized.Equations.Count;
                    best = finalized;
                }
            }
            catch
            {
                // ignore
            }
        }

        if (best != null)
            return best;

        var fallback = GenerateStripFallback(s, seed);
        return FinalizeGame(fallback, new Random(seed + 4242), s);
    }

    // ===== RESET / FINALIZE =====

    private static void ResetAllCells(MathCrossGame game)
    {
        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                var cell = game.Grid[r, c];
                if (cell.Type == CellType.Empty) continue;

                if (cell.Type == CellType.Equals)
                {
                    cell.IsGiven = true;
                    cell.UserInput = "=";
                }
                else
                {
                    cell.IsGiven = false;
                    cell.UserInput = "";
                }
            }
        }
    }

    private static void InitialMasking(MathCrossGame game, Random rnd, double givenPercent)
    {
        var editableCells = new List<MathCrossCell>();

        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                var cell = game.Grid[r, c];
                if (cell.Type is CellType.Number or CellType.Operator)
                    editableCells.Add(cell);
            }
        }

        var shuffled = editableCells.OrderBy(_ => rnd.Next()).ToList();
        int toGive = (int)(shuffled.Count * givenPercent);

        for (int i = 0; i < toGive && i < shuffled.Count; i++)
        {
            shuffled[i].IsGiven = true;
            shuffled[i].UserInput = shuffled[i].Solution;
        }
    }

    private static void EnsureSolvable(MathCrossGame game, Random rnd, Settings s)
    {
        int maxIterations = 100;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var solvableMask = new bool[game.Rows, game.Cols];

            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                {
                    var cell = game.Grid[r, c];
                    if (cell.Type == CellType.Empty) continue;
                    if (cell.IsGiven || cell.Type == CellType.Equals)
                        solvableMask[r, c] = true;
                }

            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var eq in game.Equations)
                {
                    if (TrySolveEquationStep(game, eq, solvableMask))
                        progress = true;
                }
            }

            int totalNonEmpty = 0;
            int solvedCount = 0;
            var unsolved = new List<MathCrossCell>();

            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                {
                    if (game.Grid[r, c].Type == CellType.Empty) continue;
                    totalNonEmpty++;
                    if (solvableMask[r, c]) solvedCount++;
                    else unsolved.Add(game.Grid[r, c]);
                }

            if (solvedCount == totalNonEmpty)
            {
                game.GivenCells = 0;
                for (int r = 0; r < game.Rows; r++)
                    for (int c = 0; c < game.Cols; c++)
                        if (game.Grid[r, c].IsGiven) game.GivenCells++;
                return;
            }

            if (unsolved.Count > 0)
            {
                var pick = unsolved[rnd.Next(unsolved.Count)];
                pick.IsGiven = true;
                pick.UserInput = pick.Solution;
            }
            else break;
        }
    }

    private static MathCrossGame FinalizeGame(MathCrossGame raw, Random rnd, Settings s)
    {
        ResetAllCells(raw);

        InitialMasking(raw, rnd, s.GivenPercent);

        EnsureSolvable(raw, rnd, s);

        raw.Difficulty = s.DifficultyKey;
        raw.EquationLength = s.EquationLength;
        raw.UseExtendedEquations = s.EquationLength != 5; // Hard+Master = true
        return raw;
    }

    private static bool TrySolveEquationStep(MathCrossGame game, MathEquation eq, bool[,] mask)
    {
        // simple: wenn genau 1 unbekannt -> markiere alle als lösbar (wie vorher)
        int unknowns = 0;
        foreach (var (r, c) in eq.Cells)
        {
            var cell = game.Grid[r, c];
            if (cell.Type == CellType.Equals) continue;
            if (!mask[r, c]) unknowns++;
        }

        if (unknowns != 1) return false;

        foreach (var (r, c) in eq.Cells)
            mask[r, c] = true;

        return true;
    }

    // ===== COMMIT EQUATIONS (5 / 7 / 9) =====

    private static bool TryCommitEquation5(MathCrossCell[,] grid, int sr, int sc, Delta d,
        int a, string op, int b, int c)
    {
        var tokens = new (CellType type, string sol)[]
        {
            (CellType.Number, a.ToString()),
            (CellType.Operator, op),
            (CellType.Number, b.ToString()),
            (CellType.Equals, "="),
            (CellType.Number, c.ToString())
        };

        return TryCommitTokens(grid, sr, sc, d, tokens);
    }

    private static bool TryCommitEquation7(MathCrossCell[,] grid, int sr, int sc, Delta d,
        int a, string op1, int b, string op2, int c, int result)
    {
        var tokens = new (CellType type, string sol)[]
        {
            (CellType.Number, a.ToString()),
            (CellType.Operator, op1),
            (CellType.Number, b.ToString()),
            (CellType.Operator, op2),
            (CellType.Number, c.ToString()),
            (CellType.Equals, "="),
            (CellType.Number, result.ToString())
        };

        return TryCommitTokens(grid, sr, sc, d, tokens);
    }

    private static bool TryCommitEquation9(MathCrossCell[,] grid, int sr, int sc, Delta d,
        int a, string op1, int b, string op2, int c, string op3, int dd, int result)
    {
        var tokens = new (CellType type, string sol)[]
        {
            (CellType.Number, a.ToString()),
            (CellType.Operator, op1),
            (CellType.Number, b.ToString()),
            (CellType.Operator, op2),
            (CellType.Number, c.ToString()),
            (CellType.Operator, op3),
            (CellType.Number, dd.ToString()),
            (CellType.Equals, "="),
            (CellType.Number, result.ToString())
        };

        return TryCommitTokens(grid, sr, sc, d, tokens);
    }

    private static bool TryCommitTokens(MathCrossCell[,] grid, int sr, int sc, Delta d,
        (CellType type, string sol)[] tokens)
    {
        int len = tokens.Length;

        // Boundary check: cells before and after must be empty
        int br = sr - d.Dr, bc = sc - d.Dc;
        int ar = sr + len * d.Dr, ac = sc + len * d.Dc;

        if (InBounds(grid, br, bc) && grid[br, bc].Type != CellType.Empty) return false;
        if (InBounds(grid, ar, ac) && grid[ar, ac].Type != CellType.Empty) return false;

        int pDr = d.Dr == 0 ? 1 : 0;
        int pDc = d.Dc == 0 ? 1 : 0;

        for (int i = 0; i < len; i++)
        {
            int r = sr + i * d.Dr;
            int c = sc + i * d.Dc;

            if (!InBounds(grid, r, c)) return false;

            var existing = grid[r, c];
            var (targetType, targetSol) = tokens[i];

            if (existing.Type != CellType.Empty)
            {
                if (existing.Type != targetType) return false;
                if (existing.Solution != targetSol) return false;
            }
            else
            {
                // No side contact for new cells
                if (InBounds(grid, r + pDr, c + pDc) && grid[r + pDr, c + pDc].Type != CellType.Empty) return false;
                if (InBounds(grid, r - pDr, c - pDc) && grid[r - pDr, c - pDc].Type != CellType.Empty) return false;
            }
        }

        // Write cells
        for (int i = 0; i < len; i++)
        {
            int r = sr + i * d.Dr;
            int c = sc + i * d.Dc;

            var (targetType, targetSol) = tokens[i];

            grid[r, c].Type = targetType;
            grid[r, c].Solution = targetSol;
            grid[r, c].IsGiven = false;
            grid[r, c].UserInput = "";
        }

        return true;
    }

    // ===== PLACE / GROW =====

    private static bool TryPlaceFirstEquation(MathCrossCell[,] grid, Random rnd, Settings s, ref int placedCount)
    {
        int mid = grid.GetLength(0) / 2;

        if (s.EquationLength == 5)
        {
            if (!TryGenerateEquation5(s, rnd, out var a, out var op, out var b, out var c)) return false;
            if (TryCommitEquation5(grid, mid, mid - 2, new Delta(0, 1), a, op, b, c))
            {
                placedCount++;
                return true;
            }
        }
        else if (s.EquationLength == 7)
        {
            if (!TryGenerateEquation7(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var d)) return false;
            if (TryCommitEquation7(grid, mid, mid - 3, new Delta(0, 1), a, op1, b, op2, cc, d))
            {
                placedCount++;
                return true;
            }
        }
        else // 9
        {
            if (!TryGenerateEquation9(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var op3, out var dd, out var e)) return false;
            if (TryCommitEquation9(grid, mid, mid - 4, new Delta(0, 1), a, op1, b, op2, cc, op3, dd, e))
            {
                placedCount++;
                return true;
            }
        }

        return false;
    }

    private static bool TryGrowEquations(MathCrossCell[,] grid, Random rnd, Settings s, ref int placedCount, int target)
    {
        int fails = 0;

        while (placedCount < target && fails < 25)
        {
            var anchors = GetNumberCells(grid);
            if (anchors.Count == 0) return false;

            var bbox = GetBBox(grid);
            if (Span(bbox) > s.MaxSpan) return placedCount >= s.MinEquations;

            bool success = false;

            for (int t = 0; t < 90; t++)
            {
                var (ar, ac, aval) = anchors[rnd.Next(anchors.Count)];
                var dir = Dirs[rnd.Next(Dirs.Length)];

                if (s.EquationLength == 5)
                {
                    int anchorIdx = new[] { 0, 2, 4 }[rnd.Next(3)];
                    int sr = ar - anchorIdx * dir.Dr;
                    int sc = ac - anchorIdx * dir.Dc;

                    if (!InBounds(grid, sr, sc) || !InBounds(grid, sr + 4 * dir.Dr, sc + 4 * dir.Dc)) continue;

                    if (TryGenerateEquation5WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op, out var b, out var c))
                    {
                        if (TryCommitEquation5(grid, sr, sc, dir, a, op, b, c))
                        {
                            placedCount++;
                            success = true;
                            break;
                        }
                    }
                }
                else if (s.EquationLength == 7)
                {
                    int anchorIdx = new[] { 0, 2, 4, 6 }[rnd.Next(4)];
                    int sr = ar - anchorIdx * dir.Dr;
                    int sc = ac - anchorIdx * dir.Dc;

                    if (!InBounds(grid, sr, sc) || !InBounds(grid, sr + 6 * dir.Dr, sc + 6 * dir.Dc)) continue;

                    if (TryGenerateEquation7WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op1, out var b, out var op2, out var cc, out var d))
                    {
                        if (TryCommitEquation7(grid, sr, sc, dir, a, op1, b, op2, cc, d))
                        {
                            placedCount++;
                            success = true;
                            break;
                        }
                    }
                }
                else // 9
                {
                    int anchorIdx = new[] { 0, 2, 4, 6, 8 }[rnd.Next(5)];
                    int sr = ar - anchorIdx * dir.Dr;
                    int sc = ac - anchorIdx * dir.Dc;

                    if (!InBounds(grid, sr, sc) || !InBounds(grid, sr + 8 * dir.Dr, sc + 8 * dir.Dc)) continue;

                    if (TryGenerateEquation9WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op1, out var b, out var op2, out var cc, out var op3, out var dd, out var e))
                    {
                        if (TryCommitEquation9(grid, sr, sc, dir, a, op1, b, op2, cc, op3, dd, e))
                        {
                            placedCount++;
                            success = true;
                            break;
                        }
                    }
                }
            }

            fails = success ? 0 : fails + 1;
        }

        return true;
    }

    /// <summary>
    /// Schneller Fallback: horizontale Streifen + optional vertikale Crosses
    /// </summary>
    private MathCrossGame GenerateStripFallback(Settings s, int seed)
    {
        var rnd = new Random(seed + 1337);
        int eqLen = s.EquationLength;

        int rows = Math.Max(s.MaxEquations * 2 + 4, eqLen + 4);
        int cols = eqLen + 6;

        var grid = CreateEmptyGrid(rows, cols);
        int placed = 0;
        int startCol = 2;

        for (int r = 1; r + eqLen < rows - 1 && placed < s.MaxEquations; r += 2)
        {
            if (eqLen == 5)
            {
                if (TryGenerateEquation5(s, rnd, out var a, out var op, out var b, out var c) &&
                    TryCommitEquation5(grid, r, startCol, new Delta(0, 1), a, op, b, c))
                    placed++;
            }
            else if (eqLen == 7)
            {
                if (TryGenerateEquation7(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var d) &&
                    TryCommitEquation7(grid, r, startCol - 1, new Delta(0, 1), a, op1, b, op2, cc, d))
                    placed++;
            }
            else
            {
                if (TryGenerateEquation9(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var op3, out var dd, out var e) &&
                    TryCommitEquation9(grid, r, startCol - 2, new Delta(0, 1), a, op1, b, op2, cc, op3, dd, e))
                    placed++;
            }
        }

        // vertikale Crosses
        int verticalAttempts = 0;
        while (placed < s.MaxEquations && verticalAttempts++ < 60)
        {
            var anchors = GetNumberCells(grid);
            if (anchors.Count == 0) break;

            var (ar, ac, aval) = anchors[rnd.Next(anchors.Count)];
            int anchorIdx = eqLen switch
            {
                5 => new[] { 0, 2, 4 }[rnd.Next(3)],
                7 => new[] { 0, 2, 4, 6 }[rnd.Next(4)],
                _ => new[] { 0, 2, 4, 6, 8 }[rnd.Next(5)]
            };

            int sr = ar - anchorIdx;
            int sc = ac;
            if (!InBounds(grid, sr, sc) || !InBounds(grid, sr + (eqLen - 1), sc)) continue;

            if (eqLen == 5)
            {
                if (TryGenerateEquation5WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op, out var b, out var c) &&
                    TryCommitEquation5(grid, sr, sc, new Delta(1, 0), a, op, b, c))
                {
                    placed++;
                }

            }
            else if (eqLen == 7)
            {
                if (TryGenerateEquation7WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op1, out var b, out var op2, out var cc, out var d2) &&
                    TryCommitEquation7(grid, sr, sc, new Delta(1, 0), a, op1, b, op2, cc, d2))
                    placed++;
            }
            else
            {
                if (TryGenerateEquation9WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op1, out var b, out var op2, out var cc, out var op3, out var dd, out var e) &&
                    TryCommitEquation9(grid, sr, sc, new Delta(1, 0), a, op1, b, op2, cc, op3, dd, e))
                    placed++;
            }
        }

        return Crop(grid, s);
    }

    // ===== EQUATION GENERATION =====

    private static bool TryGenerateEquation5(Settings s, Random rnd, out int a, out string op, out int b, out int c)
    {
        op = s.Ops[rnd.Next(s.Ops.Length)];
        return TryGenerateEquation5WithAnchor(s, rnd, -1, 0, out a, out op, out b, out c);
    }

    private static bool TryGenerateEquation5WithAnchor(Settings s, Random rnd, int anchorIdx, int anchorVal,
        out int a, out string op, out int b, out int c)
    {
        a = 0; b = 0; c = 0;
        op = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 70; i++)
        {
            switch (op)
            {
                case "+":
                    if (anchorIdx == 0) { a = anchorVal; b = Rnd(rnd, s); c = a + b; }
                    else if (anchorIdx == 2) { b = anchorVal; a = Rnd(rnd, s); c = a + b; }
                    else if (anchorIdx == 4) { c = anchorVal; a = Rnd(rnd, s); b = c - a; }
                    else { a = Rnd(rnd, s); b = Rnd(rnd, s); c = a + b; }
                    break;

                case "-":
                    if (anchorIdx == 0) { a = anchorVal; b = Rnd(rnd, s); c = a - b; }
                    else if (anchorIdx == 2) { b = anchorVal; c = Rnd(rnd, s); a = c + b; }
                    else if (anchorIdx == 4) { c = anchorVal; b = Rnd(rnd, s); a = c + b; }
                    else { a = Rnd(rnd, s); b = Rnd(rnd, s); c = a - b; }
                    break;

                case "×":
                    if (anchorIdx == 0) { a = anchorVal; b = RndSmall(rnd, s); c = a * b; }
                    else if (anchorIdx == 2) { b = anchorVal; a = RndSmall(rnd, s); c = a * b; }
                    else if (anchorIdx == 4) { c = anchorVal; a = RndSmall(rnd, s); if (a == 0) a = 1; b = (c % a == 0) ? c / a : 0; }
                    else { a = RndSmall(rnd, s); b = RndSmall(rnd, s); c = a * b; }
                    break;

                case "÷":
                    if (anchorIdx == 2) { b = anchorVal; if (b == 0) continue; c = RndSmall(rnd, s); a = b * c; }
                    else if (anchorIdx == 4) { c = anchorVal; b = RndSmall(rnd, s); if (b == 0) b = 1; a = b * c; }
                    else if (anchorIdx == 0) { a = anchorVal; b = RndSmall(rnd, s); if (b == 0) b = 1; if (a % b != 0) continue; c = a / b; }
                    else { b = RndSmall(rnd, s); if (b == 0) b = 1; c = RndSmall(rnd, s); a = b * c; }
                    break;
            }

            if (!Valid(s, a) || !Valid(s, b) || !Valid(s, c)) continue;

            int result = op switch
            {
                "+" => a + b,
                "-" => a - b,
                "×" => a * b,
                "÷" => (b != 0 && a % b == 0) ? a / b : int.MinValue,
                _ => int.MinValue
            };

            if (result == c) return true;
        }

        return false;
    }

    // Hard: a op b op c = d  (7 Zellen)
    private static bool TryGenerateEquation7(Settings s, Random rnd,
        out int a, out string op1, out int b, out string op2, out int c, out int d)
        => TryGenerateEquation7WithAnchor(s, rnd, -1, 0, out a, out op1, out b, out op2, out c, out d);

    private static bool TryGenerateEquation7WithAnchor(Settings s, Random rnd, int anchorIdx, int anchorVal,
        out int a, out string op1, out int b, out string op2, out int c, out int d)
    {
        a = b = c = d = 0;
        op1 = s.Ops[rnd.Next(s.Ops.Length)];
        op2 = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 120; i++)
        {
            a = (anchorIdx == 0) ? anchorVal : Rnd(rnd, s);
            b = (anchorIdx == 2) ? anchorVal : Rnd(rnd, s);
            c = (anchorIdx == 4) ? anchorVal : Rnd(rnd, s);

            if (op1 is "×" or "÷") { a = RndSmall(rnd, s); b = RndSmall(rnd, s); }
            if (op2 is "×" or "÷") { b = RndSmall(rnd, s); c = RndSmall(rnd, s); }

            if (anchorIdx == 0) a = anchorVal;
            if (anchorIdx == 2) b = anchorVal;
            if (anchorIdx == 4) c = anchorVal;

            int? result = EvaluateExpression(new[] { a, b, c }, new[] { op1, op2 });
            if (result == null) continue;

            d = result.Value;

            if (anchorIdx == 6 && d != anchorVal) continue;

            if (!Valid(s, a) || !Valid(s, b) || !Valid(s, c) || !Valid(s, d)) continue;

            return true;
        }

        return false;
    }

    // Master: a op b op c op d = e  (9 Zellen) + negative erlaubt
    private static bool TryGenerateEquation9(Settings s, Random rnd,
        out int a, out string op1, out int b, out string op2, out int c, out string op3, out int d, out int e)
        => TryGenerateEquation9WithAnchor(s, rnd, -1, 0, out a, out op1, out b, out op2, out c, out op3, out d, out e);

    private static bool TryGenerateEquation9WithAnchor(Settings s, Random rnd, int anchorIdx, int anchorVal,
        out int a, out string op1, out int b, out string op2, out int c, out string op3, out int d, out int e)
    {
        a = b = c = d = e = 0;
        op1 = s.Ops[rnd.Next(s.Ops.Length)];
        op2 = s.Ops[rnd.Next(s.Ops.Length)];
        op3 = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 200; i++)
        {
            a = (anchorIdx == 0) ? anchorVal : Rnd(rnd, s);
            b = (anchorIdx == 2) ? anchorVal : Rnd(rnd, s);
            c = (anchorIdx == 4) ? anchorVal : Rnd(rnd, s);
            d = (anchorIdx == 6) ? anchorVal : Rnd(rnd, s);

            // kleinere Zahlen für ×/÷
            if (op1 is "×" or "÷") { a = RndSmall(rnd, s); b = RndSmall(rnd, s); }
            if (op2 is "×" or "÷") { b = RndSmall(rnd, s); c = RndSmall(rnd, s); }
            if (op3 is "×" or "÷") { c = RndSmall(rnd, s); d = RndSmall(rnd, s); }

            if (anchorIdx == 0) a = anchorVal;
            if (anchorIdx == 2) b = anchorVal;
            if (anchorIdx == 4) c = anchorVal;
            if (anchorIdx == 6) d = anchorVal;

            int? result = EvaluateExpression(new[] { a, b, c, d }, new[] { op1, op2, op3 });
            if (result == null) continue;

            e = result.Value;

            if (anchorIdx == 8 && e != anchorVal) continue;

            if (!Valid(s, a) || !Valid(s, b) || !Valid(s, c) || !Valid(s, d) || !Valid(s, e)) continue;

            return true;
        }

        return false;
    }

    // ✅ Vorrangregeln: ×/÷ vor +/-, alles ganzzahlig
    private static int? EvaluateExpression(int[] values, string[] ops)
    {
        // values.Length = ops.Length + 1
        var vals = values.ToList();
        var o = ops.ToList();

        // pass 1: × und ÷
        for (int i = 0; i < o.Count;)
        {
            if (o[i] is "×" or "÷")
            {
                int left = vals[i];
                int right = vals[i + 1];

                int? res = ApplyOp(left, o[i], right);
                if (res == null) return null;

                vals[i] = res.Value;
                vals.RemoveAt(i + 1);
                o.RemoveAt(i);
            }
            else i++;
        }

        // pass 2: + und -
        int acc = vals[0];
        for (int i = 0; i < o.Count; i++)
        {
            int? res = ApplyOp(acc, o[i], vals[i + 1]);
            if (res == null) return null;
            acc = res.Value;
        }

        return acc;
    }

    private static int? ApplyOp(int x, string op, int y) => op switch
    {
        "+" => x + y,
        "-" => x - y,
        "×" => x * y,
        "÷" => (y != 0 && x % y == 0) ? x / y : null,
        _ => null
    };

    // ===== CROP + SCAN =====

    private static MathCrossGame Crop(MathCrossCell[,] big, Settings s)
    {
        var (minR, maxR, minC, maxC) = GetBBox(big);
        if (maxR < 0) return new MathCrossGame();

        int rows = maxR - minR + 1;
        int cols = maxC - minC + 1;

        var game = new MathCrossGame
        {
            Rows = rows,
            Cols = cols,
            Grid = new MathCrossCell[rows, cols],
            EquationLength = s.EquationLength,
            UseExtendedEquations = s.EquationLength != 5
        };

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var src = big[minR + r, minC + c];
                game.Grid[r, c] = new MathCrossCell
                {
                    Row = r,
                    Col = c,
                    Type = src.Type,
                    Solution = src.Solution,
                    UserInput = "",
                    IsGiven = false
                };
            }

        game.Equations = ScanEquations(game, s.EquationLength);
        return game;
    }

    private static List<MathEquation> ScanEquations(MathCrossGame game, int eqLen)
    {
        var list = new List<MathEquation>();

        // Horizontal
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c <= game.Cols - eqLen; c++)
                if (IsEquation(game, r, c, 0, 1, eqLen, out var e))
                    list.Add(e);

        // Vertical
        for (int r = 0; r <= game.Rows - eqLen; r++)
            for (int c = 0; c < game.Cols; c++)
                if (IsEquation(game, r, c, 1, 0, eqLen, out var e))
                    list.Add(e);

        return list;
    }

    private static bool IsEquation(MathCrossGame g, int r, int c, int dr, int dc, int len, out MathEquation eq)
    {
        eq = new MathEquation();
        var cells = new List<(int row, int col)>();
        for (int i = 0; i < len; i++)
            cells.Add((r + i * dr, c + i * dc));

        bool IsType(int idx, CellType t) => g.Grid[cells[idx].row, cells[idx].col].Type == t;

        if (len == 5)
        {
            if (!IsType(0, CellType.Number)) return false;
            if (!IsType(1, CellType.Operator)) return false;
            if (!IsType(2, CellType.Number)) return false;
            if (!IsType(3, CellType.Equals)) return false;
            if (!IsType(4, CellType.Number)) return false;

            eq = new MathEquation
            {
                Cells = cells,
                Operator = g.Grid[cells[1].row, cells[1].col].Solution
            };
            return true;
        }

        if (len == 7)
        {
            if (!IsType(0, CellType.Number)) return false;
            if (!IsType(1, CellType.Operator)) return false;
            if (!IsType(2, CellType.Number)) return false;
            if (!IsType(3, CellType.Operator)) return false;
            if (!IsType(4, CellType.Number)) return false;
            if (!IsType(5, CellType.Equals)) return false;
            if (!IsType(6, CellType.Number)) return false;

            eq = new MathEquation
            {
                Cells = cells,
                Operator = g.Grid[cells[1].row, cells[1].col].Solution,
                Operator2 = g.Grid[cells[3].row, cells[3].col].Solution
            };
            return true;
        }

        // len == 9 (Master)
        if (!IsType(0, CellType.Number)) return false;
        if (!IsType(1, CellType.Operator)) return false;
        if (!IsType(2, CellType.Number)) return false;
        if (!IsType(3, CellType.Operator)) return false;
        if (!IsType(4, CellType.Number)) return false;
        if (!IsType(5, CellType.Operator)) return false;
        if (!IsType(6, CellType.Number)) return false;
        if (!IsType(7, CellType.Equals)) return false;
        if (!IsType(8, CellType.Number)) return false;

        eq = new MathEquation
        {
            Cells = cells,
            Operator = g.Grid[cells[1].row, cells[1].col].Solution,
            Operator2 = g.Grid[cells[3].row, cells[3].col].Solution,
            Operator3 = g.Grid[cells[5].row, cells[5].col].Solution
        };
        return true;
    }

    private static bool IsSingleComponent(MathCrossGame game)
    {
        int startR = -1, startC = -1, total = 0;

        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
            {
                if (game.Grid[r, c].Type == CellType.Empty) continue;
                total++;
                if (startR == -1) { startR = r; startC = c; }
            }

        if (total == 0) return false;

        var q = new Queue<(int, int)>();
        var vis = new bool[game.Rows, game.Cols];

        q.Enqueue((startR, startC));
        vis[startR, startC] = true;

        int count = 0;
        int[] rr = { -1, 1, 0, 0 };
        int[] cc = { 0, 0, -1, 1 };

        while (q.Count > 0)
        {
            var (cr, ccol) = q.Dequeue();
            count++;

            for (int i = 0; i < 4; i++)
            {
                int nr = cr + rr[i], nc = ccol + cc[i];
                if (nr < 0 || nr >= game.Rows || nc < 0 || nc >= game.Cols) continue;
                if (vis[nr, nc]) continue;
                if (game.Grid[nr, nc].Type == CellType.Empty) continue;

                vis[nr, nc] = true;
                q.Enqueue((nr, nc));
            }
        }

        return count == total;
    }

    // ===== SETTINGS =====

    private static Settings GetSettings(string key)
    {
        key = (key ?? "easy").Trim().ToLowerInvariant();
        if (key == "einfach") key = "easy";
        if (key == "schwer") key = "hard";

        // ✅ Pro Schwierigkeit: 8 bis 12 Gleichungen
        const int minEq = 8;
        const int maxEq = 12;

        return key switch
        {
            // Easy: 5er Gleichung A op B = C
            "easy" => new Settings(
                DifficultyKey: "easy",
                MinVal: 1,
                MaxVal: 20,
                AllowNegative: false,
                Ops: new[] { "+", "-", "×", "÷" },
                MinEquations: minEq,
                MaxEquations: maxEq,
                GivenPercent: 0.40,
                MaxAbsResult: 200,
                MaxSpan: 10,
                EquationLength: 5
            ),

            // Normal: 5er Gleichung A op B = C
            "normal" => new Settings(
                DifficultyKey: "normal",
                MinVal: 1,
                MaxVal: 60,
                AllowNegative: false,
                Ops: new[] { "+", "-", "×", "÷" },
                MinEquations: minEq,
                MaxEquations: maxEq,
                GivenPercent: 0.35,
                MaxAbsResult: 300,
                MaxSpan: 12,
                EquationLength: 5
            ),

            // Hard: 7er Gleichung A op B op C = D
            "hard" => new Settings(
                DifficultyKey: "hard",
                MinVal: 1,
                MaxVal: 99,
                AllowNegative: false,
                Ops: new[] { "+", "-", "×", "÷" },
                MinEquations: minEq,
                MaxEquations: maxEq,
                GivenPercent: 0.30,
                MaxAbsResult: 2000,
                MaxSpan: 16,
                EquationLength: 7
            ),

            // Master: 7er Gleichung A op B op C = D + negative erlaubt
            "master" => new Settings(
                DifficultyKey: "master",
                MinVal: -99,
                MaxVal: 99,
                AllowNegative: true,
                Ops: new[] { "+", "-", "×", "÷" },
                MinEquations: minEq,
                MaxEquations: maxEq,
                GivenPercent: 0.25,
                MaxAbsResult: 3000,
                MaxSpan: 18,
                EquationLength: 7
            ),

            _ => GetSettings("easy")
        };
    }


    // ===== HELPERS =====

    private static MathCrossCell[,] CreateEmptyGrid(int rows, int cols)
    {
        var g = new MathCrossCell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                g[r, c] = new MathCrossCell { Row = r, Col = c, Type = CellType.Empty, Solution = "", UserInput = "", IsGiven = false };
        return g;
    }

    private static bool InBounds(MathCrossCell[,] grid, int r, int c)
        => r >= 0 && r < grid.GetLength(0) && c >= 0 && c < grid.GetLength(1);

    private static int Rnd(Random r, Settings s) => r.Next(s.MinVal, s.MaxVal + 1);

    private static int RndSmall(Random r, Settings s)
    {
        // kleine Werte für ×/÷, Master darf negativ
        int lo = s.AllowNegative ? -12 : 1;
        int hi = 12;
        return r.Next(lo, hi + 1);
    }

    private static bool Valid(Settings s, int v)
        => v >= s.MinVal && v <= s.MaxVal && Math.Abs(v) <= s.MaxAbsResult;

    private static List<(int r, int c, int v)> GetNumberCells(MathCrossCell[,] g)
    {
        var l = new List<(int, int, int)>();
        for (int r = 0; r < g.GetLength(0); r++)
            for (int c = 0; c < g.GetLength(1); c++)
            {
                if (g[r, c].Type != CellType.Number) continue;
                if (int.TryParse(g[r, c].Solution, out var v))
                    l.Add((r, c, v));
            }
        return l;
    }

    private static (int minR, int maxR, int minC, int maxC) GetBBox(MathCrossCell[,] grid)
    {
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        int minR = rows, maxR = -1, minC = cols, maxC = -1;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c].Type == CellType.Empty) continue;
                minR = Math.Min(minR, r);
                maxR = Math.Max(maxR, r);
                minC = Math.Min(minC, c);
                maxC = Math.Max(maxC, c);
            }
        return (minR, maxR, minC, maxC);
    }

    private static int Span((int minR, int maxR, int minC, int maxC) b)
        => (b.maxR < 0) ? 0 : Math.Max(b.maxR - b.minR + 1, b.maxC - b.minC + 1);
}
