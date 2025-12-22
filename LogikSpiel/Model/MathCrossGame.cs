using System.Collections.Generic;

namespace LogikSpiel.Model;

public enum CellType
{
    Empty,
    Number,
    Operator,
    Equals
}

public class MathCrossCell
{
    public int Row { get; set; }
    public int Col { get; set; }
    public CellType Type { get; set; }

    public string Solution { get; set; } = "";
    public string UserInput { get; set; } = "";

    public bool IsGiven { get; set; }
    public bool IsSelected { get; set; }

    public bool IsCorrect => !string.IsNullOrEmpty(UserInput) && UserInput == Solution;
}

public class MathEquation
{
    public int StartRow { get; set; }
    public int StartCol { get; set; }
    public bool IsHorizontal { get; set; }

    // 5 cells: A op B = C
    // 7 cells: A op1 B op2 C = D
    // 9 cells: A op1 B op2 C op3 D = E
    public List<(int row, int col)> Cells { get; set; } = new();

    public string Operator { get; set; } = "";
    public string Operator2 { get; set; } = "";
    public string Operator3 { get; set; } = ""; // ✅ neu für Master (9)

    public int CellCount => Cells.Count; // 5 / 7 / 9
}

public class MathCrossGame
{
    public int Rows { get; set; }
    public int Cols { get; set; }

    public MathCrossCell[,] Grid { get; set; } = new MathCrossCell[0, 0];
    public List<MathEquation> Equations { get; set; } = new();

    public string Difficulty { get; set; } = "easy";
    public int GivenCells { get; set; }

    // True for Hard/Master (>=7)
    public bool UseExtendedEquations { get; set; }

    // ✅ neu: echte Länge (5/7/9)
    public int EquationLength { get; set; } = 5;

    public int GridSize => System.Math.Max(Rows, Cols);
}
