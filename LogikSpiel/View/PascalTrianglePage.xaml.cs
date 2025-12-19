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

    public PascalTrianglePage(PascalTrianglePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        vm.RequestRedraw += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            BoardCanvas?.InvalidateSurface();
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded && BindingContext is PascalTrianglePageViewModel vm)
        {
            await vm.LoadAsync("pascal_triangle", "easy", 1);
            _isLoaded = true;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PascalTrianglePageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() ?? "pascal_triangle" : "pascal_triangle";
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "easy" : "easy";

        int level = 1;
        if (query.TryGetValue("level", out var lvObj))
            int.TryParse(lvObj?.ToString(), out level);

        Dispatcher.Dispatch(async () => await vm.LoadAsync(gameId, difficulty, level));
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

        float sqrt3 = 1.7320508f;
        float pad = Math.Max(12f, Math.Min(width, height) * 0.03f);

        // ✅ kleine Lücke zwischen Hexes: macht es „ruhiger“
        float gap = 0.06f; // 6%

        float availW = Math.Max(1, width - 2 * pad);
        float availH = Math.Max(1, height - 2 * pad);

        // dx = hexW*(1+gap), hexW = sqrt3*s
        // worst row width ~ sqrt3*s * (1 + (rows-1)*(1+gap))
        float sByW = availW / (sqrt3 * (1f + (rows - 1) * (1f + gap)));
        // requiredH ~ 2*s + (rows-1)*1.5*s*(1+gap)
        float sByH = availH / (2f + (rows - 1) * 1.5f * (1f + gap));

        float s = Math.Min(sByW, sByH) * 0.985f;
        s = Math.Clamp(s, 14f, 110f);
        _lastS = s;

        float hexW = sqrt3 * s;
        float dx = hexW * (1f + gap);
        float dy = 1.5f * s * (1f + gap);

        float requiredH = (2f * s) + (rows - 1) * dy + 2f * pad;

        float cx0 = width / 2f;

        // ✅ vertikal zentrieren, damit es nicht „oben klebt“
        float yStart = (height > requiredH)
            ? ((height - requiredH) / 2f) + pad + s
            : pad + s;

        var selected = vm.SelectedPath.ToHashSet();
        var solution = vm.GoalPathSet;

        var culture = CultureInfo.GetCultureInfo("de-DE");

        // ✅ Linien heller + dünner => weniger Augen-Stress
        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(155, 155, 155, 230),
            StrokeWidth = Math.Max(1.2f, s * 0.042f),
            IsAntialias = true
        };

        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { IsAntialias = true, TextAlign = SKTextAlign.Center };

        // Ziel-Farbe
        SKColor goalFill = vm.Goal == "max"
            ? new SKColor(46, 204, 113, 210)
            : new SKColor(231, 76, 60, 210);

        SKColor goalFillStrong = vm.Goal == "max"
            ? new SKColor(46, 204, 113, 255)
            : new SKColor(231, 76, 60, 255);

        for (int r = 0; r < rows; r++)
        {
            float cy = yStart + r * dy;
            float startX = cx0 - (r * dx) / 2f;

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

                SKColor fill = new SKColor(255, 255, 255, 245);

                if (!vm.ShowSolutionOverlay && inSel)
                    fill = goalFill;

                if (vm.ShowSolutionOverlay && inSol)
                    fill = goalFillStrong;

                fillPaint.Color = fill;
                canvas.DrawPath(path, fillPaint);

                // selected border etwas stärker
                if (inSel)
                {
                    stroke.Color = new SKColor(255, 215, 0, 255);
                    stroke.StrokeWidth = Math.Max(2.4f, s * 0.075f);
                }
                else
                {
                    stroke.Color = new SKColor(155, 155, 155, 230);
                    stroke.StrokeWidth = Math.Max(1.2f, s * 0.042f);
                }

                canvas.DrawPath(path, stroke);

                decimal v = game.Triangle[r][c];
                string txt = (v % 1m == 0m)
                    ? ((int)v).ToString(culture)
                    : v.ToString("0.#", culture); // kürzer als 0.0

                // ✅ Text auto-fit (lange Werte werden kleiner)
                float baseSize = s * 0.72f;
                float textSize = baseSize;
                textPaint.TextSize = textSize;

                float maxTextWidth = hexW * 0.78f;
                var bounds = new SKRect();
                textPaint.MeasureText(txt, ref bounds);

                while (bounds.Width > maxTextWidth && textSize > s * 0.45f)
                {
                    textSize *= 0.92f;
                    textPaint.TextSize = textSize;
                    textPaint.MeasureText(txt, ref bounds);
                }

                bool strong = vm.ShowSolutionOverlay ? inSol : inSel;
                textPaint.Color = strong ? SKColors.White : new SKColor(30, 30, 30, 255);

                float textY = cy - (bounds.Top + bounds.Bottom) / 2f;
                canvas.DrawText(txt, cx, textY, textPaint);
            }
        }
    }

    private static SKPath BuildHexPathPointy(float cx, float cy, float s)
    {
        var path = new SKPath();
        for (int i = 0; i < 6; i++)
        {
            double angle = (-Math.PI / 2) + i * (Math.PI / 3);
            float x = cx + (float)(s * Math.Cos(angle));
            float y = cy + (float)(s * Math.Sin(angle));
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }

    private void BoardCanvas_Touch(object sender, SKTouchEventArgs e)
    {
        // nicht doppelt auslösen
        if (e.ActionType == SKTouchAction.Pressed)
        {
            _handledPressId = e.Id;
        }
        else if (e.ActionType == SKTouchAction.Released)
        {
            if (e.Id == _handledPressId)
            {
                e.Handled = true;
                return;
            }
        }
        else return;

        if (BindingContext is not PascalTrianglePageViewModel vm) return;
        if (vm.Game == null) return;

        var p = e.Location;

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

        // nearest fallback
        if (_centers.Count > 0)
        {
            var nearest = _centers
                .Select(kv => (key: kv.Key, d: Dist2(kv.Value, p)))
                .OrderBy(x => x.d)
                .First();

            if (nearest.d <= (_lastS * 0.95f) * (_lastS * 0.95f))
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
