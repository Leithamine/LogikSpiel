#nullable enable
using System.ComponentModel;
using LogikSpiel.Core;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class PuzzlePage : ContentPage, IQueryAttributable
{
    private static readonly bool IsAndroid = DeviceInfo.Platform == DevicePlatform.Android;
    private bool _isLoaded;
    private readonly Dictionary<int, Entry> _entryByIndex = new();
    private readonly PuzzlePageViewModel _vm;
    private CancellationTokenSource? _celebrationCts;
    private CancellationTokenSource? _professorAnimCts;
    private CancellationTokenSource? _speechBubbleAnimCts;
    private static readonly Random RewardRandom = new();

    public PuzzlePage(PuzzlePageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        _vm.PropertyChanged += Vm_PropertyChanged;
    }

    // Backward-compatible aliases: keep legacy code references compilable
    // if older branches still reference the previous tail element names.
    private VisualElement SpeechTailPrimary => SpeechTailGroup;
    private VisualElement SpeechTailSecondary => SpeechTailGroup;

    private async void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (BindingContext is not PuzzlePageViewModel vm)
            return;

        if (e.PropertyName == nameof(PuzzlePageViewModel.ShowProfessor))
        {
            if (vm.ShowProfessor && SpeechBubble.Opacity == 0)
            {
                await ShowBubbleAsync();
                SetProfessorText(vm.ProfessorMessage);

                if (!vm.IsCelebrating)
                    await ShakeLockAsync();
            }
            else if (!vm.ShowProfessor && SpeechBubble.Opacity > 0)
            {
                await HideBubbleAsync();
            }
        }

        if (e.PropertyName == nameof(PuzzlePageViewModel.ProfessorMessage) && vm.ShowProfessor)
            SetProfessorText(vm.ProfessorMessage);

        if (e.PropertyName != nameof(PuzzlePageViewModel.IsCelebrating))
            return;

        if (!vm.IsCelebrating)
            return;

        CancelCelebrationAnimation();
        _celebrationCts = new CancellationTokenSource();
        var ct = _celebrationCts.Token;

        await Task.Delay(100, ct);

        try
        {
            ct.ThrowIfCancellationRequested();
            await Task.WhenAll(
                CelebrateLockAsync(),
                PlayCoinRewardAnimationAsync(vm.PendingCoinReward, ct));

            vm.FinalizePendingCoinReward();
        }
        catch (OperationCanceledException)
        {
            // Seite wurde verlassen
        }
    }


    private async Task PlayCoinRewardAnimationAsync(int coinAmount, CancellationToken ct)
    {
        if (coinAmount <= 0)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RewardOverlay.Children.Clear();
            RewardOverlay.IsVisible = true;
        });

        try
        {
            int animatedCoins = coinAmount;
            var center = GetElementCenter(RootGrid);
            var target = GetElementCenter(CoinCounterBadge);
            var puzzleBounds = GetElementBounds(PuzzleArea);

            var tasks = new List<Task>(animatedCoins);

            for (int i = 0; i < animatedCoins; i++)
            {
                ct.ThrowIfCancellationRequested();
                tasks.Add(AnimateSingleCoinAsync(puzzleBounds, center, target, ct));
            }

            await Task.WhenAll(tasks);
            await AnimateCoinCounterAsync(_vm, _vm.Coins, _vm.PendingCoinTarget, ct);
            await BounceCounterAsync(ct);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RewardOverlay.Children.Clear();
                RewardOverlay.IsVisible = false;
            });
        }
    }

    private async Task AnimateSingleCoinAsync(Rect puzzleBounds, Point center, Point target, CancellationToken ct)
    {
        var coin = new Image
        {
            Source = "coin.png",
            WidthRequest = 32,
            HeightRequest = 32,
            Opacity = 1,
            Scale = 1
        };

        double startX = RewardRandom.NextDouble() * Math.Max(1, puzzleBounds.Width - 32) + puzzleBounds.X;
        double startY = RewardRandom.NextDouble() * Math.Max(1, puzzleBounds.Height - 32) + puzzleBounds.Y;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RewardOverlay.Children.Add(coin);
            AbsoluteLayout.SetLayoutBounds(coin, new Rect(startX, startY, 32, 32));
        });

        await Task.Delay(RewardRandom.Next(0, 151), ct);

        await Task.WhenAll(
            coin.TranslateToAsync(center.X - startX, center.Y - startY, 300, Easing.CubicOut),
            coin.ScaleToAsync(1.2, 200, Easing.CubicOut));

        ct.ThrowIfCancellationRequested();

        await Task.WhenAll(
            coin.TranslateToAsync(target.X - startX, target.Y - startY, 500, Easing.CubicIn),
            coin.ScaleToAsync(0.3, 500, Easing.CubicIn),
            coin.FadeToAsync(0, 400, Easing.CubicIn));
    }

    private async Task AnimateCoinCounterAsync(PuzzlePageViewModel vm, int start, int end, CancellationToken ct)
    {
        if (end <= start)
            return;

        int steps = end - start;
        int delayMs = steps > 20 ? 20 : 40;

        for (int i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            int value = start + i;
            await MainThread.InvokeOnMainThreadAsync(() => vm.Coins = value);
            await Task.Delay(delayMs, ct);
        }

        await MainThread.InvokeOnMainThreadAsync(() => vm.Coins = vm.PendingCoinTarget);
    }

    private async Task BounceCounterAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await CoinCounterIcon.ScaleToAsync(1.2, 120, Easing.CubicOut);
            await CoinCounterIcon.ScaleToAsync(1.0, 120, Easing.CubicIn);
        });
    }

    private static Rect GetElementBounds(VisualElement element)
    {
        var topLeft = GetAbsolutePosition(element);
        return new Rect(topLeft.X, topLeft.Y, element.Width, element.Height);
    }

    private static Point GetElementCenter(VisualElement element)
    {
        var topLeft = GetAbsolutePosition(element);
        return new Point(topLeft.X + (element.Width / 2), topLeft.Y + (element.Height / 2));
    }

    private static Point GetAbsolutePosition(VisualElement element)
    {
        double x = element.X;
        double y = element.Y;

        var parent = element.Parent as VisualElement;
        while (parent != null)
        {
            x += parent.X;
            y += parent.Y;
            parent = parent.Parent as VisualElement;
        }

        return new Point(x, y);
    }

    private async Task ShowBubbleAsync()
    {
        SpeechBubble.Scale = IsAndroid ? 1.0 : 0.6;
        SpeechBubble.Opacity = 0;

        SpeechTailGroup.Scale = IsAndroid ? 1.0 : 0.7;
        SpeechTailGroup.Opacity = 0;

        if (IsAndroid)
        {
            await Task.WhenAll(
                SpeechBubble.FadeToAsync(1, 150),
                SpeechTailGroup.FadeToAsync(1, 140)
            );

            return;
        }

        await Task.WhenAll(
            SpeechBubble.FadeToAsync(1, 150),
            SpeechBubble.ScaleToAsync(1.05, 180, Easing.CubicOut),
            SpeechTailGroup.FadeToAsync(1, 140),
            SpeechTailGroup.ScaleToAsync(1.0, 170, Easing.CubicOut)
        );

        await Task.WhenAll(
            SpeechBubble.ScaleToAsync(1.0, 80),
            SpeechTailSecondary.FadeToAsync(1, 120),
            SpeechTailSecondary.ScaleToAsync(1.0, 140, Easing.CubicOut)
        );
    }

    private void SetProfessorText(string text)
    {
        ProfessorLabel.Text = text ?? string.Empty;
    }


    //private void StartProfessorTalkingAnimation()
    //{
    //    StopProfessorTalkingAnimation();

    //    _speechBubbleAnimCts = new CancellationTokenSource();
    //    var bubbleCt = _speechBubbleAnimCts.Token;

    //    _ = MainThread.InvokeOnMainThreadAsync(async () =>
    //    {
    //        try
    //        {
    //            while (!bubbleCt.IsCancellationRequested)
    //            {
    //                await SpeechBubble.ScaleTo(1.02, 110, Easing.CubicInOut);
    //                await SpeechTail.TranslateTo(-2, 0, 110, Easing.CubicInOut);
    //                await SpeechBubble.ScaleTo(1.0, 110, Easing.CubicInOut);
    //                await SpeechTail.TranslateTo(0, 0, 110, Easing.CubicInOut);
    //            }
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            // expected on close/navigation
    //        }
    //    });
    //}

    private void StopProfessorTalkingAnimation()
    {
        _professorAnimCts?.Cancel();
        _professorAnimCts?.Dispose();
        _professorAnimCts = null;

        _speechBubbleAnimCts?.Cancel();
        _speechBubbleAnimCts?.Dispose();
        _speechBubbleAnimCts = null;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProfessorImage.CancelAnimations();
            SpeechBubble.CancelAnimations();
            SpeechTailGroup.CancelAnimations();

            ProfessorImage.Scale = 1;
            ProfessorImage.Rotation = 0;
            SpeechBubble.Scale = 1;
            SpeechTailGroup.Scale = 1;
            SpeechTailGroup.Opacity = 1;
        });
    }

    private async Task CelebrateLockAsync()
    {
        await LockImage.ScaleToAsync(1.2, 200);
        await LockImage.ScaleToAsync(1.0, 120);
        await LockImage.RotateToAsync(10, 80);
        await LockImage.RotateToAsync(-10, 80);
        await LockImage.RotateToAsync(0, 80);
    }
    private async Task HideBubbleAsync()
    {
        try
        {
            if (IsAndroid)
            {
                await Task.WhenAll(
                    SpeechBubble.FadeToAsync(0, 180, Easing.CubicIn),
                    SpeechTailGroup.FadeToAsync(0, 120, Easing.CubicIn)
                );
            }
            else
            {
                await Task.WhenAll(
                    SpeechBubble.FadeToAsync(0, 180, Easing.CubicIn),
                    SpeechBubble.ScaleToAsync(0.85, 180, Easing.CubicIn),
                    SpeechTailGroup.FadeToAsync(0, 120, Easing.CubicIn),
                    SpeechTailGroup.ScaleToAsync(0.8, 120, Easing.CubicIn)
                );
            }
        }
        catch { }

        SpeechBubble.Opacity = 0;
        SpeechBubble.Scale = 1;
        SpeechTailGroup.Opacity = 0;
        SpeechTailGroup.Scale = 1;
    }
    private async Task ShakeLockAsync()
    {
        const int shakeDistance = 12;
        const uint shakeSpeed = 50;

        for (int i = 0; i < 4; i++)
        {
            await LockImage.TranslateToAsync(-shakeDistance, 0, shakeSpeed);
            await LockImage.TranslateToAsync(shakeDistance, 0, shakeSpeed);
        }

        await LockImage.TranslateToAsync(0, 0, shakeSpeed);
    }

    private void DigitEntry_Loaded(object? sender, EventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not DigitInputViewModel dvm) return;

        _entryByIndex[dvm.Index] = entry;
    }

    private void DigitEntry_Unloaded(object? sender, EventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not DigitInputViewModel dvm) return;

        _entryByIndex.Remove(dvm.Index);
    }


    private void DigitEntry_Focused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.Parent is not Border border) return;

        border.Stroke = GetBrushResource("C_Primary") ?? border.Stroke;
        border.StrokeThickness = 2.5;
        border.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Color.FromArgb("#5C79BCEB")),
            Offset = new Point(0, 0),
            Radius = 14,
            Opacity = 1
        };
    }

    private void DigitEntry_Unfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        if (entry.Parent is not Border border) return;

        border.Stroke = GetBrushResource("C_InputBorder") ?? border.Stroke;
        border.StrokeThickness = 2;
        if (Application.Current?.Resources.TryGetValue("ShadowSmall", out var shadow) == true && shadow is Shadow s)
            border.Shadow = s;
    }

    private static Brush? GetBrushResource(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Color color)
            return new SolidColorBrush(color);

        return null;
    }

    private void DigitEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (BindingContext is PuzzlePageViewModel vm && !vm.IsNotBusy) return;

        if (sender is not Entry entry) return;
        if (entry.BindingContext is not DigitInputViewModel dvm) return;
        if (dvm.IsLocked) return;

        var text = entry.Text ?? "";

        var digitsOnly = new string(text.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length > 1) digitsOnly = digitsOnly[^1].ToString();

        if (digitsOnly != text)
        {
            entry.Text = digitsOnly;
            return;
        }

        if (!string.IsNullOrEmpty(e.OldTextValue) && e.OldTextValue.Length == 1 && string.IsNullOrEmpty(digitsOnly))
        {
            FocusIndex(dvm.Index - 1);
            return;
        }

        if (digitsOnly.Length == 1)
            FocusIndex(dvm.Index + 1);
    }

    private void FocusIndex(int index)
    {
        if (index < 0)
            return;

        if (BindingContext is not PuzzlePageViewModel vm)
            return;

        if (index >= vm.InputDigits.Count)
            return;

        if (_entryByIndex.TryGetValue(index, out var next))
        {
            if (next.IsEnabled && !next.IsReadOnly)
                next.Focus();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded)
        {
            _isLoaded = true;
            return;
        }

        if (_vm.ShowProfessor && string.IsNullOrWhiteSpace(ProfessorLabel.Text) && !string.IsNullOrWhiteSpace(_vm.ProfessorMessage))
            SetProfessorText(_vm.ProfessorMessage);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelCelebrationAnimation();
        StopProfessorTalkingAnimation();
        _entryByIndex.Clear();
        _vm.Cleanup();
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.NewHandler is null)
        {
            CancelCelebrationAnimation();
            StopProfessorTalkingAnimation();
            _entryByIndex.Clear();
            _vm.PropertyChanged -= Vm_PropertyChanged;
        }

        base.OnHandlerChanging(args);
    }

    private void CancelCelebrationAnimation()
    {
        if (_celebrationCts != null)
        {
            _celebrationCts.Cancel();
            _celebrationCts.Dispose();
            _celebrationCts = null;
        }

    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PuzzlePageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() : null;
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "normal" : "normal";

        int level = 1;
        if (query.TryGetValue("level", out var lvObj) && int.TryParse(lvObj?.ToString(), out var parsedLevel))
            level = parsedLevel;

        Dispatcher.Dispatch(async () =>
        {
            const int maxLevel = GameConfig.MaxLevel;
            if (level > maxLevel) level = maxLevel;
            if (!string.IsNullOrWhiteSpace(gameId))
                await vm.LoadAsync(gameId, difficulty, level);
        });
    }
}
