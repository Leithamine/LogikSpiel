#nullable enable
using LogikSpiel.ViewModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using Controls = Microsoft.Maui.Controls;

namespace LogikSpiel.View;

public partial class NumberRainPage : Controls.ContentPage, Controls.IQueryAttributable
{
    private readonly Dictionary<string, Controls.View> _views = new();
    private readonly Random _random = new();
    private bool _isLoaded;
    private double _canvasWidth;
    private double _canvasHeight;

    public NumberRainPage(NumberRainPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        RainCanvas.SizeChanged += (_, __) =>
        {
            _canvasWidth = RainCanvas.Width;
            _canvasHeight = RainCanvas.Height;
        };

        vm.Spawned += OnSpawned;
        vm.ClearRequested += ClearRain;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_isLoaded || BindingContext is not NumberRainPageViewModel vm) return;
        _isLoaded = true;

        string diff = query.TryGetValue("difficulty", out var d) ? d?.ToString() ?? "easy" : "easy";
        int level = query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var lv) ? lv : 1;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            await vm.LoadAsync("number_rain", diff, level);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is NumberRainPageViewModel vm)
            vm.Stop();
        ClearRain();
    }

    private void OnSpawned(NumberRainPageViewModel.NumberRainSpawn spawn)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var button = BuildNumberButton(spawn);
            _views[spawn.Id] = button;
            RainCanvas.Children.Add(button);

            _ = AnimateFallAsync(spawn, button);
        });
    }

    private Controls.Button BuildNumberButton(NumberRainPageViewModel.NumberRainSpawn spawn)
    {
        var button = new Controls.Button
        {
            Text = spawn.Value.ToString(),
            BackgroundColor = Color.FromArgb("#1E88E5"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 16,
            HeightRequest = 42,
            WidthRequest = 68,
            Padding = new Thickness(0)
        };

        double maxX = Math.Max(0, _canvasWidth - 70);
        double x = _random.NextDouble() * maxX;

        Controls.AbsoluteLayout.SetLayoutBounds(button, new Rect(x, -50, 68, 42));
        Controls.AbsoluteLayout.SetLayoutFlags(button, Controls.AbsoluteLayoutFlags.None);

        button.Clicked += async (_, __) => await HandleSelectionAsync(spawn.Id);

        return button;
    }

    private async Task AnimateFallAsync(NumberRainPageViewModel.NumberRainSpawn spawn, Controls.View view)
    {
        double endY = _canvasHeight + 60;
        await view.TranslateTo(0, endY, (uint)spawn.FallDurationMs, Easing.Linear);

        if (_views.ContainsKey(spawn.Id))
        {
            if (BindingContext is NumberRainPageViewModel vm)
                await vm.HandleMissAsync(spawn.Id);

            RemoveView(spawn.Id);
        }
    }

    private async Task HandleSelectionAsync(string id)
    {
        if (BindingContext is not NumberRainPageViewModel vm) return;

        await vm.SelectNumberAsync(id);
        RemoveView(id);
    }

    private void RemoveView(string id)
    {
        if (!_views.TryGetValue(id, out var view)) return;

        view.CancelAnimations();
        RainCanvas.Children.Remove(view);
        _views.Remove(id);
    }

    private void ClearRain()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var view in _views.Values)
            {
                view.CancelAnimations();
                RainCanvas.Children.Remove(view);
            }
            _views.Clear();
        });
    }
}
