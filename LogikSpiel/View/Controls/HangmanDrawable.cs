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

        Color gallowsColor = Color.FromArgb("#94A3B8");
        Color bodyColor = Color.FromArgb("#E2E8F0");
        Color faceColor = Color.FromArgb("#FCD34D");
        Color shadowColor = Color.FromArgb("#0F172A");

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.StrokeSize = 6 * scale;

        // Galgen zeichnen
        canvas.StrokeColor = gallowsColor;
        canvas.DrawLine(offsetX + 20 * scale, groundY, offsetX + 210 * scale, groundY); // Boden
        canvas.DrawLine(offsetX + 70 * scale, groundY, offsetX + 70 * scale, groundY - 185 * scale); // Pfosten
        canvas.DrawLine(offsetX + 70 * scale, groundY - 185 * scale, offsetX + 155 * scale, groundY - 185 * scale); // Querbalken
        canvas.DrawLine(offsetX + 155 * scale, groundY - 185 * scale, offsetX + 155 * scale, groundY - 165 * scale); // Seil

        float headCenterX = offsetX + 155 * scale;
        float headCenterY = groundY - 140 * scale;
        float headRadius = 22 * scale;

        if (WrongCount >= 1)
        {
            canvas.FillColor = faceColor;
            canvas.FillCircle(headCenterX, headCenterY, headRadius);
            canvas.StrokeColor = bodyColor;
            canvas.DrawCircle(headCenterX, headCenterY, headRadius);

            canvas.FillColor = shadowColor;
            float eyeOffsetX = 7 * scale;
            float eyeOffsetY = 5 * scale;
            float eyeRadius = 2.5f * scale;
            if (WrongCount >= 6)
            {
                float xSize = 4 * scale;
                canvas.StrokeSize = 2.5f * scale;
                canvas.StrokeColor = shadowColor;
                canvas.DrawLine(headCenterX - eyeOffsetX - xSize, headCenterY - eyeOffsetY - xSize, headCenterX - eyeOffsetX + xSize, headCenterY - eyeOffsetY + xSize);
                canvas.DrawLine(headCenterX - eyeOffsetX - xSize, headCenterY - eyeOffsetY + xSize, headCenterX - eyeOffsetX + xSize, headCenterY - eyeOffsetY - xSize);
                canvas.DrawLine(headCenterX + eyeOffsetX - xSize, headCenterY - eyeOffsetY - xSize, headCenterX + eyeOffsetX + xSize, headCenterY - eyeOffsetY + xSize);
                canvas.DrawLine(headCenterX + eyeOffsetX - xSize, headCenterY - eyeOffsetY + xSize, headCenterX + eyeOffsetX + xSize, headCenterY - eyeOffsetY - xSize);
            }
            else
            {
                canvas.FillCircle(headCenterX - eyeOffsetX, headCenterY - eyeOffsetY, eyeRadius);
                canvas.FillCircle(headCenterX + eyeOffsetX, headCenterY - eyeOffsetY, eyeRadius);
            }

            canvas.StrokeColor = shadowColor;
            canvas.StrokeSize = 3 * scale;
            if (WrongCount <= 3)
            {
                canvas.DrawLine(headCenterX - 6 * scale, headCenterY + 6 * scale, headCenterX + 6 * scale, headCenterY + 6 * scale);
            }
            else
            {
                canvas.DrawArc(headCenterX - 9 * scale, headCenterY + 2 * scale, 18 * scale, 14 * scale, 0, 180, false, false);
            }
        }

        canvas.StrokeColor = bodyColor;
        canvas.StrokeSize = 6 * scale;

        if (WrongCount >= 2)
        {
            canvas.DrawLine(headCenterX, headCenterY + headRadius, headCenterX, groundY - 85 * scale); // Körper
        }

        if (WrongCount >= 2)
        {
            canvas.DrawLine(headCenterX, groundY - 120 * scale, headCenterX - 28 * scale, groundY - 100 * scale); // Arm 1
        }

        if (WrongCount >= 3)
        {
            canvas.DrawLine(headCenterX, groundY - 120 * scale, headCenterX + 28 * scale, groundY - 100 * scale); // Arm 2
        }

        if (WrongCount >= 4)
        {
            canvas.DrawLine(headCenterX, groundY - 85 * scale, headCenterX - 18 * scale, groundY - 40 * scale); // Bein 1
        }

        if (WrongCount >= 5)
        {
            canvas.DrawLine(headCenterX, groundY - 85 * scale, headCenterX + 18 * scale, groundY - 40 * scale); // Bein 2
        }

        canvas.RestoreState();
    }
}
