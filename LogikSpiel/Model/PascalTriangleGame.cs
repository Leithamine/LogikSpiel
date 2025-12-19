using System.Collections.Generic;

namespace LogikSpiel.Model;

public class PascalTriangleGame
{
    /// <summary>
    /// Das Dreieck als 2D-Array (Zeile → Werte)
    /// Zeile 0: [5]
    /// Zeile 1: [3, 8]
    /// Zeile 2: [7, 4, 6]
    /// etc.
    /// </summary>
    public List<List<decimal>> Triangle { get; set; } = new();

    /// <summary>
    /// Schwierigkeitsstufe
    /// </summary>
    public string Difficulty { get; set; } = "easy";

    /// <summary>
    /// Anzahl Zeilen
    /// </summary>
    public int Rows => Triangle.Count;

    /// <summary>
    /// Maximale Pfad-Summe (Lösung)
    /// </summary>
    public decimal MaxPathSum { get; set; }

    /// <summary>
    /// Minimale Pfad-Summe (Lösung)
    /// </summary>
    public decimal MinPathSum { get; set; }

    /// <summary>
    /// Indizes des Max-Pfades (für Visualisierung)
    /// Format: List of (row, col)
    /// </summary>
    public List<(int row, int col)> MaxPath { get; set; } = new();

    /// <summary>
    /// Indizes des Min-Pfades
    /// </summary>
    public List<(int row, int col)> MinPath { get; set; } = new();
}

