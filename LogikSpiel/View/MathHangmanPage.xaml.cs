#nullable enable
using LogikSpiel.View.Controls;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MathHangmanPage : ContentPage, IQueryAttributable
{
    private readonly MathHangmanPageViewModel _vm;
    private readonly HangmanDrawable _drawable = new();
    private bool _hasSized;

    public MathHangmanPage(MathHangmanPageViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        BindingContext = _vm;

        HangmanView.Drawable = _drawable;
        HangmanView.SizeChanged += HangmanView_SizeChanged;

        // Initial render
        _drawable.WrongCount = _vm.WrongCount;
        HangmanView.Invalidate();

        // Safety: nicht doppelt subscriben
        _vm.PropertyChanged -= Vm_PropertyChanged;
        _vm.PropertyChanged += Vm_PropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= Vm_PropertyChanged;
        HangmanView.SizeChanged -= HangmanView_SizeChanged;
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MathHangmanPageViewModel.WrongCount))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _drawable.WrongCount = _vm.WrongCount;
                HangmanView.Invalidate();
            });
        }
    }

    private void HangmanView_SizeChanged(object? sender, EventArgs e)
    {
        if (_hasSized || HangmanView.Width <= 0 || HangmanView.Height <= 0)
            return;

        _hasSized = true;
        _drawable.WrongCount = _vm.WrongCount;
        HangmanView.Invalidate();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var gameId = query.TryGetValue("gameId", out var g) ? g?.ToString() : "math_hangman";
        var diff = query.TryGetValue("difficulty", out var d) ? d?.ToString() : "normal";
        var level = query.TryGetValue("level", out var l) && int.TryParse(l?.ToString(), out var lv) ? lv : 1;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50);
            await _vm.LoadAsync(gameId ?? "math_hangman", diff ?? "normal", level);
        });
    }
}
