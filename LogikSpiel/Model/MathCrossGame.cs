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

    // 5 cells: A op B = C (Easy/Normal)
    // 7 cells: A op1 B op2 C = D (Hard/Master)
    public List<(int row, int col)> Cells { get; set; } = new();

    public string Operator { get; set; } = "";
    public string Operator2 { get; set; } = ""; // For 7-cell equations

    public int CellCount => Cells.Count; // 5 or 7
}

public class MathCrossGame
{
    public int Rows { get; set; }
    public int Cols { get; set; }

    public MathCrossCell[,] Grid { get; set; } = new MathCrossCell[0, 0];
    public List<MathEquation> Equations { get; set; } = new();

    public string Difficulty { get; set; } = "easy";
    public int GivenCells { get; set; }

    // True for Hard/Master (7-cell equations)
    public bool UseExtendedEquations { get; set; }

    public int GridSize => System.Math.Max(Rows, Cols);
}