using LogikSpiel.ViewModel;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View;

public class MapPathDrawable : IDrawable
{
    private readonly IList<LevelNodeViewModel> _nodes;
    private readonly float _cloudHeight; // Höhe der Wolke
    private const float NodeSize = 60f;

    public MapPathDrawable(IList<LevelNodeViewModel> nodes, float cloudHeight)
    {
        _nodes = nodes;
        _cloudHeight = cloudHeight;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Wir brauchen mindestens 1 Level, um zu zeichnen
        if (_nodes == null || _nodes.Count < 1) return;

        canvas.SaveState();

        // 1. Alle Punkte sammeln (Nodes + Wolke)
        var points = new List<PointF>();
        foreach (var node in _nodes)
        {
            points.Add(GetNodeCenter(node, dirtyRect));
        }

        // --- ZUSATZ-PUNKT: MITTE DER WOLKE ---
        // Die Wolke ist oben (Y=0) und mittig (Width/2).
        // Wir zielen etwas tiefer als 0, damit die Linie "in" die Wolke geht.
        points.Add(new PointF(dirtyRect.Width / 2, _cloudHeight / 1.6f));

        // 2. Spline berechnen (Kurve durch alle Punkte)
        int stepsPerSegment = 20;
        var allPathPoints = GenerateSplinePoints(points, stepsPerSegment);

        float roadWidth = 20f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        // A. HINTERGRUND (Grau) - Geht bis zur Wolke
        using (var bgPath = new PathF())
        {
            if (allPathPoints.Count > 0)
            {
                bgPath.MoveTo(allPathPoints[0]);
                foreach (var p in allPathPoints) bgPath.LineTo(p);
            }
            canvas.StrokeSize = roadWidth;
            canvas.StrokeColor = Colors.LightGray.WithAlpha(0.5f);
            canvas.DrawPath(bgPath);
        }

        // B. FORTSCHRITT (Grün)
        using (var activePath = new PathF())
        {
            bool hasActiveSegments = false;

            // Wir gehen durch alle Nodes.
            // Wenn Node[i] gelöst ist, malen wir den Weg zum nächsten Punkt (i+1).
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].IsCompleted)
                {
                    int startIndex = i * stepsPerSegment;
                    int endIndex = (i + 1) * stepsPerSegment;

                    // Schutz vor Index-Fehlern
                    if (endIndex < allPathPoints.Count)
                    {
                        // Beim allerersten Segment müssen wir MoveTo machen
                        if (!hasActiveSegments)
                        {
                            activePath.MoveTo(allPathPoints[startIndex]);
                            hasActiveSegments = true;
                        }
                        else
                        {
                            // Sicherstellen, dass wir an der richtigen Stelle sind
                            activePath.MoveTo(allPathPoints[startIndex]);
                        }

                        for (int k = startIndex + 1; k <= endIndex; k++)
                        {
                            activePath.LineTo(allPathPoints[k]);
                        }
                    }
                }
            }

            if (hasActiveSegments)
            {
                canvas.StrokeSize = roadWidth;
                canvas.StrokeColor = Color.FromArgb("#03DAC5"); // Türkis
                canvas.StrokeDashPattern = null;
                canvas.DrawPath(activePath);
            }
        }

        canvas.RestoreState();
    }

    // --- MATHEMATIK (Unverändert) ---
    private List<PointF> GenerateSplinePoints(List<PointF> knots, int steps)
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
                float tNormalized = t / (float)steps;
                results.Add(GetCatmullRomPos(tNormalized, p0, p1, p2, p3));
            }
        }
        results.Add(knots.Last());
        return results;
    }

    private PointF GetCatmullRomPos(float t, PointF p0, PointF p1, PointF p2, PointF p3)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float x = 0.5f * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
        float y = 0.5f * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
        return new PointF(x, y);
    }

    private PointF GetNodeCenter(LevelNodeViewModel node, RectF dirtyRect)
    {
        float availableW = dirtyRect.Width - NodeSize;
        float availableH = dirtyRect.Height - NodeSize;
        float left = availableW * (float)node.Bounds.X;
        float top = availableH * (float)node.Bounds.Y;
        return new PointF(left + NodeSize / 2, top + NodeSize / 2);
    }
}