// LogikSpiel/View/MathHangmanPage.xaml.cs
using LogikSpiel.Core;
using LogikSpiel.View.Controls;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MathHangmanPage : ContentPage, IQueryAttributable
{
    private readonly HangmanDrawable _hangmanDrawable = new();
    private bool _isLoaded;
    //
    public MathHangmanPage(MathHangmanPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Drawable der GraphicsView im XAML zuweisen
        HangmanView.Drawable = _hangmanDrawable;
        _hangmanDrawable.WrongCount = vm.WrongCount;
        HangmanView.Invalidate();

        // Überwache Fehleränderungen für das Neuzeichnen
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.WrongCount) || e.PropertyName == nameof(vm.Lives))
            {
                Dispatcher.Dispatch(() =>
                {
                    _hangmanDrawable.WrongCount = vm.WrongCount;
                    HangmanView.Invalidate(); // Erzwingt Refresh der Grafik
                });
            }
        };

        //vm.RequestNearMiss += () => MainThread.BeginInvokeOnMainThread(async () => await ShakeAsync());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isLoaded && BindingContext is MathHangmanPageViewModel vm)
        {
            await vm.LoadAsync("math_hangman", "normal", 1);
            _isLoaded = true;
            _hangmanDrawable.WrongCount = vm.WrongCount;
            HangmanView.Invalidate();
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not MathHangmanPageViewModel vm) return;
        _isLoaded = true;

        var gameId = query.TryGetValue("gameId", out var idObj) ? idObj?.ToString() ?? "math_hangman" : "math_hangman";
        var difficulty = query.TryGetValue("difficulty", out var diffObj) ? diffObj?.ToString() ?? "normal" : "normal";

        int level = 1;
        if (query.TryGetValue("level", out var lvObj))
            int.TryParse(lvObj?.ToString(), out level);

        Dispatcher.Dispatch(async () =>
        {
            await vm.LoadAsync(gameId, difficulty, level);
            _hangmanDrawable.WrongCount = vm.WrongCount;
            HangmanView.Invalidate();
        });
    }

}
