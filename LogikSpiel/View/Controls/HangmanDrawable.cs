#nullable enable
using System;
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View.Controls;

public sealed class HangmanDrawable : IDrawable
{
    public int WrongCount { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        float w = dirtyRect.Width;
        float h = dirtyRect.Height;

        // Skalierung
        float scale = Math.Min(w / 300f, h / 250f);
        float offsetX = (w - 300 * scale) / 2f;
        float offsetY = (h - 250 * scale) / 2f;

        float groundY = offsetY + 220 * scale;
        float poleX = offsetX + 70 * scale;
        float topY = offsetY + 30 * scale;
        float topX = offsetX + 200 * scale;
        float ropeY = offsetY + 50 * scale;

        int wc = Math.Clamp(WrongCount, 0, 6);

        // Farben: für hellen Hintergrund -> dunklere Linien
        var gallows = Color.FromRgba(20, 20, 20, 0.55f);
        var gallowsLight = Color.FromRgba(20, 20, 20, 0.35f);

        var headStroke = Color.FromArgb("#E74C3C"); // kräftiges Rot
        var bodyStroke = Color.FromArgb("#2C3E50"); // dunkel
        var limbStroke = Color.FromArgb("#6C5CE7"); // violett, aber sichtbar

        var face = Color.FromRgba(10, 10, 10, 0.85f);

        // Galgen
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = 6 * scale;
        canvas.StrokeColor = gallows;

        canvas.DrawLine(offsetX + 20 * scale, groundY, offsetX + 280 * scale, groundY);
        canvas.DrawLine(poleX, groundY, poleX, topY);
        canvas.DrawLine(poleX, topY, topX, topY);

        canvas.StrokeSize = 4 * scale;
        canvas.StrokeColor = gallowsLight;
        canvas.DrawLine(topX, topY, topX, ropeY + 15 * scale);

        // Figur
        float headCx = topX;
        float headCy = ropeY + 45 * scale;
        float headR = 25 * scale;

        float neckY = headCy + headR + 5 * scale;
        float bodyBottomY = neckY + 70 * scale;
        float armY = neckY + 15 * scale;

        // Kopf
        if (wc >= 1)
        {
            canvas.StrokeSize = 4 * scale;
            canvas.StrokeColor = headStroke;
            canvas.FillColor = Color.FromRgba(255, 255, 255, 0.55f);
            canvas.FillCircle(headCx, headCy, headR);
            canvas.DrawCircle(headCx, headCy, headR);

            DrawFace(canvas, wc, headCx, headCy, headR, face, scale);
        }

        // Körper
        if (wc >= 2)
        {
            canvas.StrokeSize = 5 * scale;
            canvas.StrokeColor = bodyStroke;
            canvas.DrawLine(headCx, neckY, headCx, bodyBottomY);
        }

        // Arme
        if (wc >= 3)
        {
            canvas.StrokeSize = 4 * scale;
            canvas.StrokeColor = limbStroke;
            canvas.DrawLine(headCx, armY, headCx - 40 * scale, armY + 40 * scale);
        }

        if (wc >= 4)
        {
            canvas.StrokeSize = 4 * scale;
            canvas.StrokeColor = limbStroke;
            canvas.DrawLine(headCx, armY, headCx + 40 * scale, armY + 40 * scale);
        }

        // Beine
        if (wc >= 5)
        {
            canvas.StrokeSize = 5 * scale;
            canvas.StrokeColor = limbStroke;
            canvas.DrawLine(headCx, bodyBottomY, headCx - 30 * scale, bodyBottomY + 50 * scale);
        }

        if (wc >= 6)
        {
            canvas.StrokeSize = 5 * scale;
            canvas.StrokeColor = limbStroke;
            canvas.DrawLine(headCx, bodyBottomY, headCx + 30 * scale, bodyBottomY + 50 * scale);
        }

        canvas.RestoreState();
    }

    private static void DrawFace(ICanvas canvas, int wc, float cx, float cy, float r, Color face, float scale)
    {
        canvas.StrokeColor = face;
        canvas.FillColor = face;

        bool dead = wc >= 6;
        bool sad = wc >= 4;

        float eyeY = cy - 6 * scale;
        float eyeSpacing = 9 * scale;

        if (!dead)
        {
            // klare Augen
            canvas.FillCircle(cx - eyeSpacing, eyeY, 2.8f * scale);
            canvas.FillCircle(cx + eyeSpacing, eyeY, 2.8f * scale);
        }
        else
        {
            // X Augen
            canvas.StrokeSize = 2.2f * scale;
            float xSize = 5f * scale;
            canvas.DrawLine(cx - eyeSpacing - xSize, eyeY - xSize, cx - eyeSpacing + xSize, eyeY + xSize);
            canvas.DrawLine(cx - eyeSpacing + xSize, eyeY - xSize, cx - eyeSpacing - xSize, eyeY + xSize);
            canvas.DrawLine(cx + eyeSpacing - xSize, eyeY - xSize, cx + eyeSpacing + xSize, eyeY + xSize);
            canvas.DrawLine(cx + eyeSpacing + xSize, eyeY - xSize, cx + eyeSpacing - xSize, eyeY + xSize);
        }

        // Mund
        canvas.StrokeSize = 2.4f * scale;
        float mouthY = cy + 10 * scale;
        float mouthW = 11 * scale;

        if (!dead && !sad)
        {
            canvas.DrawLine(cx - mouthW, mouthY, cx + mouthW, mouthY);
        }
        else if (sad && !dead)
        {
            canvas.DrawArc(cx - mouthW, mouthY - 2 * scale, mouthW * 2, 10 * scale, 0, 180, false, false);
        }
        else
        {
            canvas.DrawEllipse(cx - 5 * scale, mouthY - 2 * scale, 10 * scale, 8 * scale);
        }
    }
}
