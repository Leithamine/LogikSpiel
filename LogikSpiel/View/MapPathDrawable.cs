#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LogikSpiel.ViewModel;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View;

public sealed class MapPathDrawable : IDrawable
{
    private readonly IList<LevelNodeViewModel> _nodes;
    private readonly float _cloudHeight;

    private const float NodeSize = 60f;

    public MapPathDrawable(IList<LevelNodeViewModel> nodes, float cloudHeight)
    {
        _nodes = nodes;
        _cloudHeight = cloudHeight;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_nodes == null || _nodes.Count < 1) return;

        canvas.SaveState();

        // 1) Punkte sammeln (Nodes + Wolke als Ziel)
        var knots = new List<PointF>();
        foreach (var node in _nodes)
            knots.Add(GetNodeCenter(node, dirtyRect));

        knots.Add(new PointF(dirtyRect.Width / 2f, _cloudHeight / 1.6f)); // Wolke

        // 2) Spline
        const int stepsPerSegment = 22;
        var pathPoints = GenerateSplinePoints(knots, stepsPerSegment);

        // Road width (weich, passend zu pgbbg)
        float baseWidth = 18f;

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        // ---------- A) INAKTIVER PFAD (nicht grau -> warmes Weiß) ----------
        DrawStroke(canvas, pathPoints, baseWidth, Color.FromRgba(255, 255, 255, 0.33f));
        DrawStroke(canvas, pathPoints, baseWidth - 6f, Color.FromRgba(255, 255, 255, 0.16f));

        // ---------- B) AKTIVER PFAD (nur bis zusammenhängend gelöst) ----------
        // Wir aktivieren nur Segmente 0->1, 1->2, ... solange alle vorherigen gelöst sind.
        int activeSegments = GetContiguousCompletedSegments();

        if (activeSegments > 0)
        {
            // aktiven Teil ausschneiden
            int endIndex = Math.Min(activeSegments * stepsPerSegment, pathPoints.Count - 1);
            var activePoints = pathPoints.Take(endIndex + 1).ToList();

            // Pastell / Himmel-Passend (kein Neon-Grell)
            var blue = Color.FromArgb("#5DADE2");   // Primary
            var gold = Color.FromArgb("#FFD54F");   // Accent
            var white = Color.FromRgba(255, 255, 255, 0.85f);

            // Layer: Glow soft -> Color -> Highlight
            DrawStroke(canvas, activePoints, baseWidth + 4f, blue.WithAlpha(0.18f));
            DrawStroke(canvas, activePoints, baseWidth, blue.WithAlpha(0.55f));
            DrawStroke(canvas, activePoints, baseWidth - 6f, gold.WithAlpha(0.45f));
            DrawStroke(canvas, activePoints, baseWidth - 10f, white.WithAlpha(0.70f));
        }

        canvas.RestoreState();
    }

    /// <summary>
    /// Aktiviert nur so weit, wie Level zusammenhängend (ab Start) completed sind.
    /// Beispiel: 0..18 completed, 19 nicht -> nur bis Segment 18->19 aktiv.
    /// </summary>
    private int GetContiguousCompletedSegments()
    {
        // Segmente = Anzahl Kanten zwischen Nodes (ohne Wolken-Segment)
        // Wenn Node[0] completed => Segment 0->1 darf aktiv sein usw.
        int segments = 0;

        // Wir laufen von oben nach unten: sobald wir einen nicht completed finden, stoppen wir.
        // Damit kann nie "20 aktiv" sein, wenn 19 nicht gelöst ist.
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].IsCompleted)
                segments = i + 1; // aktiviert bis zum nächsten Knoten
            else
                break;
        }

        // Maximal bis zum letzten Node-Segment (Wolke nicht mitzählen)
        return Math.Clamp(segments, 0, Math.Max(0, _nodes.Count - 1));
    }

    private static void DrawStroke(ICanvas canvas, IList<PointF> pts, float width, Color color)
    {
        if (pts.Count < 2) return;

        using var p = new PathF();
        p.MoveTo(pts[0]);
        for (int i = 1; i < pts.Count; i++)
            p.LineTo(pts[i]);

        canvas.StrokeSize = width;
        canvas.StrokeColor = color;
        canvas.DrawPath(p);
    }

    private static List<PointF> GenerateSplinePoints(List<PointF> knots, int steps)
    {
        var results = new List<PointF>();
        if (knots.Count < 2) return knots;

        for (int i = 0; i < knots.Count - 1; i++)
        {
            PointF p0 = i == 0 ? knots[0] : knots[i - 1];
            PointF p1 = knots[i];
            PointF p2 = knots[i + 1];
            PointF p3 = (i + 2 < knots.Count) ? knots[i + 2] : knots[i + 1];

            for (int t = 0; t < steps; t++)
            {
                float tt = t / (float)steps;
                results.Add(GetCatmullRomPos(tt, p0, p1, p2, p3));
            }
        }

        results.Add(knots.Last());
        return results;
    }

    private static PointF GetCatmullRomPos(float t, PointF p0, PointF p1, PointF p2, PointF p3)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float x = 0.5f * ((2 * p1.X) +
            (-p0.X + p2.X) * t +
            (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
            (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

        float y = 0.5f * ((2 * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

        return new PointF(x, y);
    }

    private static PointF GetNodeCenter(LevelNodeViewModel node, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return new PointF(0, 0);

        float availableW = dirtyRect.Width - NodeSize;
        float availableH = dirtyRect.Height - NodeSize;

        if (availableW < 0) availableW = 0;
        if (availableH < 0) availableH = 0;

        float left = availableW * (float)node.Bounds.X;
        float top = availableH * (float)node.Bounds.Y;

        return new PointF(left + NodeSize / 2f, top + NodeSize / 2f);
    }
}
