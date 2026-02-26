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
    private const int FinalizeAttempts = 8;
    private const int SolvabilityRetryLimit = 60;

    public MathCrossGame GenerateGame(string difficultyKey, int seed)
    {
        var s = GetSettings(difficultyKey);

        // Versuche mehrmals ein gutes und lösbares Rätsel zu generieren
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var game = TryGenerate(s, new Random(seed + attempt * 1000));
            if (game == null || game.Equations.Count < s.MinEquations) continue;

            for (int finalizeAttempt = 0; finalizeAttempt < FinalizeAttempts; finalizeAttempt++)
            {
                var finalizeRnd = new Random(seed + attempt * 1000 + finalizeAttempt * 97 + 17);
                if (FinalizeGame(game, finalizeRnd, s))
                {
                    return game;
                }
            }
        }

        // Fallback: Einfaches Grid-Layout; bei unlösbaren Starts neu würfeln
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var fallbackRnd = new Random(seed + 50000 + attempt * 131);
            var fallback = GenerateFallbackGrid(s, fallbackRnd);
            if (FinalizeGame(fallback, fallbackRnd, s))
            {
                return fallback;
            }
        }

        // Letzte Absicherung: Minimal konfiguriertes Fallback ohne Vollaufdeckung.
        var lastRnd = new Random(seed + 999999);
        var lastFallback = GenerateFallbackGrid(s, lastRnd);
        FinalizeGame(lastFallback, lastRnd, s, allowFailure: true);
        return lastFallback;
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

        // Abwechselnd vertikal und horizontal hinzufügen
        bool tryVertical = true;
        int fails = 0;

        while (placed.Count < target && fails < 300)
        {
            fails++;

            // Sammle alle Zahlen-Zellen
            var numbers = GetNumberPositions(grid, solutions);
            if (numbers.Count == 0) continue;

            // Wähle zufällig eine Zahl
            var (nr, nc, val) = numbers[rnd.Next(numbers.Count)];

            // Prüfe ob diese Zelle schon Teil einer Gleichung in der gewünschten Richtung ist
            bool hasHorizontal = HasEquationInDirection(grid, nr, nc, true);
            bool hasVertical = HasEquationInDirection(grid, nr, nc, false);

            // Wähle die Richtung die noch nicht belegt ist
            bool vertical;
            if (hasHorizontal && !hasVertical) vertical = true;
            else if (!hasHorizontal && hasVertical) vertical = false;
            else if (!hasHorizontal && !hasVertical) vertical = tryVertical;
            else continue; // Beide Richtungen belegt

            // Versuche alle Ankerpositionen
            int[] anchors = s.IsExtended ? new[] { 0, 2, 4, 6 } : new[] { 0, 2, 4 };

            foreach (int anchor in anchors.OrderBy(_ => rnd.Next()))
            {
                var eq = GenerateEquationWithValue(s, rnd, anchor, val);
                if (eq == null) continue;

                int startR = vertical ? nr - anchor : nr;
                int startC = vertical ? nc : nc - anchor;

                if (CanPlace(grid, solutions, startR, startC, vertical, s.EquationLength, eq, nr, nc))
                {
                    Place(grid, solutions, startR, startC, vertical, eq, s.EquationLength);
                    placed.Add(new EquationPlacement(startR, startC, vertical, eq));
                    tryVertical = !tryVertical;
                    fails = 0;
                    break;
                }
            }
        }

        if (placed.Count < s.MinEquations) return null;

        return BuildGame(grid, solutions, s);
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

    private MathCrossGame GenerateFallbackGrid(Settings s, Random rnd)
    {
        // Erzeuge ein garantiertes 8-Gleichungen Grid im Gitter-Muster
        int eqLen = s.EquationLength;

        // 4 horizontal + 4 vertikal in einem Gitter (inkl. klarer Spacer zwischen Gleichungen)
        int rows = eqLen * 2 + 3;
        int cols = eqLen * 2 + 3;

        var game = new MathCrossGame
        {
            Rows = rows,
            Cols = cols,
            Grid = new MathCrossCell[rows, cols],
            Difficulty = s.DifficultyKey,
            EquationLength = eqLen,
            UseExtendedEquations = s.IsExtended
        };

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                game.Grid[r, c] = new MathCrossCell
                {
                    Row = r,
                    Col = c,
                    Type = CellType.Empty,
                    Solution = "",
                    UserInput = "",
                    IsGiven = false
                };

        int first = 1;
        int second = 1 + eqLen + 1;

        // 4 horizontale Gleichungen
        var starts = new (int row, int col)[]
        {
            (first, first),
            (first, second),
            (second, first),
            (second, second)
        };

        foreach (var (row, col) in starts)
        {
            var eq = GenerateEquation(s, rnd);
            if (eq == null || !PlaceInGame(game, row, col, true, eq, eqLen))
                return GenerateFallbackGrid(s, new Random(rnd.Next()));
        }

        // 4 vertikale Gleichungen, jeweils über den vorhandenen Startwert verankert
        foreach (var (row, col) in starts)
        {
            if (!decimal.TryParse(game.Grid[row, col].Solution, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var anchor))
            {
                return GenerateFallbackGrid(s, new Random(rnd.Next()));
            }

            var eq = GenerateEquationWithValue(s, rnd, anchorPos: 0, anchorVal: anchor);
            if (eq == null || !PlaceInGame(game, row, col, false, eq, eqLen))
                return GenerateFallbackGrid(s, new Random(rnd.Next()));
        }

        game.Equations = ScanEquations(game, eqLen);
        return game;
    }

    private bool PlaceInGame(MathCrossGame game, int startR, int startC, bool horizontal, EquationData eq, int len)
    {
        int dr = horizontal ? 0 : 1;
        int dc = horizontal ? 1 : 0;

        var cells = BuildCells(eq, len);

        for (int i = 0; i < len; i++)
        {
            int r = startR + i * dr;
            int c = startC + i * dc;
            if (r < 0 || r >= game.Rows || c < 0 || c >= game.Cols) return false;

            var existing = game.Grid[r, c];
            if (existing.Type != CellType.Empty && (existing.Type != cells[i].type || existing.Solution != cells[i].val))
                return false;
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
                    list.Add(new MathEquation
                    {
                        Cells = Enumerable.Range(0, len).Select(i => (r, c + i)).ToList()
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
                    list.Add(new MathEquation
                    {
                        Cells = Enumerable.Range(0, len).Select(i => (r + i, c)).ToList()
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

        foreach (var cell in editable.OrderBy(_ => rnd.Next()).Take(toGive))
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

    private bool EnsureSolvable(MathCrossGame game, Random rnd)
    {
        int extraGivens = 0;
        const int MaxExtraGivens = 2;

        for (int iter = 0; iter < SolvabilityRetryLimit; iter++)
        {
            var solvable = new bool[game.Rows, game.Cols];

            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                    if (game.Grid[r, c].IsGiven || game.Grid[r, c].Type is CellType.Empty or CellType.Equals)
                        solvable[r, c] = true;

            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var eq in game.Equations)
                {
                    var validCells = eq.Cells
                        .Where(p => IsWithinBounds(game, p.row, p.col))
                        .ToList();

                    if (validCells.Count == 0)
                        continue;

                    int unknowns = validCells.Count(p => !solvable[p.row, p.col]);
                    if (unknowns == 1)
                    {
                        foreach (var (er, ec) in validCells)
                            solvable[er, ec] = true;
                        progress = true;
                    }
                }
            }

            var unsolved = new List<MathCrossCell>();
            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                    if (game.Grid[r, c].Type is CellType.Number or CellType.Operator && !solvable[r, c])
                        unsolved.Add(game.Grid[r, c]);

            if (unsolved.Count == 0) return true;
            
            if (extraGivens >= MaxExtraGivens) return false;
            extraGivens++;

            var pick = unsolved[rnd.Next(unsolved.Count)];
            pick.IsGiven = true;
            pick.UserInput = pick.Solution;
        }

        return false;
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
