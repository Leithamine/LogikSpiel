using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class PuzzlePage : ContentPage, IQueryAttributable
{
    // Konstruktor nimmt ViewModel an
    public PuzzlePage(PuzzlePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not PuzzlePageViewModel vm) return;

        var gameId = query["gameId"] as string ?? "";
        var difficulty = query["difficulty"] as string ?? "normal";
        var level = query.TryGetValue("level", out var lv) && lv is int i ? i : 1;

        _ = vm.LoadAsync(gameId, difficulty, level);
    }
}