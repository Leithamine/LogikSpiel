#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

/// <summary>
/// Fast MathCross generator (simple architecture, bounded retries).
/// </summary>
public sealed class MathCrossGeneratorService
{
    private const int GridSize = 16;
    private const int DefaultMaxLayoutSize = 10;
    private const int ExtendedMaxLayoutSize = 12;
    private const int EquationVariantsPerAnchor = 1;
    private const double IntersectionBonus = 14.0;
    private const double AreaGrowthPenalty = 1.2;
    private const double AspectRatioPenalty = 2.0;
    private const double CenterDistancePenalty = 0.35;

    public MathCrossGame GenerateGame(string difficultyKey, int seed)
    {
        var s = GetSettings(difficultyKey);
        var sw = Stopwatch.StartNew();
        TimeSpan generationTimeout = TimeSpan.FromMilliseconds(300);

        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (sw.Elapsed >= generationTimeout) break;

            var game = TryGenerate(s, new Random(seed + attempt * 977), sw, generationTimeout);
            if (game == null) continue;
            if (game.Equations.Count < s.MinEquations || game.Equations.Count > s.MaxEquations) continue;

            FinalizeGame(game, new Random(seed + attempt * 1231 + 7), s);
            if (IsPlayable(game))
                return game;
        }

        var safe = GenerateFallbackGrid(s, new Random(seed + 777_777));
        FinalizeGame(safe, new Random(seed + 888_888), s);
        return safe;
    }

    private MathCrossGame? TryGenerate(Settings s, Random rnd, Stopwatch sw, TimeSpan timeout)
    {
        if (sw.Elapsed >= timeout) return null;

        var grid = new CellType[GridSize, GridSize];
        var solutions = new string[GridSize, GridSize];

        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
            {
                grid[r, c] = CellType.Empty;
                solutions[r, c] = "";
            }

        var placed = new List<EquationPlacement>();
        int target = rnd.Next(s.MinEquations, s.MaxEquations + 1);

        int midR = GridSize / 2;
        int midC = (GridSize - s.EquationLength) / 2;

        var first = GenerateEquation(s, rnd);
        if (first == null) return null;

        Place(grid, solutions, midR, midC, horizontal: true, first, s.EquationLength);
        placed.Add(new EquationPlacement(midR, midC, true, first));

        var bounds = ComputeBounds(grid);
        var boundsHistory = new List<Bounds> { bounds };
        var changeStack = new List<List<CellChange>>();

        int maxWidth = s.IsExtended ? ExtendedMaxLayoutSize : DefaultMaxLayoutSize;
        int maxHeight = s.IsExtended ? ExtendedMaxLayoutSize : DefaultMaxLayoutSize;
        int maxDepth = Math.Max(maxWidth, maxHeight) / 2 + 2;

        int fails = 0;
        while (placed.Count < target && fails < 50)
        {
            if (sw.Elapsed >= timeout)
                return null;

            fails++;

            var best = FindBestPlacement(grid, solutions, s, rnd, bounds, midR, midC, maxWidth, maxHeight, maxDepth);
            if (best == null)
            {
                if (placed.Count > 1)
                {
                    placed.RemoveAt(placed.Count - 1);
                    UndoChanges(grid, solutions, changeStack[^1]);
                    changeStack.RemoveAt(changeStack.Count - 1);
                    boundsHistory.RemoveAt(boundsHistory.Count - 1);
                    bounds = boundsHistory[^1];
                    continue;
                }

                continue;
            }

            var changes = PlaceWithChanges(grid, solutions, best.StartR, best.StartC, horizontal: !best.Vertical, best.Equation, s.EquationLength);
            placed.Add(new EquationPlacement(best.StartR, best.StartC, !best.Vertical, best.Equation));
            changeStack.Add(changes);
            bounds = best.NewBounds;
            boundsHistory.Add(bounds);
            fails = 0;
        }

        if (placed.Count < s.MinEquations || placed.Count > s.MaxEquations)
            return null;

        var game = BuildGame(grid, solutions, s);
        if (game.Equations.Count < s.MinEquations || game.Equations.Count > s.MaxEquations)
            return null;

        if (!HasValidTopology(game))
            return null;

        return game;
    }

    private PlacementCandidate? FindBestPlacement(
        CellType[,] grid,
        string[,] solutions,
        Settings s,
        Random rnd,
        Bounds currentBounds,
        int centerR,
        int centerC,
        int maxWidth,
        int maxHeight,
        int maxDepth)
    {
        var numbers = GetNumberPositions(grid, solutions)
            .OrderBy(_ => rnd.Next())
            .ToList();
        if (numbers.Count == 0) return null;

        int oldArea = currentBounds.Area;
        int[] anchors = s.IsExtended ? new[] { 2, 4 } : new[] { 2 };
        PlacementCandidate? best = null;

        foreach (var (nr, nc, val) in numbers)
        {
            bool hasHorizontal = HasEquationInDirection(grid, nr, nc, horizontal: true);
            bool hasVertical = HasEquationInDirection(grid, nr, nc, horizontal: false);
            if (hasHorizontal && hasVertical) continue;

            var directions = new List<bool>();
            if (!hasVertical) directions.Add(true);
            if (!hasHorizontal) directions.Add(false);

            foreach (var vertical in directions)
            {
                foreach (var anchor in anchors)
                {
                    int startR = vertical ? nr - anchor : nr;
                    int startC = vertical ? nc : nc - anchor;

                    for (int variant = 0; variant < EquationVariantsPerAnchor; variant++)
                    {
                        var eq = GenerateEquationWithValue(s, rnd, anchor, val);
                        if (eq == null) continue;
                        if (!CanPlace(grid, solutions, startR, startC, vertical, s.EquationLength, eq, nr, nc)) continue;

                        var simulation = SimulatePlacement(grid, solutions, startR, startC, vertical, s.EquationLength, eq, currentBounds);

                        if (simulation.Intersections == 0)
                            continue;

                        if (simulation.NewBounds.Width > maxWidth || simulation.NewBounds.Height > maxHeight)
                            continue;

                        int verticalDepth = Math.Max(Math.Abs(simulation.NewBounds.MinR - centerR), Math.Abs(simulation.NewBounds.MaxR - centerR));
                        int horizontalDepth = Math.Max(Math.Abs(simulation.NewBounds.MinC - centerC), Math.Abs(simulation.NewBounds.MaxC - centerC));
                        if (verticalDepth > maxDepth || horizontalDepth > maxDepth)
                            continue;

                        int width = simulation.NewBounds.Width;
                        int height = simulation.NewBounds.Height;
                        int newArea = simulation.NewBounds.Area;
                        double centerDistance = Math.Abs(nr - centerR) + Math.Abs(nc - centerC);

                        double score =
                            IntersectionBonus * simulation.Intersections
                            - AreaGrowthPenalty * (newArea - oldArea)
                            - AspectRatioPenalty * Math.Abs(width - height)
                            - CenterDistancePenalty * centerDistance;

                        if (best == null || score > best.Score)
                        {
                            best = new PlacementCandidate(startR, startC, vertical, eq, simulation.NewBounds, score);
                        }
                    }
                }
            }
        }

        return best;
    }

    private PlacementSimulation SimulatePlacement(
        CellType[,] grid,
        string[,] solutions,
        int startR,
        int startC,
        bool vertical,
        int len,
        EquationData eq,
        Bounds current)
    {
        int dr = vertical ? 1 : 0;
        int dc = vertical ? 0 : 1;
        var cells = BuildCells(eq, len);

        int minR = current.MinR;
        int maxR = current.MaxR;
        int minC = current.MinC;
        int maxC = current.MaxC;
        int intersections = 0;

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;

            if (grid[r, c] == CellType.Empty)
            {
                minR = Math.Min(minR, r);
                maxR = Math.Max(maxR, r);
                minC = Math.Min(minC, c);
                maxC = Math.Max(maxC, c);
            }
            else if (grid[r, c] == cells[i].type && solutions[r, c] == cells[i].val)
            {
                intersections++;
            }
        }

        return new PlacementSimulation(new Bounds(minR, maxR, minC, maxC), intersections);
    }

    private static Bounds ComputeBounds(CellType[,] grid)
    {
        int minR = GridSize;
        int maxR = -1;
        int minC = GridSize;
        int maxC = -1;

        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                if (grid[r, c] != CellType.Empty)
                {
                    minR = Math.Min(minR, r);
                    maxR = Math.Max(maxR, r);
                    minC = Math.Min(minC, c);
                    maxC = Math.Max(maxC, c);
                }

        if (maxR < 0)
            return new Bounds(0, 0, 0, 0);

        return new Bounds(minR, maxR, minC, maxC);
    }

    private bool HasEquationInDirection(CellType[,] grid, int r, int c, bool horizontal)
    {
        return horizontal
            ? (c > 0 && grid[r, c - 1] != CellType.Empty) || (c < GridSize - 1 && grid[r, c + 1] != CellType.Empty)
            : (r > 0 && grid[r - 1, c] != CellType.Empty) || (r < GridSize - 1 && grid[r + 1, c] != CellType.Empty);
    }

    private List<(int r, int c, decimal val)> GetNumberPositions(CellType[,] grid, string[,] solutions)
    {
        var list = new List<(int, int, decimal)>();
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                if (grid[r, c] == CellType.Number &&
                    decimal.TryParse(solutions[r, c], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    list.Add((r, c, v));

        return list;
    }

    private EquationData? GenerateEquation(Settings s, Random rnd)
    {
        for (int i = 0; i < 100; i++)
        {
            var eq = s.IsExtended ? GenExtended(s, rnd) : GenSimple(s, rnd);
            if (eq != null) return eq;
        }

        return null;
    }

    private EquationData? GenSimple(Settings s, Random rnd)
    {
        string op = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 30; i++)
        {
            int a, b;
            if (op == "×")
            {
                a = rnd.Next(2, 12);
                b = rnd.Next(2, 12);
            }
            else
            {
                a = rnd.Next(Math.Max(s.MinVal, -50), Math.Min(50, s.MaxVal) + 1);
                b = rnd.Next(Math.Max(s.MinVal, -50), Math.Min(50, s.MaxVal) + 1);
            }

            int? c = Calc(a, op, b);
            if (c == null || c.Value < s.MinVal || c.Value > s.MaxVal) continue;
            return new EquationData(new decimal[] { a, b, c.Value }, new[] { op });
        }

        return null;
    }

    private EquationData? GenExtended(Settings s, Random rnd)
    {
        string op1 = s.Ops[rnd.Next(s.Ops.Length)];
        string op2 = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 50; i++)
        {
            decimal a = GenValue(s, rnd);
            decimal b = GenValue(s, rnd);
            decimal c = GenValue(s, rnd);

            if (op1 is "×" or "÷") { a = rnd.Next(2, 12); b = rnd.Next(2, 12); }
            if (op2 is "×" or "÷") { c = rnd.Next(2, 12); }

            decimal? d = Evaluate(a, op1, b, op2, c, s.AllowDecimals);
            if (d == null) continue;
            if (d.Value < s.MinVal || d.Value > s.MaxVal) continue;
            if (!s.AllowDecimals && d.Value != Math.Truncate(d.Value)) continue;

            return new EquationData(new[] { a, b, c, d.Value }, new[] { op1, op2 });
        }

        return null;
    }

    private EquationData? GenerateEquationWithValue(Settings s, Random rnd, int anchorPos, decimal anchorVal)
    {
        int numIdx = anchorPos / 2;

        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (s.IsExtended)
            {
                string op1 = s.Ops[rnd.Next(s.Ops.Length)];
                string op2 = s.Ops[rnd.Next(s.Ops.Length)];

                decimal a = numIdx == 0 ? anchorVal : GenValue(s, rnd);
                decimal b = numIdx == 1 ? anchorVal : GenValue(s, rnd);
                decimal c = numIdx == 2 ? anchorVal : GenValue(s, rnd);

                if (op1 is "×" or "÷" && numIdx > 1) { a = rnd.Next(2, 12); b = rnd.Next(2, 12); }
                if (op2 is "×" or "÷" && numIdx < 1) { b = rnd.Next(2, 12); c = rnd.Next(2, 12); }

                if (numIdx == 0) a = anchorVal;
                if (numIdx == 1) b = anchorVal;
                if (numIdx == 2) c = anchorVal;

                decimal? d = Evaluate(a, op1, b, op2, c, s.AllowDecimals);
                if (d == null) continue;

                if (numIdx == 3 && d.Value != anchorVal) continue;
                if (numIdx != 3 && (d.Value < s.MinVal || d.Value > s.MaxVal)) continue;
                if (!s.AllowDecimals && d.Value != Math.Truncate(d.Value)) continue;

                return new EquationData(new[] { a, b, c, numIdx == 3 ? anchorVal : d.Value }, new[] { op1, op2 });
            }
            else
            {
                string op = s.Ops[rnd.Next(s.Ops.Length)];
                decimal a, b, c;

                if (numIdx == 0)
                {
                    a = anchorVal;
                    b = op == "×" ? rnd.Next(2, 12) : GenValue(s, rnd);
                    var res = Calc((int)a, op, (int)b);
                    if (res == null || res.Value < s.MinVal || res.Value > s.MaxVal) continue;
                    c = res.Value;
                }
                else if (numIdx == 1)
                {
                    b = anchorVal;
                    a = op == "×" ? rnd.Next(2, 12) : GenValue(s, rnd);
                    var res = Calc((int)a, op, (int)b);
                    if (res == null || res.Value < s.MinVal || res.Value > s.MaxVal) continue;
                    c = res.Value;
                }
                else
                {
                    c = anchorVal;
                    var rev = Reverse((int)c, op, s, rnd);
                    if (rev == null) continue;
                    a = rev.Value.a;
                    b = rev.Value.b;
                }

                return new EquationData(new[] { a, b, c }, new[] { op });
            }
        }

        return null;
    }

    private decimal GenValue(Settings s, Random rnd)
    {
        int min = Math.Max(-50, s.MinVal);
        int max = Math.Min(50, s.MaxVal);
        if (min > max) (min, max) = (max, min);

        if (s.AllowDecimals && rnd.Next(10) < 3)
        {
            int whole = rnd.Next(min, max + 1);
            return whole + rnd.Next(1, 10) / 10m;
        }

        return rnd.Next(min, max + 1);
    }

    private static int? Calc(int a, string op, int b)
    {
        return op switch
        {
            "+" => a + b,
            "-" => a - b,
            "×" => a * b,
            "÷" when b != 0 && a % b == 0 => a / b,
            _ => null
        };
    }

    private static decimal? CalcDec(decimal a, string op, decimal b, bool allowDecimalDivision)
    {
        return op switch
        {
            "+" => a + b,
            "-" => a - b,
            "×" => a * b,
            "÷" when b != 0 => DivideWithRule(a, b, allowDecimalDivision),
            _ => null
        };
    }

    private static decimal? DivideWithRule(decimal a, decimal b, bool allowDecimalDivision)
    {
        var q = a / b;
        if (!allowDecimalDivision)
            return q == Math.Truncate(q) ? q : null;

        return HasAtMostOneDecimal(q) ? q : null;
    }

    private static bool HasAtMostOneDecimal(decimal value)
    {
        return value == decimal.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal? Evaluate(decimal a, string op1, decimal b, string op2, decimal c, bool allowDecimalDivision)
    {
        static int Priority(string op) => op is "×" or "÷" ? 2 : 1;

        if (Priority(op1) >= Priority(op2))
        {
            var t1 = CalcDec(a, op1, b, allowDecimalDivision);
            if (t1 == null) return null;
            return CalcDec(t1.Value, op2, c, allowDecimalDivision);
        }

        var t2 = CalcDec(b, op2, c, allowDecimalDivision);
        if (t2 == null) return null;
        return CalcDec(a, op1, t2.Value, allowDecimalDivision);
    }

    private (int a, int b)? Reverse(int c, string op, Settings s, Random rnd)
    {
        for (int i = 0; i < 40; i++)
        {
            int a, b;
            switch (op)
            {
                case "+":
                {
                    int minA = Math.Max(s.MinVal, c - s.MaxVal);
                    int maxA = Math.Min(s.MaxVal, c - s.MinVal);
                    if (maxA < minA) break;
                    a = rnd.Next(minA, maxA + 1);
                    b = c - a;
                    if (b >= s.MinVal && b <= s.MaxVal) return (a, b);
                    break;
                }
                case "-":
                {
                    int minB = s.MinVal;
                    int maxB = s.MaxVal;
                    if (maxB < minB) break;
                    b = rnd.Next(minB, maxB + 1);
                    a = c + b;
                    if (a >= s.MinVal && a <= s.MaxVal) return (a, b);
                    break;
                }
                case "×":
                {
                    var candidates = new List<(int a, int b)>();
                    if (c == 0)
                    {
                        for (int tryA = s.MinVal; tryA <= s.MaxVal; tryA++)
                        {
                            if (tryA == 0) continue;
                            if (0 >= s.MinVal && 0 <= s.MaxVal)
                                candidates.Add((tryA, 0));
                        }
                    }
                    else
                    {
                        for (int tryA = s.MinVal; tryA <= s.MaxVal; tryA++)
                        {
                            if (tryA == 0 || c % tryA != 0) continue;
                            int tryB = c / tryA;
                            if (tryB < s.MinVal || tryB > s.MaxVal) continue;
                            candidates.Add((tryA, tryB));
                        }
                    }

                    if (candidates.Count == 0) break;
                    return candidates[rnd.Next(candidates.Count)];
                }
                case "÷":
                {
                    var divisors = Enumerable.Range(2, 10)
                        .Select(d => rnd.Next(2) == 0 ? d : -d)
                        .Where(d => d >= s.MinVal && d <= s.MaxVal)
                        .Distinct()
                        .ToList();
                    if (divisors.Count == 0) break;
                    b = divisors[rnd.Next(divisors.Count)];
                    a = c * b;
                    if (a >= s.MinVal && a <= s.MaxVal) return (a, b);
                    break;
                }
            }
        }

        return null;
    }

    private bool CanPlace(CellType[,] grid, string[,] sols, int startR, int startC,
        bool vertical, int len, EquationData eq, int crossR, int crossC)
    {
        int dr = vertical ? 1 : 0;
        int dc = vertical ? 0 : 1;

        if (startR < 1 || startC < 1) return false;
        if (startR + (vertical ? len : 0) >= GridSize - 1) return false;
        if (startC + (vertical ? 0 : len) >= GridSize - 1) return false;

        if (grid[startR - dr, startC - dc] != CellType.Empty) return false;
        if (grid[startR + len * dr, startC + len * dc] != CellType.Empty) return false;

        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            var (type, val) = cells[i];

            if (grid[r, c] == CellType.Empty)
            {
                if (r == crossR && c == crossC) continue;

                int pdr = vertical ? 0 : 1;
                int pdc = vertical ? 1 : 0;
                if (grid[r + pdr, c + pdc] != CellType.Empty) return false;
                if (grid[r - pdr, c - pdc] != CellType.Empty) return false;
            }
            else
            {
                if (grid[r, c] != type) return false;
                if (sols[r, c] != val) return false;
            }
        }

        return true;
    }

    private void Place(CellType[,] grid, string[,] sols, int startR, int startC, bool horizontal, EquationData eq, int len)
    {
        int dr = horizontal ? 0 : 1;
        int dc = horizontal ? 1 : 0;
        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            grid[r, c] = cells[i].type;
            sols[r, c] = cells[i].val;
        }
    }

    private List<CellChange> PlaceWithChanges(CellType[,] grid, string[,] sols, int startR, int startC, bool horizontal, EquationData eq, int len)
    {
        int dr = horizontal ? 0 : 1;
        int dc = horizontal ? 1 : 0;
        var cells = BuildCells(eq, len);
        var changes = new List<CellChange>(len / 2 + 1);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            if (grid[r, c] == CellType.Empty)
                changes.Add(new CellChange(r, c));

            grid[r, c] = cells[i].type;
            sols[r, c] = cells[i].val;
        }

        return changes;
    }

    private static void UndoChanges(CellType[,] grid, string[,] sols, List<CellChange> changes)
    {
        for (int i = changes.Count - 1; i >= 0; i--)
        {
            var c = changes[i];
            grid[c.Row, c.Col] = CellType.Empty;
            sols[c.Row, c.Col] = "";
        }
    }

    private List<(CellType type, string val)> BuildCells(EquationData eq, int len)
    {
        var cells = new List<(CellType, string)>();
        int numIdx = 0;
        int opIdx = 0;

        for (int i = 0; i < len; i++)
        {
            if (i == len - 2)
                cells.Add((CellType.Equals, "="));
            else if (i % 2 == 0)
                cells.Add((CellType.Number, Format(eq.Numbers[numIdx++])));
            else
                cells.Add((CellType.Operator, eq.Operators[opIdx++]));
        }

        return cells;
    }

    private MathCrossGame BuildGame(CellType[,] grid, string[,] sols, Settings s)
    {
        int minR = GridSize, maxR = 0, minC = GridSize, maxC = 0;

        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                if (grid[r, c] != CellType.Empty)
                {
                    minR = Math.Min(minR, r);
                    maxR = Math.Max(maxR, r);
                    minC = Math.Min(minC, c);
                    maxC = Math.Max(maxC, c);
                }

        int rows = maxR - minR + 1;
        int cols = maxC - minC + 1;

        var game = new MathCrossGame
        {
            Rows = rows,
            Cols = cols,
            Grid = new MathCrossCell[rows, cols],
            Difficulty = s.DifficultyKey,
            EquationLength = s.EquationLength,
            UseExtendedEquations = s.IsExtended
        };

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                game.Grid[r, c] = new MathCrossCell
                {
                    Row = r,
                    Col = c,
                    Type = grid[minR + r, minC + c],
                    Solution = sols[minR + r, minC + c],
                    UserInput = "",
                    IsGiven = false
                };

        game.Equations = ScanEquations(game, s.EquationLength);
        return game;
    }

    private MathCrossGame GenerateFallbackGrid(Settings s, Random rnd)
    {
        MathCrossGame? first = null;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var candidate = TryBuildEmergencyTemplate(s, new Random(rnd.Next() + attempt * 101));
            first ??= candidate;

            if (candidate.Equations.Count >= s.MinEquations
                && candidate.Equations.Count <= s.MaxEquations
                && HasValidTopology(candidate))
                return candidate;
        }

        return first ?? TryBuildEmergencyTemplate(s, rnd);
    }

    private MathCrossGame TryBuildEmergencyTemplate(Settings s, Random rnd)
    {
        int rows = s.IsExtended ? ExtendedMaxLayoutSize : DefaultMaxLayoutSize;
        int cols = rows;
        var game = new MathCrossGame
        {
            Rows = rows,
            Cols = cols,
            Grid = new MathCrossCell[rows, cols],
            Difficulty = s.DifficultyKey,
            EquationLength = s.EquationLength,
            UseExtendedEquations = s.IsExtended
        };

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                game.Grid[r, c] = new MathCrossCell { Row = r, Col = c, Type = CellType.Empty, Solution = "", UserInput = "", IsGiven = false };

        int[] lineStarts = GetFallbackLineStarts(game.EquationLength, rows);

        // 4 horizontal equations
        foreach (int hr in lineStarts)
        {
            var eq = GenerateEquation(s, rnd);
            if (eq != null) PlaceInGame(game, hr, 0, horizontal: true, eq, game.EquationLength);
        }

        // 4 vertical equations
        foreach (int vc in lineStarts)
        {
            var eq = GenerateEquation(s, rnd);
            if (eq != null) PlaceInGame(game, 0, vc, horizontal: false, eq, game.EquationLength);
        }

        game.Equations = ScanEquations(game, game.EquationLength);
        return game;
    }

    private static int[] GetFallbackLineStarts(int len, int size)
    {
        int maxStart = Math.Max(0, size - len);
        var starts = new[] { 0, maxStart / 3, (2 * maxStart) / 3, maxStart }
            .Distinct()
            .ToList();

        while (starts.Count < 4)
        {
            starts.Add(Math.Max(0, starts[^1] - 1));
            starts = starts.Distinct().ToList();
            if (starts.Count == Math.Min(4, maxStart + 1)) break;
        }

        return starts.OrderBy(v => v).Take(4).ToArray();
    }

    private void PlaceInGame(MathCrossGame game, int startR, int startC, bool horizontal, EquationData eq, int len)
    {
        int dr = horizontal ? 0 : 1;
        int dc = horizontal ? 1 : 0;
        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            if (r < 0 || r >= game.Rows || c < 0 || c >= game.Cols) continue;

            if (game.Grid[r, c].Type == CellType.Empty ||
                (game.Grid[r, c].Type == cells[i].type && game.Grid[r, c].Solution == cells[i].val))
            {
                game.Grid[r, c].Type = cells[i].type;
                game.Grid[r, c].Solution = cells[i].val;
            }
        }
    }

    private List<MathEquation> ScanEquations(MathCrossGame game, int len)
    {
        var list = new List<MathEquation>();

        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c <= game.Cols - len; c++)
            {
                if (!IsValidEq(game, r, c, 0, 1, len)) continue;
                list.Add(new MathEquation
                {
                    StartRow = r,
                    StartCol = c,
                    IsHorizontal = true,
                    Cells = Enumerable.Range(0, len).Select(i => (r, c + i)).ToList(),
                    Operator = game.Grid[r, c + 1].Solution,
                    Operator2 = len >= 7 ? game.Grid[r, c + 3].Solution : "",
                    Operator3 = len >= 9 ? game.Grid[r, c + 5].Solution : ""
                });
            }
        }

        for (int r = 0; r <= game.Rows - len; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (!IsValidEq(game, r, c, 1, 0, len)) continue;
                list.Add(new MathEquation
                {
                    StartRow = r,
                    StartCol = c,
                    IsHorizontal = false,
                    Cells = Enumerable.Range(0, len).Select(i => (r + i, c)).ToList(),
                    Operator = game.Grid[r + 1, c].Solution,
                    Operator2 = len >= 7 ? game.Grid[r + 3, c].Solution : "",
                    Operator3 = len >= 9 ? game.Grid[r + 5, c].Solution : ""
                });
            }
        }

        return list;
    }

    private bool IsValidEq(MathCrossGame g, int r, int c, int dr, int dc, int len)
    {
        for (int i = 0; i < len; i++)
        {
            var type = g.Grid[r + i * dr, c + i * dc].Type;
            if (i == len - 2) { if (type != CellType.Equals) return false; }
            else if (i % 2 == 0) { if (type != CellType.Number) return false; }
            else { if (type != CellType.Operator) return false; }
        }
        return true;
    }

    private void FinalizeGame(MathCrossGame game, Random rnd, Settings s)
    {
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
            {
                var cell = game.Grid[r, c];
                if (cell.Type == CellType.Equals)
                {
                    cell.IsGiven = true;
                    cell.UserInput = "=";
                }
                else if (cell.Type == CellType.Empty)
                {
                    cell.IsGiven = true;
                    cell.UserInput = "";
                }
                else
                {
                    cell.IsGiven = false;
                    cell.UserInput = "";
                }
            }

        var editable = new List<MathCrossCell>();
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type is CellType.Number or CellType.Operator)
                    editable.Add(game.Grid[r, c]);

        int toGive = (int)(editable.Count * s.GivenPercent);
        toGive = Math.Max(4, Math.Min(toGive, Math.Max(0, editable.Count - 2)));

        foreach (var cell in ChooseInitialGivens(game, editable, toGive, rnd))
        {
            cell.IsGiven = true;
            cell.UserInput = cell.Solution;
        }

        EnsureSolvable(game, rnd);
        HideCellsInFullyGivenEquations(game, rnd);

        if (editable.Count > 0 && editable.All(c => c.IsGiven))
        {
            var hide = editable[rnd.Next(editable.Count)];
            hide.IsGiven = false;
            hide.UserInput = "";
        }

        game.GivenCells = editable.Count(c => c.IsGiven);
    }

    private IEnumerable<MathCrossCell> ChooseInitialGivens(
        MathCrossGame game,
        List<MathCrossCell> editable,
        int targetCount,
        Random rnd)
    {
        var picked = new HashSet<MathCrossCell>();

        foreach (var eq in game.Equations.OrderBy(_ => rnd.Next()))
        {
            var candidates = eq.Cells
                .Where(p => IsWithinBounds(game, p.row, p.col))
                .Select(p => game.Grid[p.row, p.col])
                .Where(c => c.Type is CellType.Number or CellType.Operator)
                .ToList();

            if (candidates.Count == 0) continue;
            picked.Add(candidates[rnd.Next(candidates.Count)]);
        }

        var remaining = editable.Where(c => !picked.Contains(c)).OrderBy(_ => rnd.Next()).ToList();
        foreach (var cell in remaining)
        {
            if (picked.Count >= targetCount) break;
            picked.Add(cell);
        }

        if (picked.Count == 0 && editable.Count > 0)
            picked.Add(editable[rnd.Next(editable.Count)]);

        return picked;
    }

    private void HideCellsInFullyGivenEquations(MathCrossGame game, Random rnd)
    {
        foreach (var eq in game.Equations.OrderBy(_ => rnd.Next()))
        {
            var cells = eq.Cells
                .Where(p => IsWithinBounds(game, p.row, p.col))
                .Select(p => game.Grid[p.row, p.col])
                .Where(c => c.Type is CellType.Number or CellType.Operator)
                .ToList();

            if (cells.Count == 0 || cells.Any(c => !c.IsGiven))
                continue;

            var hide = cells[rnd.Next(cells.Count)];
            hide.IsGiven = false;
            hide.UserInput = "";
        }
    }

    private void EnsureSolvable(MathCrossGame game, Random rnd)
    {
        for (int iter = 0; iter < 20; iter++)
        {
            var solved = new bool[game.Rows, game.Cols];

            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                    if (game.Grid[r, c].IsGiven || game.Grid[r, c].Type is CellType.Empty or CellType.Equals)
                        solved[r, c] = true;

            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var eq in game.Equations)
                {
                    var valid = eq.Cells.Where(p => IsWithinBounds(game, p.row, p.col)).ToList();
                    if (valid.Count == 0) continue;

                    int unknowns = valid.Count(p => !solved[p.row, p.col]);
                    if (unknowns != 1) continue;

                    foreach (var (er, ec) in valid)
                    {
                        if (!solved[er, ec])
                        {
                            solved[er, ec] = true;
                            progress = true;
                        }
                    }
                }
            }

            var unsolved = new List<MathCrossCell>();
            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                    if (game.Grid[r, c].Type is CellType.Number or CellType.Operator && !solved[r, c])
                        unsolved.Add(game.Grid[r, c]);

            if (unsolved.Count == 0) return;

            var pick = unsolved[rnd.Next(unsolved.Count)];
            pick.IsGiven = true;
            pick.UserInput = pick.Solution;
        }
    }

    private bool IsPlayable(MathCrossGame game)
    {
        if (game.Equations.Count < 8 || game.Equations.Count > 12) return false;

        int editable = 0;
        int hidden = 0;
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type is CellType.Number or CellType.Operator)
                {
                    editable++;
                    if (!game.Grid[r, c].IsGiven) hidden++;
                }

        return editable > 0 && hidden > 0;
    }

    private static bool HasValidTopology(MathCrossGame game)
    {
        int n = game.Equations.Count;
        if (n < 8 || n > 12) return false;

        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        int intersections = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (game.Equations[i].Cells.Intersect(game.Equations[j].Cells).Any())
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                    intersections++;
                }

        if (adj.Any(a => a.Count == 0)) return false;

        bool hasBranch = adj.Any(a => a.Count >= 3);
        if (!hasBranch && intersections < n - 1 && adj.Max(a => a.Count) <= 2)
            return false;

        return true;
    }

    private bool IsValidTopology(CellType[,] grid, int eqLen, int minEquations, int maxEquations, out int actualEquationCount)
    {
        actualEquationCount = 0;

        var temp = new MathCrossGame
        {
            Rows = GridSize,
            Cols = GridSize,
            Grid = new MathCrossCell[GridSize, GridSize]
        };

        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                temp.Grid[r, c] = new MathCrossCell { Type = grid[r, c] };

        var equations = ScanEquations(temp, eqLen);
        actualEquationCount = equations.Count;
        if (actualEquationCount < minEquations || actualEquationCount > maxEquations) return false;

        int n = equations.Count;
        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        int intersections = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (equations[i].Cells.Intersect(equations[j].Cells).Any())
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                    intersections++;
                }

        if (adj.Any(a => a.Count == 0)) return false;

        bool hasBranch = adj.Any(a => a.Count >= 3);
        if (!hasBranch && intersections < n - 1 && adj.Max(a => a.Count) <= 2)
            return false;

        return true;
    }

    private static bool IsWithinBounds(MathCrossGame game, int row, int col)
    {
        return row >= 0 && row < game.Rows && col >= 0 && col < game.Cols;
    }

    private static string Format(decimal v)
    {
        if (v == Math.Truncate(v)) return ((int)v).ToString();
        return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private record EquationData(decimal[] Numbers, string[] Operators);
    private record EquationPlacement(int StartR, int StartC, bool Horizontal, EquationData Eq);
    private record Bounds(int MinR, int MaxR, int MinC, int MaxC)
    {
        public int Width => MaxC - MinC + 1;
        public int Height => MaxR - MinR + 1;
        public int Area => Width * Height;
    }
    private record PlacementSimulation(Bounds NewBounds, int Intersections);
    private record PlacementCandidate(int StartR, int StartC, bool Vertical, EquationData Equation, Bounds NewBounds, double Score);
    private record CellChange(int Row, int Col);

    private record Settings(
        string DifficultyKey,
        int MinVal,
        int MaxVal,
        bool AllowDecimals,
        string[] Ops,
        int MinEquations,
        int MaxEquations,
        double GivenPercent,
        int EquationLength,
        bool IsExtended);

    private static Settings GetSettings(string key)
    {
        key = (key ?? "easy").Trim().ToLowerInvariant();

        return key switch
        {
            "easy" => new Settings("easy", 1, 99, false,
                new[] { "+", "-" }, 8, 12, 0.45, 5, false),

            "normal" => new Settings("normal", 1, 99, false,
                new[] { "+", "-", "×" }, 8, 12, 0.40, 5, false),

            "hard" => new Settings("hard", -1, 99, false,
                new[] { "+", "-", "×", "÷" }, 8, 12, 0.35, 7, true),

            "master" => new Settings("master", -1, 99, true,
                new[] { "+", "-", "×", "÷" }, 8, 12, 0.30, 7, true),

            _ => GetSettings("easy")
        };
    }
}
