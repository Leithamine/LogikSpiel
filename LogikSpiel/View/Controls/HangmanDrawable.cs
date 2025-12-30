#nullable enable
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View.Controls;

/// <summary>
/// Zeichnet den Hangman - passt sich automatisch an die Box-Größe an
/// </summary>
public sealed class HangmanDrawable : IDrawable
{
    public int WrongCount { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        // Skalierungsfaktor basierend auf Box-Größe
        float scale = Math.Min(w / 300f, h / 250f);

        // Zentrieren
        float offsetX = (w - 300 * scale) / 2;
        float offsetY = (h - 250 * scale) / 2;

        // Galgen Koordinaten (skaliert)
        float groundY = offsetY + 220 * scale;
        float poleX = offsetX + 70 * scale;
        float topY = offsetY + 30 * scale;
        float topX = offsetX + 200 * scale;
        float ropeY = offsetY + 50 * scale;

        // Galgen zeichnen
        canvas.StrokeColor = Color.FromRgba(255, 255, 255, 0.25f);
        canvas.StrokeSize = 6 * scale;
        canvas.StrokeLineCap = LineCap.Round;

        // Boden
        canvas.DrawLine(offsetX + 20 * scale, groundY, offsetX + 280 * scale, groundY);
        // Pfosten
        canvas.DrawLine(poleX, groundY, poleX, topY);
        // Querbalken
        canvas.DrawLine(poleX, topY, topX, topY);
        // Seil
        canvas.StrokeSize = 4 * scale;
        canvas.DrawLine(topX, topY, topX, ropeY + 15 * scale);

        // Figur Position
        float headCx = topX;
        float headCy = ropeY + 45 * scale;
        float headR = 25 * scale;

        // Farben
        var headColor = Color.FromArgb("#FF6B6B");
        var bodyColor = Color.FromArgb("#4ECDC4");
        var limbColor = Color.FromArgb("#9B59B6");
        var white = Color.FromRgba(255, 255, 255, 0.9f);

        int wc = Math.Clamp(WrongCount, 0, 6);

        // Teil 1: Kopf
        if (wc >= 1)
        {
            canvas.StrokeSize = 4 * scale;
            canvas.StrokeColor = headColor;
            canvas.FillColor = Color.FromRgba(255, 255, 255, 0.1f);
            canvas.FillCircle(headCx, headCy, headR);
            canvas.DrawCircle(headCx, headCy, headR);

            DrawFace(canvas, wc, headCx, headCy, headR, white, scale);
        }

        float neckY = headCy + headR + 5 * scale;
        float bodyBottomY = neckY + 70 * scale;

        // Teil 2: Körper
        if (wc >= 2)
        {
            canvas.StrokeSize = 5 * scale;
            canvas.StrokeColor = bodyColor;
            canvas.DrawLine(headCx, neckY, headCx, bodyBottomY);
        }

        float armY = neckY + 15 * scale;

        // Teil 3: Linker Arm
        if (wc >= 3)
        {
            canvas.StrokeSize = 4 * scale;
            canvas.StrokeColor = limbColor;
            canvas.DrawLine(headCx, armY, headCx - 40 * scale, armY + 40 * scale);
        }

        // Teil 4: Rechter Arm
        if (wc >= 4)
        {
            canvas.StrokeSize = 4 * scale;
            canvas.StrokeColor = limbColor;
            canvas.DrawLine(headCx, armY, headCx + 40 * scale, armY + 40 * scale);
        }

        // Teil 5: Linkes Bein
        if (wc >= 5)
        {
            canvas.StrokeSize = 5 * scale;
            canvas.StrokeColor = limbColor;
            canvas.DrawLine(headCx, bodyBottomY, headCx - 30 * scale, bodyBottomY + 50 * scale);
        }

        // Teil 6: Rechtes Bein
        if (wc >= 6)
        {
            canvas.StrokeSize = 5 * scale;
            canvas.StrokeColor = limbColor;
            canvas.DrawLine(headCx, bodyBottomY, headCx + 30 * scale, bodyBottomY + 50 * scale);
        }

        canvas.RestoreState();
    }

    private static void DrawFace(ICanvas canvas, int wc, float cx, float cy, float r, Color white, float scale)
    {
        canvas.StrokeColor = white;
        canvas.StrokeSize = 2 * scale;
        canvas.FillColor = white;

        bool dead = wc >= 6;
        bool sad = wc >= 4;

        float eyeY = cy - 5 * scale;
        float eyeSpacing = 8 * scale;
        float eyeSize = 3 * scale;

        if (!dead)
        {
            // Normale Augen
            canvas.FillCircle(cx - eyeSpacing, eyeY, eyeSize);
            canvas.FillCircle(cx + eyeSpacing, eyeY, eyeSize);
        }
        else
        {
            // X Augen
            float xSize = 5 * scale;
            canvas.DrawLine(cx - eyeSpacing - xSize, eyeY - xSize, cx - eyeSpacing + xSize, eyeY + xSize);
            canvas.DrawLine(cx - eyeSpacing + xSize, eyeY - xSize, cx - eyeSpacing - xSize, eyeY + xSize);
            canvas.DrawLine(cx + eyeSpacing - xSize, eyeY - xSize, cx + eyeSpacing + xSize, eyeY + xSize);
            canvas.DrawLine(cx + eyeSpacing + xSize, eyeY - xSize, cx + eyeSpacing - xSize, eyeY + xSize);
        }

        // Mund
        float mouthY = cy + 8 * scale;
        float mouthWidth = 10 * scale;

        if (!dead && !sad)
        {
            // Neutral
            canvas.DrawLine(cx - mouthWidth, mouthY, cx + mouthWidth, mouthY);
        }
        else if (sad && !dead)
        {
            // Traurig
            canvas.DrawArc(cx - mouthWidth, mouthY, mouthWidth * 2, 10 * scale, 0, 180, false, false);
        }
        else
        {
            // Tot - offener Mund
            canvas.DrawEllipse(cx - 5 * scale, mouthY, 10 * scale, 8 * scale);
        }
    }
}
