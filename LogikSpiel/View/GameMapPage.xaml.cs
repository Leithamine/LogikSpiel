using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameMapPage : ContentPage, IQueryAttributable
{
    public GameMapPage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<GameMapPageViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not GameMapPageViewModel vm) return;
        if (query.TryGetValue("gameId", out var idObj) && idObj is string id)
        {
            vm.GameId = id;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is GameMapPageViewModel vm)
        {
            BackgroundLottie.IsAnimationEnabled = true;
            vm.RequestScrollToY += OnRequestScroll;

            // Reagieren, wenn sich die Level ändern (z.B. Schwierigkeitswechsel)
            vm.Nodes.CollectionChanged += OnNodesChanged;

            await vm.LoadAsync();

            // WICHTIG: Hier übergeben wir die Wolkenhöhe (180), damit der Strich passt
            PathView.Drawable = new MapPathDrawable(vm.Nodes, 180);
            PathView.Invalidate();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BackgroundLottie.IsAnimationEnabled = false;

        if (BindingContext is GameMapPageViewModel vm)
        {
            vm.RequestScrollToY -= OnRequestScroll;
            vm.Nodes.CollectionChanged -= OnNodesChanged;
        }
    }

    private void OnNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        PathView.Invalidate();
    }

    private void OnRequestScroll(double nodeCenterY)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Warten bis Layout fertig ist
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
            if (contentHeight <= 0 && BindingContext is GameMapPageViewModel vm)
                contentHeight = vm.MapHeight;

            double maxScroll = Math.Max(0, contentHeight - screenHeight);

            // Zentrieren: Node soll in der Mitte sein
            double targetScrollY = nodeCenterY - (screenHeight / 2);

            if (targetScrollY < 0) targetScrollY = 0;
            if (targetScrollY > maxScroll) targetScrollY = maxScroll;

            try
            {
                // false = sofort springen
                await MapScrollView.ScrollToAsync(0, targetScrollY, false);
            }
            catch { }
        });
    }
}