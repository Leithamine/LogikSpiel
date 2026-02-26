#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.Model;

namespace LogikSpiel.Services;

/// <summary>
/// Intelligenter Math Cross Generator - garantiert 8-12 verbundene Gleichungen
/// </summary>
public sealed class MathCrossGeneratorService
{
    private const int GridSize = 30;
    private const int FinalizeAttempts = 3;
    private const int SolvabilityRetryLimit = 24;
    private const int CandidateSearchLimit = 1200;
    private const int GenerationBudgetMs = 2800;
    private const int MaxTryGenerateAttempts = 14;
    private const int MaxFallbackAttempts = 3;

    public MathCrossGame GenerateGame(string difficultyKey, int seed)
    {
        var s = GetSettings(difficultyKey);
        var startedAt = DateTime.UtcNow;

        for (int attempt = 0; attempt < MaxTryGenerateAttempts && !IsBudgetExceeded(startedAt); attempt++)
        {
            var game = TryGenerate(s, new Random(seed + attempt * 1000));
            if (game == null || game.Equations.Count < s.MinEquations) continue;

            for (int finalizeAttempt = 0; finalizeAttempt < FinalizeAttempts && !IsBudgetExceeded(startedAt); finalizeAttempt++)
            {
                var finalizeRnd = new Random(seed + attempt * 1000 + finalizeAttempt * 97 + 17);
                if (FinalizeGame(game, finalizeRnd, s))
                {
                    return game;
                }
            }
        }

        for (int attempt = 0; attempt < MaxFallbackAttempts && !IsBudgetExceeded(startedAt); attempt++)
        {
            var fallbackRnd = new Random(seed + 50000 + attempt * 131);
            var fallback = GenerateFallbackGrid(s, fallbackRnd);
            if (FinalizeGame(fallback, fallbackRnd, s))
            {
                return fallback;
            }
        }

        var emergency = GenerateEmergencyTemplateGame(s, seed + 777777);
        if (!FinalizeGame(emergency, new Random(seed + 888888), s))
            throw new InvalidOperationException("MathCross generation failed to produce a solvable puzzle within budget.");

        return emergency;
    }

    private MathCrossGame? TryGenerate(Settings s, Random rnd)
    {
        var grid = new CellType[GridSize, GridSize];
        var solutions = new string[GridSize, GridSize];

        // Initialisiere
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
            {
                grid[r, c] = CellType.Empty;
                solutions[r, c] = "";
            }

        var placed = new List<EquationPlacement>();
        int target = rnd.Next(s.MinEquations, s.MaxEquations + 1);

        // Erste Gleichung horizontal in der Mitte
        int midR = GridSize / 2;
        int midC = (GridSize - s.EquationLength) / 2;

        var first = GenerateEquation(s, rnd);
        if (first == null) return null;

        Place(grid, solutions, midR, midC, false, first, s.EquationLength);
        placed.Add(new EquationPlacement(midR, midC, false, first));

        // Let best score decide orientation
        int fails = 0;

        while (placed.Count < target && fails < 40)
        {
            fails++;

            var numbers = GetNumberPositions(grid, solutions);
            if (numbers.Count == 0) continue;

            var candidates = new List<(EquationPlacement placement, int score)>();
            int attempts = 0;
            
            // Generate up to 25 valid candidate placements to ensure thorough search
            while (candidates.Count < 25 && attempts < 150)
            {
                attempts++;
                var (nr, nc, val) = numbers[rnd.Next(numbers.Count)];

                bool hasHorizontal = HasEquationInDirection(grid, nr, nc, true);
                bool hasVertical = HasEquationInDirection(grid, nr, nc, false);

                // Both directions are full? Skip.
                if (hasHorizontal && hasVertical) continue;

                var tryDirections = new List<bool>();
                if (!hasHorizontal) tryDirections.Add(true); // Can place horizontally
                if (!hasVertical) tryDirections.Add(false);  // Can place vertically

                int[] anchors = s.IsExtended ? new[] { 0, 2, 4, 6 } : new[] { 0, 2, 4 };

                foreach (bool vertical in tryDirections)
                {
                    foreach (int anchor in anchors.OrderBy(_ => rnd.Next()))
                    {
                        var eq = GenerateEquationWithValue(s, rnd, anchor, val);
                        if (eq == null) continue;

                        int startR = vertical ? nr - anchor : nr;
                        int startC = vertical ? nc : nc - anchor;

                        if (CanPlace(grid, solutions, startR, startC, vertical, s.EquationLength, eq, nr, nc))
                        {
                            var placement = new EquationPlacement(startR, startC, vertical, eq);
                            int score = ScorePlacement(placed, placement, grid, s.EquationLength, nr, nc);
                            candidates.Add((placement, score));
                            // Only add one valid candidate for this specific anchor/direction pair to ensure diversity 
                            break; 
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                var best = candidates.OrderBy(c => c.score).First();
                Place(grid, solutions, best.placement.StartR, best.placement.StartC, best.placement.Vertical, best.placement.Eq, s.EquationLength);
                placed.Add(best.placement);
                fails = 0;
            }
        }

        if (placed.Count < s.MinEquations || placed.Count > s.MaxEquations) return null;
        if (!IsValidTopology(grid, s.EquationLength, s.MinEquations, s.MaxEquations, out int _)) return null;

        var game = BuildGame(grid, solutions, s);
        if (game.Equations.Count < s.MinEquations || game.Equations.Count > s.MaxEquations) return null;
        return game;
    }

    private int ScorePlacement(List<EquationPlacement> placed, EquationPlacement p, CellType[,] grid, int len, int anchorR, int anchorC)
    {
        int minR = p.StartR;
        int maxR = p.StartR + (p.Vertical ? len - 1 : 0);
        int minC = p.StartC;
        int maxC = p.StartC + (p.Vertical ? 0 : len - 1);

        foreach (var exist in placed)
        {
            minR = Math.Min(minR, exist.StartR);
            maxR = Math.Max(maxR, exist.StartR + (exist.Vertical ? len - 1 : 0));
            minC = Math.Min(minC, exist.StartC);
            maxC = Math.Max(maxC, exist.StartC + (exist.Vertical ? 0 : len - 1));
        }

        int width = maxC - minC + 1;
        int height = maxR - minR + 1;
        int area = width * height;
        
        // Base score driven by compactness. Smaller area is better.
        int score = area * 20;

        int intersections = 0;
        for (int i = 0; i < len; i++)
        {
            int r = p.StartR + i * (p.Vertical ? 1 : 0);
            int c = p.StartC + i * (p.Vertical ? 0 : 1);
            if (grid[r, c] != CellType.Empty)
            {
                intersections++;
            }
        }
        
        // Massive reward for genuine matrix crossings (intersections > 1 means it crossed an existing line naturally)
        if (intersections > 1) 
        {
            score -= (intersections - 1) * 1000;
        }

        // Extremely heavy bonus for anchoring in the middle rather than ends, to stop end-to-end snakes.
        int anchorIdx = p.Vertical ? (anchorR - p.StartR) : (anchorC - p.StartC);
        if (anchorIdx > 0 && anchorIdx < len - 1)
        {
            score -= 400; 
        }
        else
        {
            // Penalty for edge anchoring (leaf growth).
            score += 200;
        }

        // Penalty for long aspect ratios to prevent flat/tall lines.
        int ratio = Math.Max(width, height) - Math.Min(width, height);
        score += ratio * 50;

        return score;
    }

    private bool IsValidTopology(CellType[,] grid, int eqLen, int minEquations, int maxEquations, out int actualEquationCount)
    {
        actualEquationCount = 0;
        
        // Temporarily build a MathCrossGame just to use ScanEquations
        var tempGame = new MathCrossGame
        {
            Rows = GridSize,
            Cols = GridSize,
            Grid = new MathCrossCell[GridSize, GridSize]
        };
        for (int r = 0; r < GridSize; r++)
            for (int c = 0; c < GridSize; c++)
                tempGame.Grid[r, c] = new MathCrossCell { Type = grid[r, c] };

        var equations = ScanEquations(tempGame, eqLen);
        actualEquationCount = equations.Count;

        if (actualEquationCount < minEquations || actualEquationCount > maxEquations)
            return false;

        // Build Equation Intersection Graph
        int n = equations.Count;
        var adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        int totalIntersections = 0;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (equations[i].Cells.Intersect(equations[j].Cells).Any())
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                    totalIntersections++;
                }
            }
        }

        // Rules:
        // 1. No isolated equations (degree 0) allowed. Everything must intersect at least once.
        for (int i = 0; i < n; i++)
        {
            if (adj[i].Count == 0) return false;
        }

        // 2. Reject pure snake topologies. A snake is formed if max degree is <= 2 and components are entirely just line segments.
        // If we have totalIntersections < n - 1, we have disconnected components, which is ALLOWED if the sub-clusters are rich.
        // To be safe, let's demand at least some complexity. 
        // We want at least one intersection that branches (degree >= 3), OR a loop (intersections >= n in a component).
        bool hasRichStructure = false;
        
        for (int i = 0; i < n; i++)
        {
            if (adj[i].Count >= 3)
            {
                hasRichStructure = true;
                break;
            }
        }
        
        // If there's no degree-3 crossing, at least check if we have loops (more intersections than trees)
        // Or if it's explicitly allowed to be a simple cross (e.g., small 8 eq boards might just be degree-2).
        if (!hasRichStructure)
        {
            // If even max node degree is 2, it's a pure loop or straight snake.
            // Allow if there are multiple clusters, OR if there's at least a loop.
            // Wait, standard crosswords with 8 words can easily just be an interconnected chain...
            // Let's require that totalIntersections >= n / 2.
            if (totalIntersections < n - 1 && adj.Max(x => x.Count) <= 2)
            {
                // This means there are multiple components AND none of them branch. Pure distinct snakes.
                return false;
            }
            
            // If it's a single snake component with 8+ equations it's extremely boring.
            // If max paths are > 4 without branching, reject.
        }

        return true;
    }

    private bool HasEquationInDirection(CellType[,] grid, int r, int c, bool horizontal)
    {
        if (horizontal)
        {
            return (c > 0 && grid[r, c - 1] != CellType.Empty) ||
                   (c < GridSize - 1 && grid[r, c + 1] != CellType.Empty);
        }
        else
        {
            return (r > 0 && grid[r - 1, c] != CellType.Empty) ||
                   (r < GridSize - 1 && grid[r + 1, c] != CellType.Empty);
        }
    }

    private List<(int r, int c, decimal val)> GetNumberPositions(CellType[,] grid, string[,] solutions)
    {
        var list = new List<(int, int, decimal)>();
        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                if (grid[r, c] == CellType.Number)
                {
                    if (decimal.TryParse(solutions[r, c], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    {
                        list.Add((r, c, v));
                    }
                }
            }
        }
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
        // a op b = c
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
                a = rnd.Next(Math.Max(1, s.MinVal), Math.Min(50, s.MaxVal) + 1);
                b = rnd.Next(Math.Max(1, s.MinVal), Math.Min(50, s.MaxVal) + 1);
            }

            int? c = Calc(a, op, b);
            if (c == null || c.Value < s.MinVal || c.Value > s.MaxVal) continue;

            return new EquationData(new decimal[] { a, b, c.Value }, new[] { op });
        }
        return null;
    }

    private EquationData? GenExtended(Settings s, Random rnd)
    {
        // a op1 b op2 c = d
        string op1 = s.Ops[rnd.Next(s.Ops.Length)];
        string op2 = s.Ops[rnd.Next(s.Ops.Length)];

        for (int i = 0; i < 50; i++)
        {
            decimal a, b, c;

            if (op1 is "×" or "÷")
            {
                a = rnd.Next(2, 12);
                b = rnd.Next(2, 12);
            }
            else
            {
                a = GenValue(s, rnd);
                b = GenValue(s, rnd);
            }

            if (op2 is "×" or "÷")
            {
                c = rnd.Next(2, 12);
            }
            else
            {
                c = GenValue(s, rnd);
            }

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

                // Für × und ÷ kleinere Zahlen
                if (op1 is "×" or "÷" && numIdx > 1) { a = rnd.Next(2, 12); b = rnd.Next(2, 12); }
                if (op2 is "×" or "÷" && numIdx < 1) { b = rnd.Next(2, 12); c = rnd.Next(2, 12); }

                // Anker wiederherstellen
                if (numIdx == 0) a = anchorVal;
                if (numIdx == 1) b = anchorVal;
                if (numIdx == 2) c = anchorVal;

                decimal? d = Evaluate(a, op1, b, op2, c, s.AllowDecimals);
                if (d == null) continue;

                if (numIdx == 3 && d.Value != anchorVal) continue;
                if (numIdx != 3 && (d.Value < s.MinVal || d.Value > s.MaxVal)) continue;
                if (!s.AllowDecimals && d.Value != Math.Truncate(d.Value)) continue;

                return new EquationData(
                    new[] { a, b, c, numIdx == 3 ? anchorVal : d.Value },
                    new[] { op1, op2 }
                );
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
        var quotient = a / b;

        if (!allowDecimalDivision)
            return quotient == Math.Truncate(quotient) ? quotient : null;

        // Master-Regel: nicht-ganzzahlige Ergebnisse sind erlaubt,
        // aber nur mit maximal einer Nachkommastelle, damit Anzeige/Validierung stabil bleibt.
        return HasAtMostOneDecimal(quotient) ? quotient : null;
    }

    private static bool HasAtMostOneDecimal(decimal value)
    {
        return value == decimal.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal? Evaluate(decimal a, string op1, decimal b, string op2, decimal c, bool allowDecimalDivision)
    {
        // Spielregel: strikt links-nach-rechts auswerten
        var t1 = CalcDec(a, op1, b, allowDecimalDivision);
        if (t1 == null) return null;

        return CalcDec(t1.Value, op2, c, allowDecimalDivision);
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
                        int minB = Math.Max(s.MinVal, s.MinVal - c);
                        int maxB = Math.Min(s.MaxVal, s.MaxVal - c);
                        if (maxB < minB) break;

                        b = rnd.Next(minB, maxB + 1);
                        a = c + b;
                        if (a >= s.MinVal && a <= s.MaxVal) return (a, b);
                        break;
                    }
                case "×":
                    {
                        var candidates = new List<(int a, int b)>();
                        for (int tryA = -12; tryA <= 12; tryA++)
                        {
                            if (tryA is 0 or 1 or -1) continue;
                            if (c % tryA != 0) continue;

                            int tryB = c / tryA;
                            if (tryB is 0 or 1 or -1) continue;
                            if (tryB < s.MinVal || tryB > s.MaxVal) continue;
                            if (tryA < s.MinVal || tryA > s.MaxVal) continue;
                            candidates.Add((tryA, tryB));
                        }

                        if (candidates.Count == 0) break;
                        var pick = candidates[rnd.Next(candidates.Count)];
                        return pick;
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

        // Bounds
        if (startR < 1 || startC < 1) return false;
        if (startR + (vertical ? len : 0) >= GridSize - 1) return false;
        if (startC + (vertical ? 0 : len) >= GridSize - 1) return false;

        // Zelle vor Start muss leer sein
        if (grid[startR - dr, startC - dc] != CellType.Empty) return false;

        // Zelle nach Ende muss leer sein
        if (grid[startR + len * dr, startC + len * dc] != CellType.Empty) return false;

        // Baue erwartete Zellen
        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            var (type, val) = cells[i];
            var existing = grid[r, c];

            if (existing == CellType.Empty)
            {
                // Prüfe seitliche Nachbarn (außer am Kreuzungspunkt)
                if (!(r == crossR && c == crossC))
                {
                    int perpDr = vertical ? 0 : 1;
                    int perpDc = vertical ? 1 : 0;

                    if (grid[r + perpDr, c + perpDc] != CellType.Empty) return false;
                    if (grid[r - perpDr, c - perpDc] != CellType.Empty) return false;
                }
            }
            else
            {
                // Muss exakt übereinstimmen
                if (existing != type) return false;
                if (sols[r, c] != val) return false;

                // Verhindere, dass wir parallel eine bereits in dieser Richtung existierende Gleichung überlappen (Glued Equations)
                if (HasEquationInDirection(grid, r, c, !vertical)) return false;
            }
        }

        return true;
    }

    private void Place(CellType[,] grid, string[,] sols, int startR, int startC,
        bool vertical, EquationData eq, int len)
    {
        int dr = vertical ? 1 : 0;
        int dc = vertical ? 0 : 1;

        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            grid[r, c] = cells[i].type;
            sols[r, c] = cells[i].val;
        }
    }

    private List<(CellType type, string val)> BuildCells(EquationData eq, int len)
    {
        var cells = new List<(CellType, string)>();
        int numIdx = 0, opIdx = 0;

        for (int i = 0; i < len; i++)
        {
            if (i == len - 2)
            {
                cells.Add((CellType.Equals, "="));
            }
            else if (i % 2 == 0)
            {
                cells.Add((CellType.Number, Format(eq.Numbers[numIdx++])));
            }
            else
            {
                cells.Add((CellType.Operator, eq.Operators[opIdx++]));
            }
        }

        return cells;
    }

    private MathCrossGame BuildGame(CellType[,] grid, string[,] sols, Settings s)
    {
        // Finde Bounding Box
        int minR = GridSize, maxR = 0, minC = GridSize, maxC = 0;

        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                if (grid[r, c] != CellType.Empty)
                {
                    minR = Math.Min(minR, r);
                    maxR = Math.Max(maxR, r);
                    minC = Math.Min(minC, c);
                    maxC = Math.Max(maxC, c);
                }
            }
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
        {
            for (int c = 0; c < cols; c++)
            {
                game.Grid[r, c] = new MathCrossCell
                {
                    Row = r,
                    Col = c,
                    Type = grid[minR + r, minC + c],
                    Solution = sols[minR + r, minC + c],
                    UserInput = "",
                    IsGiven = false
                };
            }
        }

        game.Equations = ScanEquations(game, s.EquationLength);
        return game;
    }

    private MathCrossGame GenerateFallbackGrid(Settings s, Random rnd, int depth = 0)
    {
        return GenerateEmergencyTemplateGame(s, rnd.Next());
    }

    private MathCrossGame GenerateEmergencyTemplateGame(Settings s, int seed)
    {
        int len = s.EquationLength;
        int rows = 25;
        int cols = 25;
        var game = new MathCrossGame
        {
            Rows = rows,
            Cols = cols,
            Grid = new MathCrossCell[rows, cols],
            Difficulty = s.DifficultyKey,
            EquationLength = len,
            UseExtendedEquations = s.IsExtended
        };

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                game.Grid[r, c] = new MathCrossCell { Row = r, Col = c, Type = CellType.Empty, Solution = "", UserInput = "", IsGiven = false };

        var rnd = new Random(seed);
        var placements = GetEmergencyPlacements(len);
        var used = new Dictionary<(int r, int c), string>();

        foreach (var (startR, startC, horizontal) in placements)
        {
            EquationData? picked = null;
            for (int attempt = 0; attempt < 120; attempt++)
            {
                var candidate = GenerateEquation(s, rnd);
                if (candidate == null) continue;

                bool ok = true;
                var cells = BuildCells(candidate, len);
                for (int i = 0; i < len; i++)
                {
                    int r = startR + (horizontal ? 0 : i);
                    int c = startC + (horizontal ? i : 0);
                    if (used.TryGetValue((r, c), out var existing) && existing != cells[i].val)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    picked = candidate;
                    break;
                }
            }

            if (picked == null)
                throw new InvalidOperationException("Emergency template could not be filled consistently.");

            var packed = BuildCells(picked, len);
            for (int i = 0; i < len; i++)
            {
                int r = startR + (horizontal ? 0 : i);
                int c = startC + (horizontal ? i : 0);
                used[(r, c)] = packed[i].val;
                game.Grid[r, c].Type = packed[i].type;
                game.Grid[r, c].Solution = packed[i].val;
            }
        }

        return TrimAndScanGame(game, s);
    }

    private List<(int startR, int startC, bool horizontal)> GetEmergencyPlacements(int len)
    {
        int mid = 12;
        int half = len / 2;
        return new List<(int, int, bool)>
        {
            (mid, mid - half, true),
            (mid - half, mid, false),
            (mid - 4, mid - half, true),
            (mid - half, mid - 4, false),
            (mid + 4, mid - half, true),
            (mid - half, mid + 4, false),
            (mid - 2, mid - half - 2, true),
            (mid - half - 2, mid - 2, false)
        };
    }

    private MathCrossGame TrimAndScanGame(MathCrossGame game, Settings s)
    {
        int minR = game.Rows, maxR = 0, minC = game.Cols, maxC = 0;
        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (game.Grid[r, c].Type == CellType.Empty) continue;
                minR = Math.Min(minR, r);
                maxR = Math.Max(maxR, r);
                minC = Math.Min(minC, c);
                maxC = Math.Max(maxC, c);
            }
        }

        int nRows = maxR - minR + 1;
        int nCols = maxC - minC + 1;
        var trimmed = new MathCrossGame
        {
            Rows = nRows,
            Cols = nCols,
            Grid = new MathCrossCell[nRows, nCols],
            Difficulty = s.DifficultyKey,
            EquationLength = s.EquationLength,
            UseExtendedEquations = s.IsExtended
        };

        for (int r = 0; r < nRows; r++)
        {
            for (int c = 0; c < nCols; c++)
            {
                var src = game.Grid[minR + r, minC + c];
                trimmed.Grid[r, c] = new MathCrossCell
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

        trimmed.Equations = ScanEquations(trimmed, s.EquationLength);
        if (trimmed.Equations.Count < s.MinEquations || trimmed.Equations.Count > s.MaxEquations)
            throw new InvalidOperationException("Emergency template produced invalid equation count.");

        return trimmed;
    }

    private bool PlaceInGame(MathCrossGame game, int startR, int startC, bool horizontal, EquationData eq, int len)
    {
        int dr = horizontal ? 0 : 1;
        int dc = horizontal ? 1 : 0;

        if (startR < 0 || startC < 0) return false;
        if (startR + (horizontal ? 0 : len - 1) >= game.Rows) return false;
        if (startC + (horizontal ? len - 1 : 0) >= game.Cols) return false;

        int prevR = startR - dr;
        int prevC = startC - dc;
        if (prevR >= 0 && prevC >= 0 && prevR < game.Rows && prevC < game.Cols && game.Grid[prevR, prevC].Type != CellType.Empty) return false;

        int nextR = startR + len * dr;
        int nextC = startC + len * dc;
        if (nextR >= 0 && nextC >= 0 && nextR < game.Rows && nextC < game.Cols && game.Grid[nextR, nextC].Type != CellType.Empty) return false;

        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            var existing = game.Grid[r, c];
            if (existing.Type != CellType.Empty)
            {
                if (existing.Type != cells[i].type || existing.Solution != cells[i].val) return false;
                
                if (horizontal)
                {
                    if ((c > 0 && game.Grid[r, c - 1].Type != CellType.Empty) ||
                        (c < game.Cols - 1 && game.Grid[r, c + 1].Type != CellType.Empty)) return false;
                }
                else
                {
                    if ((r > 0 && game.Grid[r - 1, c].Type != CellType.Empty) ||
                        (r < game.Rows - 1 && game.Grid[r + 1, c].Type != CellType.Empty)) return false;
                }
            }
            else
            {
                int perpDr = horizontal ? 1 : 0;
                int perpDc = horizontal ? 0 : 1;
                
                if (r + perpDr >= 0 && r + perpDr < game.Rows && c + perpDc >= 0 && c + perpDc < game.Cols && game.Grid[r + perpDr, c + perpDc].Type != CellType.Empty) return false;
                if (r - perpDr >= 0 && r - perpDr < game.Rows && c - perpDc >= 0 && c - perpDc < game.Cols && game.Grid[r - perpDr, c - perpDc].Type != CellType.Empty) return false;
            }
        }

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            game.Grid[r, c].Type = cells[i].type;
            game.Grid[r, c].Solution = cells[i].val;
        }

        return true;
    }

    private List<MathEquation> ScanEquations(MathCrossGame game, int len)
    {
        var list = new List<MathEquation>();

        // Horizontal
        for (int r = 0; r < game.Rows; r++)
        {
            for (int c = 0; c <= game.Cols - len; c++)
            {
                if (IsValidEq(game, r, c, 0, 1, len))
                {
                    var cells = Enumerable.Range(0, len).Select(i => (r, c + i)).ToList();
                    list.Add(new MathEquation
                    {
                        StartRow = r,
                        StartCol = c,
                        IsHorizontal = true,
                        Cells = cells,
                        Operator = game.Grid[r, c + 1].Solution,
                        Operator2 = len >= 7 ? game.Grid[r, c + 3].Solution : "",
                        Operator3 = len >= 9 ? game.Grid[r, c + 5].Solution : ""
                    });
                }
            }
        }

        // Vertikal
        for (int r = 0; r <= game.Rows - len; r++)
        {
            for (int c = 0; c < game.Cols; c++)
            {
                if (IsValidEq(game, r, c, 1, 0, len))
                {
                    var cells = Enumerable.Range(0, len).Select(i => (r + i, c)).ToList();
                    list.Add(new MathEquation
                    {
                        StartRow = r,
                        StartCol = c,
                        IsHorizontal = false,
                        Cells = cells,
                        Operator = game.Grid[r + 1, c].Solution,
                        Operator2 = len >= 7 ? game.Grid[r + 3, c].Solution : "",
                        Operator3 = len >= 9 ? game.Grid[r + 5, c].Solution : ""
                    });
                }
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

    private bool FinalizeGame(MathCrossGame game, Random rnd, Settings s, bool allowFailure = false)
    {
        // Setze Defaults
        for (int r = 0; r < game.Rows; r++)
        {
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
                }
                else
                {
                    cell.IsGiven = false;
                    cell.UserInput = "";
                }
            }
        }

        // Sammle editierbare Zellen
        var editable = new List<MathCrossCell>();
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type is CellType.Number or CellType.Operator)
                    editable.Add(game.Grid[r, c]);

        // Setze Givens
        int toGive = Math.Max(Math.Min(4, editable.Count), (int)(editable.Count * s.GivenPercent));
        toGive = Math.Min(toGive, editable.Count - 2);
        toGive = Math.Max(toGive, 0);

        foreach (var cell in ChooseInitialGivens(game, editable, toGive, rnd))
        {
            cell.IsGiven = true;
            cell.UserInput = cell.Solution;
        }

        bool solvable = EnsureSolvable(game, rnd);

        // Startzustand darf nicht vollständig gelöst sein.
        if (editable.Count > 0 && editable.All(c => c.IsGiven))
        {
            var toHide = editable[rnd.Next(editable.Count)];
            toHide.IsGiven = false;
            toHide.UserInput = "";
            solvable = false;
        }

        game.GivenCells = editable.Count(c => c.IsGiven);

        return allowFailure || solvable;
    }

    private record DeductionResult(int Completions, Dictionary<int, HashSet<string>> Candidates, bool IsConsistent, bool LimitReached);

    private record DeductionPassResult(
        bool IsConsistent,
        Dictionary<(int row, int col), string> KnownValues,
        Dictionary<(int row, int col), int> CandidateCounts,
        Dictionary<(int row, int col), int> EquationCoverage);

    private DeductionResult GetValidCandidates(MathCrossGame game, MathEquation eq, Dictionary<(int row, int col), string> knownValues, Settings s)
    {
        var validCells = eq.Cells.Where(p => IsWithinBounds(game, p.row, p.col)).ToList();
        if (validCells.Count != eq.CellCount)
            return new DeductionResult(0, new Dictionary<int, HashSet<string>>(), false, false);

        if (eq.CellCount == 5)
            return GetValidCandidatesSimple(eq, validCells, knownValues, s);

        List<decimal> numDomain = new List<decimal>();
        for (decimal v = s.MinVal; v <= s.MaxVal; v += s.AllowDecimals ? 0.1m : 1m)
            numDomain.Add(v);
            
        List<string> opDomain = s.Ops.ToList();

        var domains = new List<string>[eq.CellCount];
        for (int i = 0; i < eq.CellCount; i++)
        {
            if (i == eq.CellCount - 2) 
            {
                domains[i] = new List<string> { "=" };
            }
            else if (knownValues.TryGetValue(validCells[i], out var knownValue))
            {
                domains[i] = new List<string> { knownValue };
            }
            else
            {
                if (i % 2 == 0) domains[i] = numDomain.Select(Format).ToList();
                else domains[i] = opDomain.ToList();
            }
        }

        var candidates = new Dictionary<int, HashSet<string>>();
        for (int i = 0; i < eq.CellCount; i++) candidates[i] = new HashSet<string>();

        int completions = 0;
        bool isConsistent = false;
        bool limitReached = false;

        void SolveDFS(int idx, string[] current)
        {
            if (limitReached) return;
            
            if (idx == eq.CellCount)
            {
                decimal? result = null;
                try {
                    int numVars = (eq.CellCount + 1) / 2;
                    decimal currentVal = decimal.Parse(current[0], System.Globalization.CultureInfo.InvariantCulture);
                    for (int o = 0; o < numVars - 2; o++)
                    {
                        string op = current[o * 2 + 1];
                        decimal nextNum = decimal.Parse(current[o * 2 + 2], System.Globalization.CultureInfo.InvariantCulture);
                        var nextRes = CalcDec(currentVal, op, nextNum, s.AllowDecimals);
                        if (nextRes == null) { result = null; break; }
                        currentVal = nextRes.Value;
                    }
                    result = currentVal;
                } catch { result = null; }

                if (result != null)
                {
                    decimal expected = decimal.Parse(current[eq.CellCount - 1], System.Globalization.CultureInfo.InvariantCulture);
                    if (Math.Abs(result.Value - expected) < 0.0001m)
                    {
                        isConsistent = true;
                        completions++;
                        for (int i = 0; i < eq.CellCount; i++)
                        {
                            if (!knownValues.ContainsKey(validCells[i]))
                                candidates[i].Add(current[i]);
                        }
                        if (completions > CandidateSearchLimit) limitReached = true;
                    }
                }
                return;
            }

            if (idx == eq.CellCount - 2)
            {
                current[idx] = "=";
                SolveDFS(idx + 1, current);
                return;
            }

            if (idx == eq.CellCount - 1 && !knownValues.ContainsKey(validCells[idx]))
            {
                decimal? result = null;
                try {
                    int numVars = (eq.CellCount + 1) / 2;
                    decimal currentVal = decimal.Parse(current[0], System.Globalization.CultureInfo.InvariantCulture);
                    for (int o = 0; o < numVars - 2; o++)
                    {
                        string op = current[o * 2 + 1];
                        decimal nextNum = decimal.Parse(current[o * 2 + 2], System.Globalization.CultureInfo.InvariantCulture);
                        var nextRes = CalcDec(currentVal, op, nextNum, s.AllowDecimals);
                        if (nextRes == null) { result = null; break; }
                        currentVal = nextRes.Value;
                    }
                    result = currentVal;
                } catch { result = null; }

                if (result != null)
                {
                    string resStr = Format(result.Value);
                    if (domains[idx].Contains(resStr))
                    {
                        current[idx] = resStr;
                        SolveDFS(idx + 1, current);
                    }
                }
                return;
            }

            foreach (var val in domains[idx])
            {
                current[idx] = val;
                SolveDFS(idx + 1, current);
                if (limitReached) return;
            }
        }

        SolveDFS(0, new string[eq.CellCount]);

        if (limitReached)
        {
            foreach (var key in candidates.Keys.ToList())
            {
                if (candidates[key].Count < 2) 
                {
                    candidates[key].Add("AMB_1");
                    candidates[key].Add("AMB_2");
                }
            }
        }

        return new DeductionResult(completions, candidates, isConsistent, limitReached);
    }

    private DeductionResult GetValidCandidatesSimple(
        MathEquation eq,
        List<(int row, int col)> validCells,
        Dictionary<(int row, int col), string> knownValues,
        Settings s)
    {
        var candidates = new Dictionary<int, HashSet<string>>
        {
            [0] = new HashSet<string>(),
            [1] = new HashSet<string>(),
            [2] = new HashSet<string>(),
            [3] = new HashSet<string>(),
            [4] = new HashSet<string>()
        };

        decimal? a = TryGetKnownNumber(0);
        decimal? b = TryGetKnownNumber(2);
        decimal? c = TryGetKnownNumber(4);
        string? op = TryGetKnownOperator(1);

        if (op != null)
        {
            TryUseOperator(op);
        }
        else
        {
            foreach (var allowedOp in s.Ops)
            {
                TryUseOperator(allowedOp);
            }
        }

        bool allCoreKnown = knownValues.ContainsKey(validCells[0]) && knownValues.ContainsKey(validCells[1]) && knownValues.ContainsKey(validCells[2]) && knownValues.ContainsKey(validCells[4]);
        if (allCoreKnown)
        {
            bool matches = false;
            if (decimal.TryParse(knownValues[validCells[0]], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ka)
                && decimal.TryParse(knownValues[validCells[2]], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var kb)
                && decimal.TryParse(knownValues[validCells[4]], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var kc))
            {
                var computed = CalcDec(ka, knownValues[validCells[1]], kb, s.AllowDecimals);
                matches = computed != null && AreSameNumber(computed.Value, kc);
            }

            return new DeductionResult(matches ? 1 : 0, candidates, matches, false);
        }

        int completions = candidates[0].Count == 0 && candidates[1].Count == 0 && candidates[2].Count == 0 && candidates[4].Count == 0
            ? 0
            : Math.Max(Math.Max(candidates[0].Count, candidates[2].Count), Math.Max(candidates[1].Count, candidates[4].Count));

        bool consistent = candidates[0].Count > 0 || candidates[1].Count > 0 || candidates[2].Count > 0 || candidates[4].Count > 0;
        return new DeductionResult(consistent ? Math.Max(1, completions) : 0, candidates, consistent, false);

        decimal? TryGetKnownNumber(int idx)
        {
            if (!knownValues.TryGetValue(validCells[idx], out var txt)) return null;
            if (!decimal.TryParse(txt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v)) return null;
            return v;
        }

        string? TryGetKnownOperator(int idx)
        {
            return knownValues.TryGetValue(validCells[idx], out var txt) ? txt : null;
        }

        void TryUseOperator(string currentOp)
        {
            if (!s.Ops.Contains(currentOp)) return;

            if (a.HasValue && b.HasValue)
            {
                var result = CalcDec(a.Value, currentOp, b.Value, s.AllowDecimals);
                if (result == null || !IsAllowedNumber(result.Value, s)) return;
                if (c.HasValue && !AreSameNumber(c.Value, result.Value)) return;
                AddCompletion(a.Value, currentOp, b.Value, result.Value);
                return;
            }

            if (a.HasValue && c.HasValue)
            {
                var inferredB = SolveRightOperand(a.Value, c.Value, currentOp, s.AllowDecimals);
                if (inferredB == null || !IsAllowedNumber(inferredB.Value, s)) return;
                AddCompletion(a.Value, currentOp, inferredB.Value, c.Value);
                return;
            }

            if (b.HasValue && c.HasValue)
            {
                var inferredA = SolveLeftOperand(b.Value, c.Value, currentOp, s.AllowDecimals);
                if (inferredA == null || !IsAllowedNumber(inferredA.Value, s)) return;
                AddCompletion(inferredA.Value, currentOp, b.Value, c.Value);
                return;
            }

            if (a.HasValue && !b.HasValue && !c.HasValue)
            {
                foreach (var bCandidate in NumberDomain(s))
                {
                    var result = CalcDec(a.Value, currentOp, bCandidate, s.AllowDecimals);
                    if (result == null || !IsAllowedNumber(result.Value, s)) continue;
                    AddCompletion(a.Value, currentOp, bCandidate, result.Value);
                }
                return;
            }

            if (!a.HasValue && b.HasValue && !c.HasValue)
            {
                foreach (var aCandidate in NumberDomain(s))
                {
                    var result = CalcDec(aCandidate, currentOp, b.Value, s.AllowDecimals);
                    if (result == null || !IsAllowedNumber(result.Value, s)) continue;
                    AddCompletion(aCandidate, currentOp, b.Value, result.Value);
                }
                return;
            }

            if (!a.HasValue && !b.HasValue && c.HasValue)
            {
                foreach (var aCandidate in NumberDomain(s))
                {
                    var inferred = SolveRightOperand(aCandidate, c.Value, currentOp, s.AllowDecimals);
                    if (inferred == null || !IsAllowedNumber(inferred.Value, s)) continue;
                    AddCompletion(aCandidate, currentOp, inferred.Value, c.Value);
                }
                return;
            }

            foreach (var aCandidate in NumberDomain(s))
            {
                foreach (var bCandidate in NumberDomain(s))
                {
                    var result = CalcDec(aCandidate, currentOp, bCandidate, s.AllowDecimals);
                    if (result == null || !IsAllowedNumber(result.Value, s)) continue;
                    AddCompletion(aCandidate, currentOp, bCandidate, result.Value);
                    if (candidates[0].Count > CandidateSearchLimit) return;
                }
            }
        }

        void AddCompletion(decimal av, string ov, decimal bv, decimal cv)
        {
            var avs = Format(av);
            var bvs = Format(bv);
            var cvs = Format(cv);

            if (knownValues.TryGetValue(validCells[0], out var ka) && ka != avs) return;
            if (knownValues.TryGetValue(validCells[1], out var ko) && ko != ov) return;
            if (knownValues.TryGetValue(validCells[2], out var kb) && kb != bvs) return;
            if (knownValues.TryGetValue(validCells[4], out var kc) && kc != cvs) return;

            if (!IsAllowedNumber(av, s) || !IsAllowedNumber(bv, s) || !IsAllowedNumber(cv, s)) return;

            if (!knownValues.ContainsKey(validCells[0])) candidates[0].Add(avs);
            if (!knownValues.ContainsKey(validCells[1])) candidates[1].Add(ov);
            if (!knownValues.ContainsKey(validCells[2])) candidates[2].Add(bvs);
            if (!knownValues.ContainsKey(validCells[4])) candidates[4].Add(cvs);
            candidates[3].Add("=");
        }
    }

    private bool EnsureSolvable(MathCrossGame game, Random rnd)
    {
        return EnsureSolvableCore(game, rnd, null);
    }

    private bool EnsureSolvableCore(MathCrossGame game, Random rnd, int? maxStrategicReveals)
    {
        Settings s = GetSettings(game.Difficulty);
        int maxExtraGivens = Math.Max(2, game.Equations.Count / 3);
        var editable = GetEditableCoordinates(game).ToList();
        int revealBudget = Math.Min(maxExtraGivens, maxStrategicReveals ?? maxExtraGivens);

        for (int revealsUsed = 0; revealsUsed <= revealBudget; revealsUsed++)
        {
            var pass = RunDeductionPass(game, s);
            if (!pass.IsConsistent) return false;

            var unsolved = editable
                .Where(p => !pass.KnownValues.ContainsKey(p))
                .ToList();

            if (unsolved.Count == 0) return true;
            if (revealsUsed == revealBudget) return false;

            var baseSolved = pass.KnownValues.Count;
            var pick = unsolved
                .Select(coord =>
                {
                    var simulated = new Dictionary<(int row, int col), string>(pass.KnownValues)
                    {
                        [coord] = game.Grid[coord.row, coord.col].Solution
                    };
                    var simulatedPass = RunDeductionPass(game, s, simulated);
                    int gain = simulatedPass.IsConsistent ? simulatedPass.KnownValues.Count - baseSolved : int.MinValue;
                    int ambiguity = pass.CandidateCounts.TryGetValue(coord, out var cands) ? cands : int.MaxValue;
                    int eqCoverage = pass.EquationCoverage.TryGetValue(coord, out var eqCount) ? eqCount : 0;
                    return (coord, gain, ambiguity, eqCoverage);
                })
                .OrderByDescending(x => x.gain)
                .ThenBy(x => x.ambiguity)
                .ThenByDescending(x => x.eqCoverage)
                .ThenBy(_ => rnd.Next())
                .First().coord;

            var revealed = game.Grid[pick.row, pick.col];
            revealed.IsGiven = true;
            revealed.UserInput = revealed.Solution;
        }

        return false;
    }


    private DeductionPassResult RunDeductionPass(
        MathCrossGame game,
        Settings s,
        Dictionary<(int row, int col), string>? seedKnownValues = null)
    {
        var knownValues = seedKnownValues != null
            ? new Dictionary<(int row, int col), string>(seedKnownValues)
            : new Dictionary<(int row, int col), string>();

        var editable = GetEditableCoordinates(game).ToList();
        foreach (var (r, c) in editable)
        {
            if (game.Grid[r, c].IsGiven)
                knownValues[(r, c)] = game.Grid[r, c].Solution;
        }

        var candidateCounts = new Dictionary<(int row, int col), int>();
        var equationCoverage = new Dictionary<(int row, int col), int>();

        bool progress = true;
        int guard = 0;
        while (progress && guard++ < SolvabilityRetryLimit)
        {
            progress = false;
            var mergedCandidates = new Dictionary<(int row, int col), HashSet<string>>();
            candidateCounts.Clear();
            equationCoverage.Clear();

            foreach (var eq in game.Equations)
            {
                var res = GetValidCandidates(game, eq, knownValues, s);
                if (!res.IsConsistent || res.Completions == 0)
                    return new DeductionPassResult(false, knownValues, candidateCounts, equationCoverage);

                var validCells = eq.Cells.Where(p => IsWithinBounds(game, p.row, p.col)).ToList();
                for (int i = 0; i < validCells.Count; i++)
                {
                    var coord = validCells[i];
                    if (knownValues.ContainsKey(coord) || game.Grid[coord.row, coord.col].Type is CellType.Empty or CellType.Equals)
                        continue;

                    if (!mergedCandidates.TryGetValue(coord, out var set))
                    {
                        set = new HashSet<string>(res.Candidates[i]);
                        mergedCandidates[coord] = set;
                    }
                    else
                    {
                        set.IntersectWith(res.Candidates[i]);
                    }

                    candidateCounts[coord] = set.Count;
                    equationCoverage[coord] = equationCoverage.TryGetValue(coord, out var ec) ? ec + 1 : 1;
                }
            }

            foreach (var kv in mergedCandidates)
            {
                if (kv.Value.Count == 0)
                    return new DeductionPassResult(false, knownValues, candidateCounts, equationCoverage);

                if (kv.Value.Count == 1)
                {
                    knownValues[kv.Key] = kv.Value.First();
                    progress = true;
                }
            }
        }

        return new DeductionPassResult(true, knownValues, candidateCounts, equationCoverage);
    }

    private IEnumerable<(int row, int col)> GetEditableCoordinates(MathCrossGame game)
    {
        for (int r = 0; r < game.Rows; r++)
            for (int c = 0; c < game.Cols; c++)
                if (game.Grid[r, c].Type is CellType.Number or CellType.Operator)
                    yield return (r, c);
    }

    private IEnumerable<MathCrossCell> ChooseInitialGivens(MathCrossGame game, List<MathCrossCell> editable, int toGive, Random rnd)
    {
        var selected = new HashSet<MathCrossCell>();

        foreach (var eq in game.Equations.OrderBy(_ => rnd.Next()))
        {
            var options = eq.Cells
                .Where(p => IsWithinBounds(game, p.row, p.col))
                .Select(p => game.Grid[p.row, p.col])
                .Where(c => c.Type is CellType.Number or CellType.Operator)
                .OrderBy(c => c.Type == CellType.Operator ? 0 : 1)
                .ThenBy(_ => rnd.Next())
                .ToList();

            if (options.Count == 0 || selected.Count >= toGive)
                continue;

            var pick = options.First();
            selected.Add(pick);
        }

        foreach (var cell in editable
            .OrderBy(c => c.Type == CellType.Operator ? 0 : 1)
            .ThenBy(_ => rnd.Next()))
        {
            if (selected.Count >= toGive)
                break;
            selected.Add(cell);
        }

        return selected;
    }

    private static IEnumerable<decimal> NumberDomain(Settings s)
    {
        if (!s.AllowDecimals)
        {
            for (int v = s.MinVal; v <= s.MaxVal; v++)
                yield return v;
            yield break;
        }

        for (int v = s.MinVal; v <= s.MaxVal; v++)
        {
            yield return v;
            for (int tenth = 1; tenth <= 9; tenth++)
                yield return v + tenth / 10m;
        }
    }

    private static bool IsAllowedNumber(decimal value, Settings s)
    {
        if (value < s.MinVal || value > s.MaxVal) return false;
        if (!s.AllowDecimals && value != Math.Truncate(value)) return false;
        if (s.AllowDecimals && !HasAtMostOneDecimal(value)) return false;
        return true;
    }

    private static bool AreSameNumber(decimal a, decimal b)
    {
        return Math.Abs(a - b) < 0.0001m;
    }

    private static decimal? SolveRightOperand(decimal left, decimal result, string op, bool allowDecimalDivision)
    {
        return op switch
        {
            "+" => result - left,
            "-" => left - result,
            "×" when left != 0 => result / left,
            "÷" when result != 0 => DivideWithRule(left, result, allowDecimalDivision),
            _ => null
        };
    }

    private static decimal? SolveLeftOperand(decimal right, decimal result, string op, bool allowDecimalDivision)
    {
        return op switch
        {
            "+" => result - right,
            "-" => result + right,
            "×" when right != 0 => result / right,
            "÷" => right * result,
            _ => null
        };
    }

    private static bool IsBudgetExceeded(DateTime startedAt)
    {
        return (DateTime.UtcNow - startedAt).TotalMilliseconds >= GenerationBudgetMs;
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
    private record EquationPlacement(int StartR, int StartC, bool Vertical, EquationData Eq);

    private record Settings(
        string DifficultyKey, int MinVal, int MaxVal, bool AllowDecimals,
        string[] Ops, int MinEquations, int MaxEquations, double GivenPercent,
        int EquationLength, bool IsExtended
    );

    private static Settings GetSettings(string key)
    {
        key = (key ?? "easy").Trim().ToLowerInvariant();

        return key switch
        {
            // Einfach: a op b = c, +/-, 1-99
            "easy" => new Settings("easy", 1, 99, false,
                new[] { "+", "-" }, 8, 12, 0.45, 5, false),

            // Normal: a op b = c, +/-/×, 1-99
            "normal" => new Settings("normal", 1, 99, false,
                new[] { "+", "-", "×" }, 8, 12, 0.40, 5, false),

            // Schwer: a op b op c = d, +/-/×/÷, -1 bis 99
            "hard" => new Settings("hard", -1, 99, false,
                new[] { "+", "-", "×", "÷" }, 8, 12, 0.35, 7, true),

            // Master: a op b op c = d, +/-/×/÷, -1 bis 99 mit Dezimal
            "master" => new Settings("master", -1, 99, true,
                new[] { "+", "-", "×", "÷" }, 8, 12, 0.30, 7, true),

            _ => GetSettings("easy")
        };
    }
}
