// LogikSpiel/View/Controls/HangmanDrawable.cs
using Microsoft.Maui.Graphics;

namespace LogikSpiel.View.Controls;

public sealed class HangmanDrawable : IDrawable
{
    public int WrongCount { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        float scale = Math.Min(dirtyRect.Width / 300f, dirtyRect.Height / 250f);
        float offsetX = (dirtyRect.Width - 300 * scale) / 2f;
        float groundY = (dirtyRect.Height + 220 * scale) / 2f;

        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 5 * scale;

        // Galgen zeichnen
        canvas.DrawLine(offsetX + 20 * scale, groundY, offsetX + 200 * scale, groundY); // Boden
        if (WrongCount >= 1) canvas.DrawLine(offsetX + 70 * scale, groundY, offsetX + 70 * scale, groundY - 180 * scale); // Balken
        if (WrongCount >= 2) canvas.DrawCircle(offsetX + 150 * scale, groundY - 150 * scale, 20 * scale); // Kopf
        if (WrongCount >= 3) canvas.DrawLine(offsetX + 150 * scale, groundY - 130 * scale, offsetX + 150 * scale, groundY - 80 * scale); // Körper
        if (WrongCount >= 4) canvas.DrawLine(offsetX + 150 * scale, groundY - 120 * scale, offsetX + 120 * scale, groundY - 100 * scale); // Arm 1
        if (WrongCount >= 5) canvas.DrawLine(offsetX + 150 * scale, groundY - 120 * scale, offsetX + 180 * scale, groundY - 100 * scale); // Arm 2
        if (WrongCount >= 6)
        {
            canvas.DrawLine(offsetX + 150 * scale, groundY - 80 * scale, offsetX + 130 * scale, groundY - 40 * scale); // Bein 1
            canvas.DrawLine(offsetX + 150 * scale, groundY - 80 * scale, offsetX + 170 * scale, groundY - 40 * scale); // Bein 2
        }

        canvas.RestoreState();
    }
}