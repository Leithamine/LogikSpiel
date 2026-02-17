#nullable enable
using System.Globalization;
using LogikSpiel.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace LogikSpiel.View;

public partial class PascalTrianglePage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;

    private readonly Dictionary<(int r, int c), SKPath> _hitPaths = new();
    private readonly Dictionary<(int r, int c), SKPoint> _centers = new();

    private int _lastW, _lastH, _lastRows;
    private float _lastS;
    private long _handledPressId = -1;

    private Action? _redrawHandler;

    public PascalTrianglePage(PascalTrianglePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        _redrawHandler = () =>
        {
            if (BoardCanvas != null)
                MainThread.BeginInvokeOnMainThread(() => BoardCanvas.InvalidateSurface());
        };
        vm.RequestRedraw += _redrawHandler;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded && BindingContext is PascalTrianglePageViewModel vm)
        {
            _ = vm.LoadAsync("pascal_triangle", "easy", 1);
            _isLoaded = true;
        }

        BoardCanvas?.InvalidateSurface();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (BindingContext is PascalTrianglePageViewModel vm && _redrawHandler != null)
        {
            vm.RequestRedraw -= _redrawHandler;
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && height > 0)
            BoardCanvas?.InvalidateSurface();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PascalTrianglePageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() ?? "pascal_triangle" : "pascal_triangle";
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "easy" : "easy";

        int level = 1;
        if (query.TryGetValue("level", out var lvObj) && int.TryParse(lvObj?.ToString(), out var parsedLevel))
            level = parsedLevel;

        Dispatcher.Dispatch(async () =>
        {
            const int maxLevel = 10000;
            if (level > maxLevel) level = maxLevel;
            await vm.LoadAsync(gameId, difficulty, level);
        });
    }

    private void BoardCanvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (BindingContext is not PascalTrianglePageViewModel vm) return;
        if (vm.Game == null) return;

        int w = e.Info.Width;
        int h = e.Info.Height;
        int rows = vm.Game.Rows;

        if (w != _lastW || h != _lastH || rows != _lastRows)
        {
            _lastW = w;
            _lastH = h;
            _lastRows = rows;
            _hitPaths.Clear();
            _centers.Clear();
        }

        DrawTriangle(canvas, vm, w, h);
    }

    private void DrawTriangle(SKCanvas canvas, PascalTrianglePageViewModel vm, int width, int height)
    {
        var game = vm.Game!;
        int rows = game.Rows;
        if (rows <= 0) return;

        const float sqrt3 = 1.7320508f;

        // ✅ MINIMALES Padding - nur ein kleiner Rand
        float padH = 8f;
        float padV = 8f;

        // ✅ Nutzbarer Bereich = fast der gesamte Bildschirm
        float availW = width - 2 * padH;
        float availH = height - 2 * padV;

        // ✅ Gap zwischen Hexagons (sehr klein für maximale Ausnutzung)
        float gap = 0.04f;

        // ✅ BERECHNUNG DER OPTIMALEN HEX-GRÖßE
        // Die unterste Reihe hat `rows` Hexagons
        // Breite eines Hexagons = sqrt(3) * size
        // Gesamtbreite der untersten Reihe = rows * hexW + (rows-1) * gap * hexW
        //                                  = hexW * (rows + (rows-1) * gap)
        //                                  = sqrt3 * s * (rows * (1 + gap) - gap)

        float widthFactor = sqrt3 * (rows * (1f + gap) - gap);
        float sByW = availW / widthFactor;

        // Höhe: rows Hexagons vertikal
        // Erste Reihe braucht 2*s (volle Höhe)
        // Jede weitere Reihe fügt 1.5*s hinzu (mit gap)
        // Gesamthöhe = 2*s + (rows-1) * 1.5*s * (1+gap)
        //            = s * (2 + (rows-1) * 1.5 * (1+gap))

        float heightFactor = 2f + (rows - 1) * 1.5f * (1f + gap);
        float sByH = availH / heightFactor;

        // ✅ Nimm das Minimum - damit passt es in beide Richtungen
        float s = Math.Min(sByW, sByH);

        // ✅ KEINE Obergrenze - lass es so groß wie möglich werden!
        // Nur eine Untergrenze für Lesbarkeit
        s = Math.Max(s, 18f);

        _lastS = s;

        float hexW = sqrt3 * s;
        float dx = hexW * (1f + gap);
        float dy = 1.5f * s * (1f + gap);

        // ✅ ZENTRIERUNG: Berechne tatsächliche Größe und zentriere
        float actualWidth = (rows - 1) * dx + hexW;
        float actualHeight = (rows - 1) * dy + 2 * s;

        float xCenter = width / 2f;
        float yStart = (height - actualHeight) / 2f + s;

        var selected = vm.SelectedPath.ToHashSet();
        var solution = vm.GoalPathSet;
        var culture = CultureInfo.GetCultureInfo("de-DE");

        // ✅ Stroke proportional zur Größe
        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(130, 130, 130, 220),
            StrokeWidth = Math.Max(1.2f, s * 0.04f),
            IsAntialias = true
        };

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { IsAntialias = true };

        // ✅ BOLD FONT für bessere Lesbarkeit
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var font = new SKFont(typeface)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true
        };

        SKColor goalFill = vm.Goal == "max"
            ? new SKColor(46, 204, 113, 215)
            : new SKColor(231, 76, 60, 215);

        SKColor goalFillStrong = vm.Goal == "max"
            ? new SKColor(46, 204, 113, 255)
            : new SKColor(231, 76, 60, 255);

        using var shadowPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0, 0, 0, 40),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
        };

        // ✅ Text-Breite: 75% der Hex-Breite
        float maxTextWidth = hexW * 0.75f;

        for (int r = 0; r < rows; r++)
        {
            float cy = yStart + r * dy;

            // ✅ Zentrierte X-Position für jede Reihe
            float rowWidth = r * dx;
            float startX = xCenter - rowWidth / 2f;

            for (int c = 0; c <= r; c++)
            {
                float cx = startX + c * dx;
                var key = (r, c);

                _centers[key] = new SKPoint(cx, cy);

                if (!_hitPaths.TryGetValue(key, out var path))
                {
                    path = BuildHexPathPointy(cx, cy, s);
                    _hitPaths[key] = path;
                }

                bool inSel = selected.Contains(key);
                bool inSol = solution.Contains(key);

                SKColor fill = new SKColor(255, 255, 255, 250);
                if (!vm.ShowSolutionOverlay && inSel) fill = goalFill;
                if (vm.ShowSolutionOverlay && inSol) fill = goalFillStrong;

                // Shadow
                canvas.Save();
                canvas.Translate(2f, 2f);
                canvas.DrawPath(path, shadowPaint);
                canvas.Restore();

                fillPaint.Color = fill;
                canvas.DrawPath(path, fillPaint);
                canvas.DrawPath(path, stroke);

                decimal value = game.Triangle[r][c];
                string text = FormatNumber(value, culture);

                // ✅ Font-Größe proportional zur Hex-Größe
                float fontSize = Math.Max(11f, s * 0.50f);
                font.Size = fontSize;

                // Auto-fit wenn Text zu breit
                float measured = font.MeasureText(text);
                if (measured > maxTextWidth && measured > 0)
                {
                    float scale = maxTextWidth / measured;
                    font.Size = Math.Max(9f, fontSize * scale);
                }

                bool isDark = inSel || (vm.ShowSolutionOverlay && inSol);
                textPaint.Color = isDark ? SKColors.White : new SKColor(30, 30, 30);

                SKRect bounds;
                font.MeasureText(text, out bounds);

                float textX = cx - bounds.MidX;
                float textY = cy - bounds.MidY;

                canvas.DrawText(text, textX, textY, font, textPaint);
            }
        }
    }

    private static string FormatNumber(decimal value, CultureInfo culture)
    {
        if (value == Math.Truncate(value))
            return ((int)value).ToString(culture);

        return value.ToString("0.#", culture);
    }

    private static SKPath BuildHexPathPointy(float cx, float cy, float size)
    {
        var path = new SKPath();

        for (int i = 0; i < 6; i++)
        {
            float angle = (float)(Math.PI / 3 * i - Math.PI / 2);
            float x = cx + size * (float)Math.Cos(angle);
            float y = cy + size * (float)Math.Sin(angle);

            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }

        path.Close();
        return path;
    }

    private void BoardCanvas_Touch(object? sender, SKTouchEventArgs e)
    {
        if (BindingContext is not PascalTrianglePageViewModel vm) return;
        if (vm.Game == null) return;

        if (e.ActionType == SKTouchAction.Pressed)
        {
            _handledPressId = e.Id;
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Released && e.Id == _handledPressId)
        {
            _handledPressId = -1;
            HandleTap(e.Location, vm, e);
            return;
        }

        e.Handled = true;
    }

    private void HandleTap(SKPoint p, PascalTrianglePageViewModel vm, SKTouchEventArgs e)
    {
        foreach (var kv in _hitPaths)
        {
            if (kv.Value.Contains(p.X, p.Y))
            {
                vm.TapCell(kv.Key.r, kv.Key.c);
                BoardCanvas?.InvalidateSurface();
                e.Handled = true;
                return;
            }
        }

        if (_centers.Count > 0)
        {
            var nearest = _centers
                .Select(kv => (key: kv.Key, d: Dist2(kv.Value, p)))
                .OrderBy(x => x.d)
                .First();

            float tolerance = _lastS * 0.95f;
            if (nearest.d <= tolerance * tolerance)
            {
                vm.TapCell(nearest.key.r, nearest.key.c);
                BoardCanvas?.InvalidateSurface();
                e.Handled = true;
                return;
            }
        }

        e.Handled = true;
    }

    private static float Dist2(SKPoint a, SKPoint b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
