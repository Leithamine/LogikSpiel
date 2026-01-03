// LogikSpiel/View/MathHangmanPage.xaml.cs
using LogikSpiel.View.Controls;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class MathHangmanPage : ContentPage
{
    private readonly HangmanDrawable _hangmanDrawable = new();

    public MathHangmanPage(MathHangmanPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Drawable der GraphicsView im XAML zuweisen
        HangmanView.Drawable = _hangmanDrawable;

        // Überwache Fehleränderungen für das Neuzeichnen
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.WrongCount))
            {
                _hangmanDrawable.WrongCount = vm.WrongCount;
                HangmanView.Invalidate(); // Erzwingt Refresh der Grafik
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MathHangmanPageViewModel vm) await vm.StartRoundAsync();
    }
}