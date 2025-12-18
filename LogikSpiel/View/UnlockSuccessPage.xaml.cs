namespace LogikSpiel.View;

public partial class UnlockSuccessPage : ContentPage, IQueryAttributable
{
    private string _gameId = "riddle_lock";
    private string _difficulty = "normal";
    private int _nextLevel = 1;
    private int _reward = 0;

    public UnlockSuccessPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _gameId = query.TryGetValue("gameId", out var g) ? g?.ToString() ?? "riddle_lock" : "riddle_lock";
        _difficulty = query.TryGetValue("difficulty", out var d) ? d?.ToString() ?? "normal" : "normal";

        if (query.TryGetValue("nextLevel", out var nl))
            int.TryParse(nl?.ToString(), out _nextLevel);

        if (query.TryGetValue("reward", out var r))
            int.TryParse(r?.ToString(), out _reward);

        RewardLabel.Text = _reward > 0 ? $"Du erhältst {_reward} Coins 💰" : "";
    }

    private async void Continue_Clicked(object sender, EventArgs e)
    {
        // ✅ nächstes Level öffnen
        await Shell.Current.GoToAsync(nameof(PuzzlePage), new Dictionary<string, object>
        {
            ["gameId"] = _gameId,
            ["difficulty"] = _difficulty,
            ["level"] = _nextLevel
        });
    }

    private async void BackToMap_Clicked(object sender, EventArgs e)
    {
        // ✅ HIER Route zu deiner Map anpassen!
        // Beispiel:
        // await Shell.Current.GoToAsync("//MapPage");
        await Shell.Current.GoToAsync(".."); // Fallback: geht zurück
    }
}
