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
        if (sender is not Entry entry) return;
        if (entry.BindingContext is not DigitInputViewModel dvm) return;
        if (dvm.IsLocked) return;

        var text = entry.Text ?? "";

        // nur Ziffern, max 1 Zeichen
        var digitsOnly = new string(text.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length > 1) digitsOnly = digitsOnly[^1].ToString();

        if (digitsOnly != text)
        {
            entry.Text = digitsOnly;
            return;
        }

        // Backspace: 1 -> 0 => Fokus zurück
        if (!string.IsNullOrEmpty(e.OldTextValue) && e.OldTextValue.Length == 1 && string.IsNullOrEmpty(digitsOnly))
        {
            FocusIndex(dvm.Index - 1);
            return;
        }

        // 1 Ziffer eingegeben => Fokus nach vorne
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
