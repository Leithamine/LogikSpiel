#nullable enable
using System.ComponentModel;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class PuzzlePage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;
    private readonly Dictionary<int, Entry> _entryByIndex = new();
    private CancellationTokenSource? _celebrationCts;
    private PuzzlePageViewModel? _subscribedVm;

    public PuzzlePage(PuzzlePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        AttachVm(vm);
    }


    private void AttachVm(PuzzlePageViewModel? vm)
    {
        if (ReferenceEquals(_subscribedVm, vm))
            return;

        if (_subscribedVm != null)
            _subscribedVm.PropertyChanged -= Vm_PropertyChanged;

        _subscribedVm = vm;

        if (_subscribedVm != null)
            _subscribedVm.PropertyChanged += Vm_PropertyChanged;
    }

    private async void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PuzzlePageViewModel.IsCelebrating))
            return;

        if (BindingContext is not PuzzlePageViewModel vm)
            return;

        _celebrationCts?.Cancel();
        _celebrationCts?.Dispose();
        _celebrationCts = null;

        if (!vm.IsCelebrating)
        {
            OpenedLockImage?.CancelAnimations();
            return;
        }

        var cts = new CancellationTokenSource();
        _celebrationCts = cts;

        try
        {
            if (FireworksView != null)
            {
                FireworksView.IsAnimationEnabled = false;
                FireworksView.IsAnimationEnabled = true;
            }

            if (OpenedLockImage != null)
            {
                OpenedLockImage.CancelAnimations();
                OpenedLockImage.Scale = 0.9;

                await OpenedLockImage.ScaleToAsync(1.05, 160, Easing.CubicOut);
                if (cts.IsCancellationRequested) return;

                await OpenedLockImage.ScaleToAsync(1.0, 120, Easing.CubicInOut);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is TaskCanceledException)
        {
            // Seite wurde während Animation freigegeben oder Animation abgebrochen.
        }
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
        if (BindingContext is not PuzzlePageViewModel vm) return;
        if (index < 0 || index >= vm.InputDigits.Count) return;

        if (_entryByIndex.TryGetValue(index, out var next))
        {
            if (next.Handler != null && next.IsEnabled && !next.IsReadOnly)
                next.Focus();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded && BindingContext is PuzzlePageViewModel vm && vm.Hints.Count == 0)
        {
            _ = vm.LoadAsync("riddle_lock", "normal", 1);
            _isLoaded = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _celebrationCts?.Cancel();
        _celebrationCts?.Dispose();
        _celebrationCts = null;
        OpenedLockImage?.CancelAnimations();
        _entryByIndex.Clear();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        AttachVm(BindingContext as PuzzlePageViewModel);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PuzzlePageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() ?? "riddle_lock" : "riddle_lock";
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "normal" : "normal";

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
}
