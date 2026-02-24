#nullable enable
using System.ComponentModel;
using LogikSpiel.Core;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class PuzzlePage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;
    private readonly Dictionary<int, Entry> _entryByIndex = new();
    private readonly PuzzlePageViewModel _vm;
    private CancellationTokenSource? _celebrationCts;
    private CancellationTokenSource? _typingCts;
    private CancellationTokenSource? _professorAnimCts;
    private CancellationTokenSource? _speechBubbleAnimCts;

    public PuzzlePage(PuzzlePageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        _vm.PropertyChanged += Vm_PropertyChanged;
    }

    private async void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (BindingContext is not PuzzlePageViewModel vm)
            return;

        if (e.PropertyName == nameof(PuzzlePageViewModel.ShowProfessor))
        {
            if (vm.ShowProfessor)
            {
                await ShowBubbleAsync();
                StartProfessorTalkingAnimation();

                if (!vm.IsCelebrating)
                    await ShakeLockAsync();
            }
            else
            {
                StopProfessorTalkingAnimation();
            }
        }

        if (e.PropertyName == nameof(PuzzlePageViewModel.ProfessorMessage) && vm.ShowProfessor)
            await TypeTextAsync(vm.ProfessorMessage);

        if (e.PropertyName != nameof(PuzzlePageViewModel.IsCelebrating))
            return;

        if (!vm.IsCelebrating)
            return;

        CancelCelebrationAnimation();
        _celebrationCts = new CancellationTokenSource();
        var ct = _celebrationCts.Token;

        await Task.Delay(100);

        try
        {
            ct.ThrowIfCancellationRequested();
            await CelebrateLockAsync();
        }
        catch (OperationCanceledException)
        {
            // Seite wurde verlassen
        }
    }

    private async Task ShowBubbleAsync()
    {
        SpeechBubble.Scale = 0.6;
        SpeechBubble.Opacity = 0;

        await Task.WhenAll(
            SpeechBubble.FadeTo(1, 150),
            SpeechBubble.ScaleTo(1.05, 180, Easing.CubicOut)
        );

        await SpeechBubble.ScaleTo(1.0, 80);
    }

    private async Task TypeTextAsync(string text)
    {
        _typingCts?.Cancel();
        _typingCts?.Dispose();
        _typingCts = new CancellationTokenSource();
        var ct = _typingCts.Token;

        ProfessorLabel.Text = string.Empty;

        foreach (char c in text ?? string.Empty)
        {
            if (ct.IsCancellationRequested)
                break;

            ProfessorLabel.Text += c;
            await Task.Delay(20, ct);
        }
    }


    private void StartProfessorTalkingAnimation()
    {
        StopProfessorTalkingAnimation();

        _professorAnimCts = new CancellationTokenSource();
        _speechBubbleAnimCts = new CancellationTokenSource();
        var professorCt = _professorAnimCts.Token;
        var bubbleCt = _speechBubbleAnimCts.Token;

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                while (!professorCt.IsCancellationRequested)
                {
                    await ProfessorImage.ScaleTo(1.06, 280, Easing.CubicInOut);
                    await ProfessorImage.ScaleTo(1.0, 280, Easing.CubicInOut);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on close/navigation
            }
        });

        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                while (!bubbleCt.IsCancellationRequested)
                {
                    await SpeechBubble.ScaleTo(1.02, 110, Easing.CubicInOut);
                    await SpeechTail.TranslateTo(-2, 0, 110, Easing.CubicInOut);
                    await SpeechBubble.ScaleTo(1.0, 110, Easing.CubicInOut);
                    await SpeechTail.TranslateTo(0, 0, 110, Easing.CubicInOut);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on close/navigation
            }
        });
    }

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
            SpeechTail.CancelAnimations();

            ProfessorImage.Scale = 1;
            ProfessorImage.Rotation = 0;
            SpeechBubble.Scale = 1;
            SpeechTail.TranslationX = 0;
        });
    }

    private async Task CelebrateLockAsync()
    {
        await LockImage.ScaleTo(1.2, 200);
        await LockImage.ScaleTo(1.0, 120);
        await LockImage.RotateTo(10, 80);
        await LockImage.RotateTo(-10, 80);
        await LockImage.RotateTo(0, 80);
    }

    private async Task ShakeLockAsync()
    {
        const int shakeDistance = 12;
        const uint shakeSpeed = 50;

        for (int i = 0; i < 4; i++)
        {
            await LockImage.TranslateTo(-shakeDistance, 0, shakeSpeed);
            await LockImage.TranslateTo(shakeDistance, 0, shakeSpeed);
        }

        await LockImage.TranslateTo(0, 0, shakeSpeed);
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

        _typingCts?.Cancel();
        _typingCts?.Dispose();
        _typingCts = null;
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
