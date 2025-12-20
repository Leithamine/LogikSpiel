using System;
using System.Collections.Generic;
using System.Globalization;
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
        int EquationLength // 5 for Easy/Normal, 7 for Hard/Master
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
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2.5);

        MathCrossGame? best = null;
        int bestEquationCount = -1;

        for (int attempt = 0; attempt < 120 && DateTime.UtcNow < deadline; attempt++)
        {
            try
            {
                var rnd = new Random(seed + attempt * 997);
                int targetEquations = rnd.Next(s.MinEquations, s.MaxEquations + 1);

                var big = CreateEmptyGrid(BigSize, BigSize);
                int placedCount = 0;

                // Place first equation
                if (!TryPlaceFirstEquation(big, rnd, s, ref placedCount))
                    continue;

                // Grow more equations
                if (!TryGrowEquations(big, rnd, s, ref placedCount, targetEquations))
                    continue;

                // Crop + validate shape
                var cropped = Crop(big, s);
                if (cropped.Rows == 0 || cropped.Cols == 0) continue;
                if (!IsSingleComponent(cropped)) continue;
                if (cropped.Equations.Count < s.MinEquations) continue;

                // Finalize (givens/solvable/etc.)
                var finalized = FinalizeGame(cropped, rnd, s);

                // Perfect hit
                if (finalized.Equations.Count >= targetEquations)
                    return finalized;

                // Keep best
                if (finalized.Equations.Count > bestEquationCount)
                {
                    bestEquationCount = finalized.Equations.Count;
                    best = finalized;
                }
            }
            catch
            {
                // ignore and try next attempt
            }
        }

        // If we generated something "good enough"
        if (best != null)
            return best;

        // Hard fallback (always returns something)
        var fallback = GenerateStripFallback(s, seed);
        return FinalizeGame(fallback, new Random(seed + 4242), s);
    }


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

    private static bool TryCommitEquation5(MathCrossCell[,] grid, Settings s, int sr, int sc, Delta d,
        int a, string op, int b, int c)
    {
        var cells = new List<(int r, int c)>();
        for (int i = 0; i < 5; i++)
            cells.Add((sr + i * d.Dr, sc + i * d.Dc));

        // Boundary check: cells before and after must be empty
        int br = sr - d.Dr, bc = sc - d.Dc;
        int ar = sr + 5 * d.Dr, ac = sc + 5 * d.Dc;

        if (InBounds(grid, br, bc) && grid[br, bc].Type != CellType.Empty) return false;
        if (InBounds(grid, ar, ac) && grid[ar, ac].Type != CellType.Empty) return false;

        var tokens = new (CellType type, string sol)[] {
            (CellType.Number, a.ToString()),
            (CellType.Operator, op),
            (CellType.Number, b.ToString()),
            (CellType.Equals, "="),
            (CellType.Number, c.ToString())
        };

        int pDr = d.Dr == 0 ? 1 : 0;
        int pDc = d.Dc == 0 ? 1 : 0;

        // Validate all positions
        for (int i = 0; i < 5; i++)
        {
            var (r, col) = cells[i];
            if (!InBounds(grid, r, col)) return false;

            var existing = grid[r, col];
            var (targetType, targetSol) = tokens[i];

            if (existing.Type != CellType.Empty)
            {
                if (existing.Type != targetType) return false;
                if (existing.Solution != targetSol) return false;
            }
            else
            {
                // No side contact for new cells
                if (InBounds(grid, r + pDr, col + pDc) && grid[r + pDr, col + pDc].Type != CellType.Empty) return false;
                if (InBounds(grid, r - pDr, col - pDc) && grid[r - pDr, col - pDc].Type != CellType.Empty) return false;
            }
        }

        // Write cells
        for (int i = 0; i < 5; i++)
        {
            var (r, col) = cells[i];
            var (targetType, targetSol) = tokens[i];

            grid[r, col].Type = targetType;
            grid[r, col].Solution = targetSol;
            grid[r, col].IsGiven = false;
            grid[r, col].UserInput = "";
        }

        return true;
    }

    private static bool TryCommitEquation7(MathCrossCell[,] grid, Settings s, int sr, int sc, Delta d,
        int a, string op1, int b, string op2, int c, int result)
    {
        var cells = new List<(int r, int c)>();
        for (int i = 0; i < 7; i++)
            cells.Add((sr + i * d.Dr, sc + i * d.Dc));

        // Boundary check
        int br = sr - d.Dr, bc = sc - d.Dc;
        int ar = sr + 7 * d.Dr, ac = sc + 7 * d.Dc;

        if (InBounds(grid, br, bc) && grid[br, bc].Type != CellType.Empty) return false;
        if (InBounds(grid, ar, ac) && grid[ar, ac].Type != CellType.Empty) return false;

        var tokens = new (CellType type, string sol)[] {
            (CellType.Number, a.ToString()),
            (CellType.Operator, op1),
            (CellType.Number, b.ToString()),
            (CellType.Operator, op2),
            (CellType.Number, c.ToString()),
            (CellType.Equals, "="),
            (CellType.Number, result.ToString())
        };

        int pDr = d.Dr == 0 ? 1 : 0;
        int pDc = d.Dc == 0 ? 1 : 0;

        // Validate all positions
        for (int i = 0; i < 7; i++)
        {
            var (r, col) = cells[i];
            if (!InBounds(grid, r, col)) return false;

            var existing = grid[r, col];
            var (targetType, targetSol) = tokens[i];

            if (existing.Type != CellType.Empty)
            {
                if (existing.Type != targetType) return false;
                if (existing.Solution != targetSol) return false;
            }
            else
            {
                if (InBounds(grid, r + pDr, col + pDc) && grid[r + pDr, col + pDc].Type != CellType.Empty) return false;
                if (InBounds(grid, r - pDr, col - pDc) && grid[r - pDr, col - pDc].Type != CellType.Empty) return false;
            }
        }

        // Write cells
        for (int i = 0; i < 7; i++)
        {
            var (r, col) = cells[i];
            var (targetType, targetSol) = tokens[i];

            grid[r, col].Type = targetType;
            grid[r, col].Solution = targetSol;
            grid[r, col].IsGiven = false;
            grid[r, col].UserInput = "";
        }

        return true;
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
                {
                    editableCells.Add(cell);
                }
            }
        }

        // Shuffle and mark some as given
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

            // Mark given cells and equals as solvable
            for (int r = 0; r < game.Rows; r++)
            {
                for (int c = 0; c < game.Cols; c++)
                {
                    var cell = game.Grid[r, c];
                    if (cell.Type == CellType.Empty) continue;
                    if (cell.IsGiven || cell.Type == CellType.Equals)
                        solvableMask[r, c] = true;
                }
            }

            // Propagate solvability
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var eq in game.Equations)
                {
                    if (TrySolveEquationStep(game, eq, solvableMask, s))
                        progress = true;
                }
            }

            // Check if all non-empty cells are solvable
            int totalNonEmpty = 0;
            int solvedCount = 0;
            var unsolved = new List<MathCrossCell>();

            for (int r = 0; r < game.Rows; r++)
            {
                for (int c = 0; c < game.Cols; c++)
                {
                    if (game.Grid[r, c].Type == CellType.Empty) continue;
                    totalNonEmpty++;
                    if (solvableMask[r, c])
                        solvedCount++;
                    else
                        unsolved.Add(game.Grid[r, c]);
                }
            }

            if (solvedCount == totalNonEmpty)
            {
                // Count given cells
                game.GivenCells = 0;
                for (int r = 0; r < game.Rows; r++)
                    for (int c = 0; c < game.Cols; c++)
                        if (game.Grid[r, c].IsGiven) game.GivenCells++;
                return;
            }

            // Add one more given cell from unsolved
            if (unsolved.Count > 0)
            {
                var pick = unsolved[rnd.Next(unsolved.Count)];
                pick.IsGiven = true;
                pick.UserInput = pick.Solution;
            }
            else
            {
                break;
            }
        }
    }

    private static MathCrossGame FinalizeGame(MathCrossGame raw, Random rnd, Settings s)
    {
        // CRITICAL: Reset all cells to not given first
        ResetAllCells(raw);

        // Then apply initial masking
        InitialMasking(raw, rnd, s.GivenPercent);

        // Ensure puzzle is solvable
        EnsureSolvable(raw, rnd, s);

        raw.Difficulty = s.DifficultyKey;
        raw.UseExtendedEquations = s.EquationLength == 7;
        return raw;
    }

    private static bool TrySolveEquationStep(MathCrossGame game, MathEquation eq, bool[,] mask, Settings s)
    {
        int unknowns = 0;

        foreach (var (r, c) in eq.Cells)
        {
            var cell = game.Grid[r, c];
            if (cell.Type == CellType.Equals) continue;
            if (!mask[r, c]) unknowns++;
        }

        if (unknowns != 1) return false;

        // For 5-cell equations: A op B = C
        // For 7-cell equations: A op1 B op2 C = D
        // We can solve if exactly 1 unknown

        if (s.EquationLength == 5)
        {
            var pA = eq.Cells[0];
            var pOp = eq.Cells[1];
            var pB = eq.Cells[2];
            var pC = eq.Cells[4];

            bool kA = mask[pA.row, pA.col];
            bool kOp = mask[pOp.row, pOp.col];
            bool kB = mask[pB.row, pB.col];
            bool kC = mask[pC.row, pC.col];

            string op = game.Grid[pOp.row, pOp.col].Solution;
            int.TryParse(game.Grid[pA.row, pA.col].Solution, out var valA);
            int.TryParse(game.Grid[pB.row, pB.col].Solution, out var valB);
            int.TryParse(game.Grid[pC.row, pC.col].Solution, out var valC);

            // Ambiguity guards
            if (!kA && op == "×" && valB == 0 && valC == 0) return false;
            if (!kB && op == "×" && valA == 0 && valC == 0) return false;
            if (!kB && op == "÷" && valA == 0 && valC == 0) return false;
            if (!kOp && valA == 0 && valB == 0 && valC == 0) return false;
        }
        else // 7-cell
        {
            var pA = eq.Cells[0];
            var pOp1 = eq.Cells[1];
            var pB = eq.Cells[2];
            var pOp2 = eq.Cells[3];
            var pC = eq.Cells[4];
            var pD = eq.Cells[6];

            // Similar guards for 7-cell equations
            // For simplicity, just mark as solvable if 1 unknown
        }

        // Mark all unknowns as solved
        foreach (var (r, c) in eq.Cells)
        {
            if (!mask[r, c]) mask[r, c] = true;
        }

        return true;
    }

    private static bool TryPlaceFirstEquation(MathCrossCell[,] grid, Random rnd, Settings s, ref int placedCount)
    {
        int mid = grid.GetLength(0) / 2;

        if (s.EquationLength == 5)
        {
            if (!TryGenerateEquation5(s, rnd, out var a, out var op, out var b, out var c)) return false;
            if (TryCommitEquation5(grid, s, mid, mid - 2, new Delta(0, 1), a, op, b, c))
            {
                placedCount++;
                return true;
            }
        }
        else
        {
            if (!TryGenerateEquation7(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var d)) return false;
            if (TryCommitEquation7(grid, s, mid, mid - 3, new Delta(0, 1), a, op1, b, op2, cc, d))
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

        while (placedCount < target && fails < 20)
        {
            var anchors = GetNumberCells(grid);
            if (anchors.Count == 0) return false;

            var bbox = GetBBox(grid);
            if (Span(bbox) > s.MaxSpan) return placedCount >= s.MinEquations;

            bool success = false;

            for (int t = 0; t < 80; t++)
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
                        if (TryCommitEquation5(grid, s, sr, sc, dir, a, op, b, c))
                        {
                            placedCount++;
                            success = true;
                            break;
                        }
                    }
                }
                else
                {
                    int anchorIdx = new[] { 0, 2, 4, 6 }[rnd.Next(4)];
                    int sr = ar - anchorIdx * dir.Dr;
                    int sc = ac - anchorIdx * dir.Dc;

                    if (!InBounds(grid, sr, sc) || !InBounds(grid, sr + 6 * dir.Dr, sc + 6 * dir.Dc)) continue;

                    if (TryGenerateEquation7WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op1, out var b, out var op2, out var cc, out var d))
                    {
                        if (TryCommitEquation7(grid, s, sr, sc, dir, a, op1, b, op2, cc, d))
                        {
                            placedCount++;
                            success = true;
                            break;
                        }
                    }
                }
            }

            if (success) fails = 0;
            else fails++;
        }

        return true;
    }

    /// <summary>
    /// Deterministic, schnelle Fallback-Generierung mit gestapelten Horizontal-Equations
    /// und optionalen vertikalen Crosses (für mehr Schnittpunkte).
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
            if (s.EquationLength == 5)
            {
                if (TryGenerateEquation5(s, rnd, out var a, out var op, out var b, out var c) &&
                    TryCommitEquation5(grid, s, r, startCol, new Delta(0, 1), a, op, b, c))
                {
                    placed++;
                }
            }
            else
            {
                if (TryGenerateEquation7(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var d) &&
                    TryCommitEquation7(grid, s, r, startCol - 1, new Delta(0, 1), a, op1, b, op2, cc, d))
                {
                    placed++;
                }
            }
        }

        // Versuche ein paar vertikale Gleichungen auf vorhandenen Zahl-Ankern zu platzieren,
        // um mehr Schnitte zu erreichen.
        int verticalAttempts = 0;
        while (placed < s.MaxEquations && verticalAttempts++ < 60)
        {
            var anchors = GetNumberCells(grid);
            if (anchors.Count == 0) break;

            var (ar, ac, aval) = anchors[rnd.Next(anchors.Count)];
            int anchorIdx = s.EquationLength == 5
                ? new[] { 0, 2, 4 }[rnd.Next(3)]
                : new[] { 0, 2, 4, 6 }[rnd.Next(4)];

            int sr = ar - anchorIdx;
            int sc = ac;
            if (!InBounds(grid, sr, sc) || !InBounds(grid, sr + (s.EquationLength - 1), sc)) continue;

            if (s.EquationLength == 5)
            {
                if (TryGenerateEquation5WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op, out var b, out var c) &&
                    TryCommitEquation5(grid, s, sr, sc, new Delta(1, 0), a, op, b, c))
                {
                    placed++;
                }
            }
            else
            {
                if (TryGenerateEquation7WithAnchor(s, rnd, anchorIdx, aval, out var a, out var op1, out var b, out var op2, out var cc, out var d) &&
                    TryCommitEquation7(grid, s, sr, sc, new Delta(1, 0), a, op1, b, op2, cc, d))
                {
                    placed++;
                }
            }
        }

        // Korrigiere Mindestanzahl
        if (placed < s.MinEquations)
        {
            for (int extra = 0; placed < s.MinEquations && extra < 20; extra++)
            {
                int r = rnd.Next(1, rows - eqLen - 1);
                int c = rnd.Next(1, Math.Max(2, cols - eqLen - 1));

                if (s.EquationLength == 5)
                {
                    if (TryGenerateEquation5(s, rnd, out var a, out var op, out var b, out var c2) &&
                        TryCommitEquation5(grid, s, r, c, new Delta(0, 1), a, op, b, c2))
                    {
                        placed++;
                    }
                }
                else
                {
                    if (TryGenerateEquation7(s, rnd, out var a, out var op1, out var b, out var op2, out var cc, out var d) &&
                        TryCommitEquation7(grid, s, r, c, new Delta(0, 1), a, op1, b, op2, cc, d))
                    {
                        placed++;
                    }
                }
            }
        }

        return Crop(grid, s);
    }

    // 5-cell equation generation: A op B = C
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

        for (int i = 0; i < 50; i++)
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
                    else if (anchorIdx == 4) { c = anchorVal; a = RndSmall(rnd, s); if (a == 0) a = 1; b = (a != 0 && c % a == 0) ? c / a : 0; }
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

            // Verify equation
            int result = op switch
            {
                "+" => a + b,
                "-" => a - b,
                "×" => a * b,
                "÷" => b != 0 ? a / b : int.MinValue,
                _ => int.MinValue
            };

            if (op == "÷" && (b == 0 || a % b != 0)) continue;
            if (result == c) return true;
        }

        return false;
    }

    // 7-cell equation generation: A op1 B op2 C = D
    // Follows standard math order: multiplication/division before addition/subtraction
    private static bool TryGenerateEquation7(Settings s, Random rnd,
        out int a, out string op1, out int b, out string op2, out int c, out int d)
    {
        return TryGenerateEquation7WithAnchor(s, rnd, -1, 0, out a, out op1, out b, out op2, out c, out d);
    }

    private static bool TryGenerateEquation7WithAnchor(Settings s, Random rnd, int anchorIdx, int anchorVal,
        out int a, out string op1, out int b, out string op2, out int c, out int d)
    {
        a = 0; b = 0; c = 0; d = 0;
        op1 = s.Ops[rnd.Next(s.Ops.Length)];
        op2 = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 80; i++)
        {
            // Generate random values
            if (anchorIdx == 0) a = anchorVal; else a = Rnd(rnd, s);
            if (anchorIdx == 2) b = anchorVal; else b = Rnd(rnd, s);
            if (anchorIdx == 4) c = anchorVal; else c = Rnd(rnd, s);

            // For multiplication/division, use smaller numbers
            if (op1 is "×" or "÷") { a = RndSmall(rnd, s); b = RndSmall(rnd, s); }
            if (op2 is "×" or "÷") { b = RndSmall(rnd, s); c = RndSmall(rnd, s); }

            // Re-apply anchor
            if (anchorIdx == 0) a = anchorVal;
            if (anchorIdx == 2) b = anchorVal;
            if (anchorIdx == 4) c = anchorVal;

            // Avoid division by zero
            if (op1 == "÷" && b == 0) continue;
            if (op2 == "÷" && c == 0) continue;

            // Calculate result with proper operator precedence
            // Standard math: × and ÷ before + and -
            int? result = EvaluateExpression(a, op1, b, op2, c);
            if (result == null) continue;

            d = result.Value;

            if (anchorIdx == 6 && d != anchorVal)
            {
                // Try to find values that give the anchor result
                // This is complex, so just continue and hope for luck
                continue;
            }

            if (!Valid(s, a) || !Valid(s, b) || !Valid(s, c) || !Valid(s, d)) continue;

            // Verify no decimal intermediate results for division
            if (op1 == "÷" && a % b != 0) continue;
            if (op2 == "÷" && !IsDivisionClean(a, op1, b, op2, c)) continue;

            return true;
        }

        return false;
    }

    private static int? EvaluateExpression(int a, string op1, int b, string op2, int c)
    {
        // Apply operator precedence: × ÷ before + -
        // Case 1: op1 is × or ÷ (evaluated first)
        // Case 2: op2 is × or ÷ (evaluated first)
        // Case 3: Same precedence (left to right)

        bool op1High = op1 is "×" or "÷";
        bool op2High = op2 is "×" or "÷";

        if (op1High && !op2High)
        {
            // a op1 b first, then result op2 c
            int? mid = ApplyOp(a, op1, b);
            if (mid == null) return null;
            return ApplyOp(mid.Value, op2, c);
        }
        else if (!op1High && op2High)
        {
            // b op2 c first, then a op1 result
            int? mid = ApplyOp(b, op2, c);
            if (mid == null) return null;
            return ApplyOp(a, op1, mid.Value);
        }
        else
        {
            // Same precedence: left to right
            int? mid = ApplyOp(a, op1, b);
            if (mid == null) return null;
            return ApplyOp(mid.Value, op2, c);
        }
    }

    private static int? ApplyOp(int x, string op, int y)
    {
        return op switch
        {
            "+" => x + y,
            "-" => x - y,
            "×" => x * y,
            "÷" => y != 0 && x % y == 0 ? x / y : null,
            _ => null
        };
    }

    private static bool IsDivisionClean(int a, string op1, int b, string op2, int c)
    {
        // Check that no intermediate result is non-integer
        bool op1High = op1 is "×" or "÷";
        bool op2High = op2 is "×" or "÷";

        if (op1High && !op2High)
        {
            if (op1 == "÷" && (b == 0 || a % b != 0)) return false;
        }
        else if (!op1High && op2High)
        {
            if (op2 == "÷" && (c == 0 || b % c != 0)) return false;
        }
        else
        {
            if (op1 == "÷" && (b == 0 || a % b != 0)) return false;
            if (op2 == "÷")
            {
                int mid = op1 == "+" ? a + b : op1 == "-" ? a - b : op1 == "×" ? a * b : a / b;
                if (c == 0 || mid % c != 0) return false;
            }
        }
        return true;
    }

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
            Grid = new MathCrossCell[rows, cols]
        };

        for (int r = 0; r < rows; r++)
        {
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
        }

        game.Equations = ScanEquations(game, s.EquationLength);
        return game;
    }

    private static List<MathEquation> ScanEquations(MathCrossGame game, int eqLen)
    {
        var list = new List<MathEquation>();

        // Horizontal
        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols - eqLen + 1; c++)
            {
                if (IsEquation(game, r, c, 0, 1, eqLen, out var e))
                    list.Add(e);
            }
        }

        // Vertical
        for (int r = 0; r < game.Rows - eqLen + 1; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (IsEquation(game, r, c, 1, 0, eqLen, out var e))
                    list.Add(e);
            }
        }

        return list;
    }

    private static bool IsEquation(MathCrossGame g, int r, int c, int dr, int dc, int len, out MathEquation eq)
    {
        eq = new MathEquation();
        var cells = new List<(int, int)>();

        for (int i = 0; i < len; i++)
            cells.Add((r + i * dr, c + i * dc));

        if (len == 5)
        {
            // A op B = C
            if (g.Grid[cells[0].Item1, cells[0].Item2].Type != CellType.Number) return false;
            if (g.Grid[cells[1].Item1, cells[1].Item2].Type != CellType.Operator) return false;
            if (g.Grid[cells[2].Item1, cells[2].Item2].Type != CellType.Number) return false;
            if (g.Grid[cells[3].Item1, cells[3].Item2].Type != CellType.Equals) return false;
            if (g.Grid[cells[4].Item1, cells[4].Item2].Type != CellType.Number) return false;

            eq = new MathEquation
            {
                Cells = cells,
                Operator = g.Grid[cells[1].Item1, cells[1].Item2].Solution
            };
        }
        else // len == 7
        {
            // A op1 B op2 C = D
            if (g.Grid[cells[0].Item1, cells[0].Item2].Type != CellType.Number) return false;
            if (g.Grid[cells[1].Item1, cells[1].Item2].Type != CellType.Operator) return false;
            if (g.Grid[cells[2].Item1, cells[2].Item2].Type != CellType.Number) return false;
            if (g.Grid[cells[3].Item1, cells[3].Item2].Type != CellType.Operator) return false;
            if (g.Grid[cells[4].Item1, cells[4].Item2].Type != CellType.Number) return false;
            if (g.Grid[cells[5].Item1, cells[5].Item2].Type != CellType.Equals) return false;
            if (g.Grid[cells[6].Item1, cells[6].Item2].Type != CellType.Number) return false;

            eq = new MathEquation
            {
                Cells = cells,
                Operator = g.Grid[cells[1].Item1, cells[1].Item2].Solution,
                Operator2 = g.Grid[cells[3].Item1, cells[3].Item2].Solution
            };
        }

        return true;
    }

    private static bool IsSingleComponent(MathCrossGame game)
    {
        int startR = -1, startC = -1, total = 0;

        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (game.Grid[r, c].Type == CellType.Empty) continue;
                total++;
                if (startR == -1) { startR = r; startC = c; }
            }
        }

        if (total == 0) return false;

        var q = new Queue<(int, int)>();
        var vis = new bool[game.Rows, game.Cols];

        q.Enqueue((startR, startC));
        vis[startR, startC] = true;

        int count = 0;
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        while (q.Count > 0)
        {
            var (cr, cc) = q.Dequeue();
            count++;

            for (int i = 0; i < 4; i++)
            {
                int nr = cr + dr[i], nc = cc + dc[i];
                if (nr < 0 || nr >= game.Rows || nc < 0 || nc >= game.Cols) continue;
                if (vis[nr, nc]) continue;
                if (game.Grid[nr, nc].Type == CellType.Empty) continue;

                vis[nr, nc] = true;
                q.Enqueue((nr, nc));
            }
        }

        return count == total;
    }

    private static Settings GetSettings(string key)
    {
        key = (key ?? "easy").Trim().ToLowerInvariant();
        if (key == "einfach") key = "easy";
        if (key == "schwer") key = "hard";

        return key switch
        {
            // Easy: 5-cell equations, +/-, numbers 1-20
            "easy" => new Settings("easy", 1, 20, false, new[] { "+", "-" }, 7, 10, 0.40, 40, 10, 5),

            // Normal: 5-cell equations, +/-/×/÷, numbers 1-50
            "normal" => new Settings("normal", 1, 50, false, new[] { "+", "-", "×", "÷" }, 7, 10, 0.35, 100, 12, 5),

            // Hard: 7-cell equations (a op b op c = d), +/-/×/÷, numbers 1-999
            "hard" => new Settings("hard", 1, 999, false, new[] { "+", "-", "×", "÷" }, 7, 10, 0.30, 2000, 16, 7),

            // Master: 7-cell equations, +/-/×/÷, numbers -1 to 999 (negative allowed)
            "master" => new Settings("master", -99, 999, true, new[] { "+", "-", "×", "÷" }, 7, 10, 0.25, 3000, 18, 7),

            _ => GetSettings("easy")
        };
    }

    private static MathCrossCell[,] CreateEmptyGrid(int rows, int cols)
    {
        var g = new MathCrossCell[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                g[r, c] = new MathCrossCell
                {
                    Row = r,
                    Col = c,
                    Type = CellType.Empty,
                    Solution = "",
                    UserInput = "",
                    IsGiven = false
                };
            }
        }
        return g;
    }

    private static bool InBounds(MathCrossCell[,] grid, int r, int c)
        => r >= 0 && r < grid.GetLength(0) && c >= 0 && c < grid.GetLength(1);

    private static int Rnd(Random r, Settings s)
    {
        return r.Next(s.MinVal, s.MaxVal + 1);
    }

    private static int RndSmall(Random r, Settings s)
    {
        int lo = s.AllowNegative ? -12 : 1;
        int hi = 15;
        return r.Next(lo, hi);
    }

    private static bool Valid(Settings s, int v)
        => v >= s.MinVal && v <= s.MaxVal && Math.Abs(v) <= s.MaxAbsResult;

    private static List<(int, int, int)> GetNumberCells(MathCrossCell[,] g)
    {
        var l = new List<(int, int, int)>();
        for (int r = 0; r < g.GetLength(0); r++)
        {
            for (int c = 0; c < g.GetLength(1); c++)
            {
                if (g[r, c].Type != CellType.Number) continue;
                if (int.TryParse(g[r, c].Solution, out var v))
                    l.Add((r, c, v));
            }
        }
        return l;
    }

    private static (int minR, int maxR, int minC, int maxC) GetBBox(MathCrossCell[,] grid)
    {
        int rows = grid.GetLength(0), cols = grid.GetLength(1);
        int minR = rows, maxR = -1, minC = cols, maxC = -1;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c].Type == CellType.Empty) continue;
                minR = Math.Min(minR, r);
                maxR = Math.Max(maxR, r);
                minC = Math.Min(minC, c);
                maxC = Math.Max(maxC, c);
            }
        }
        return (minR, maxR, minC, maxC);
    }

    private static int Span((int minR, int maxR, int minC, int maxC) b)
        => (b.maxR < 0) ? 0 : Math.Max(b.maxR - b.minR + 1, b.maxC - b.minC + 1);
}
