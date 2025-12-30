#nullable enable
using LogikSpiel.View.Controls;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MathHangmanPage : ContentPage, IQueryAttributable
{
    private readonly MathHangmanPageViewModel _vm;
    private readonly HangmanDrawable _drawable = new();

    public MathHangmanPage(MathHangmanPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        HangmanView.Drawable = _drawable;
        _drawable.WrongCount = _vm.WrongCount;
        HangmanView.Invalidate();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MathHangmanPageViewModel.WrongCount))
            {
                _drawable.WrongCount = _vm.WrongCount;
                HangmanView.Invalidate();
            }
        };
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
