#nullable enable
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameMapPage : ContentPage, IQueryAttributable
{
    private readonly GameMapPageViewModel _vm;

    public GameMapPage(GameMapPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("gameId", out var idObj) && idObj is string id)
            _vm.GameId = id;

        if (_vm.GameId is { Length: > 0 } gameId &&
            query.TryGetValue("difficulty", out var difficultyObj) &&
            !string.IsNullOrWhiteSpace(difficultyObj?.ToString()))
        {
            Preferences.Set($"LAST_DIFF_{gameId}", difficultyObj!.ToString()!);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        BackgroundLottie.IsAnimationEnabled = true;

        _vm.RequestScrollToY += OnRequestScroll;
        _vm.Nodes.CollectionChanged += OnNodesChanged;

        await _vm.LoadAsync();

        // Wolkenhöhe 180 wie vorher
        PathView.Drawable = new MapPathDrawable(_vm.Nodes, 180);
        PathView.Invalidate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BackgroundLottie.IsAnimationEnabled = false;

        _vm.RequestScrollToY -= OnRequestScroll;
        _vm.Nodes.CollectionChanged -= OnNodesChanged;
    }

    private void OnNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => PathView.Invalidate();

    private void OnRequestScroll(double nodeCenterY)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            double screenHeight = MapScrollView.Height;
            int retries = 0;
            while (screenHeight <= 0 && retries < 20)
            {
                await Task.Delay(50);
                screenHeight = MapScrollView.Height;
                retries++;
            }
            if (screenHeight <= 0) screenHeight = 800;

            double contentHeight = MapScrollView.ContentSize.Height;
            if (contentHeight <= 0) contentHeight = _vm.MapHeight;

            double maxScroll = Math.Max(0, contentHeight - screenHeight);

            double targetScrollY = nodeCenterY - (screenHeight / 2);
            if (targetScrollY < 0) targetScrollY = 0;
            if (targetScrollY > maxScroll) targetScrollY = maxScroll;

            try { await MapScrollView.ScrollToAsync(0, targetScrollY, false); }
            catch { }
        });
    }
}
