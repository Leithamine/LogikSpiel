using System;
using System.Collections.Generic;
using System.Text;

namespace LogikSpiel.Model
{
    /// <summary>
    /// Repräsentiert eine Zelle im Dreieck für die UI
    /// </summary>
    public class TriangleCell
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public decimal Value { get; set; }

        /// <summary>
        /// Ist diese Zelle Teil des aktuell ausgewählten Pfades?
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Ist diese Zelle Teil des korrekten Max-Pfades? (für Lösung anzeigen)
        /// </summary>
        public bool IsMaxPath { get; set; }

        /// <summary>
        /// Ist diese Zelle Teil des korrekten Min-Pfades?
        /// </summary>
        public bool IsMinPath { get; set; }
    }
}
