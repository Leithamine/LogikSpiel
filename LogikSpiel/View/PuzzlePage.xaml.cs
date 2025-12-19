#nullable enable
using System.ComponentModel;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class PuzzlePage : ContentPage, IQueryAttributable
{
    private bool _isLoaded;
    private readonly Dictionary<int, Entry> _entryByIndex = new();

    public PuzzlePage(PuzzlePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        vm.PropertyChanged += Vm_PropertyChanged;
    }

    private async void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PuzzlePageViewModel.IsCelebrating))
        {
            if (BindingContext is not PuzzlePageViewModel vm) return;

            if (vm.IsCelebrating)
            {
                // ?? Animation “neu starten” (best effort)
                if (FireworksView != null)
                {
                    FireworksView.IsAnimationEnabled = false;
                    FireworksView.IsAnimationEnabled = true;
                }

                // kleines Pop fürs Schloss im Overlay
                if (OpenedLockImage != null)
                {
                    OpenedLockImage.Scale = 0.9;
                    await OpenedLockImage.ScaleToAsync(1.05, 160, Easing.CubicOut);
                    await OpenedLockImage.ScaleToAsync(1.0, 120, Easing.CubicInOut);
                }
            }
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

        // Backspace -> Fokus zurück
        if (!string.IsNullOrEmpty(e.OldTextValue) && e.OldTextValue.Length == 1 && string.IsNullOrEmpty(digitsOnly))
        {
            FocusIndex(dvm.Index - 1);
            return;
        }

        // 1 Ziffer -> Fokus vor
        if (digitsOnly.Length == 1)
            FocusIndex(dvm.Index + 1);
    }

    private void FocusIndex(int index)
    {
        if (index < 0) return;

        if (_entryByIndex.TryGetValue(index, out var next))
        {
            if (next.IsEnabled && !next.IsReadOnly)
                next.Focus();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded && BindingContext is PuzzlePageViewModel vm && vm.Hints.Count == 0)
        {
            await vm.LoadAsync("riddle_lock", "normal", 1);
            _isLoaded = true;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PuzzlePageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() ?? "riddle_lock" : "riddle_lock";
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "normal" : "normal";

        int level = 1;
        if (query.TryGetValue("level", out var lvObj))
            int.TryParse(lvObj?.ToString(), out level);

        Dispatcher.Dispatch(async () => await vm.LoadAsync(gameId, difficulty, level));
    }
}
