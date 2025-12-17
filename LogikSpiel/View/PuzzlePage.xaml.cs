using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class PuzzlePage : ContentPage, IQueryAttributable
{
    private bool _isLoaded = false; // Verhindert doppelts Laden

    public PuzzlePage(PuzzlePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    // WICHTIG: Diese Methode wird immer aufgerufen, wenn die Seite sichtbar wird
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Wenn noch kein Spiel geladen wurde, starte ein Standard-Spiel (Normal)
        if (!_isLoaded && BindingContext is PuzzlePageViewModel vm)
        {
            // Starte Level 1 Normal, falls nichts anderes übergeben wurde
            await vm.LoadAsync("riddle_lock", "normal", 1);
            _isLoaded = true;
        }
    }

    // Das hier wird NUR aufgerufen, wenn Parameter übergeben wurden (z.B. ?difficulty=hard)
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PuzzlePageViewModel vm) return;

        var gameId = query.ContainsKey("gameId") ? query["gameId"]?.ToString() ?? "" : "";
        var difficulty = query.ContainsKey("difficulty") ? query["difficulty"]?.ToString() ?? "normal" : "normal";

        int level = 1;
        if (query.TryGetValue("level", out var lv))
        {
            int.TryParse(lv?.ToString(), out level);
        }

        // ÄNDERUNG: Nicht Task.Run nutzen, sondern direkt aufrufen.
        // Da LoadAsync ein Task ist, nutzen wir "Fire-and-Forget" sicher auf dem Dispatcher.
        Dispatcher.Dispatch(async () =>
        {
            await vm.LoadAsync(gameId, difficulty, level);
        });

        _isLoaded = true;
    }
}