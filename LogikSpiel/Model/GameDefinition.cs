#nullable enable

using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using LogikSpiel.Services.Localization;

namespace LogikSpiel.Model;

/// <summary>
/// Model für Spiel-Definition aus games.json
/// </summary>
public sealed class GameDefinition : INotifyPropertyChanged, IDisposable
{
    private string _title = "";
    private string _description = "";
    private List<string> _rules = new();
    private readonly EventHandler _cultureChangedHandler;
    private bool _disposed;

    public GameDefinition()
    {
        _cultureChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Rules));
        };
        LocalizationService.CultureChanged += _cultureChangedHandler;
    }

    ~GameDefinition() => Dispose(false);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = "";

    public string Title
    {
        get => string.IsNullOrWhiteSpace(TitleKey) ? _title : LocalizationService.GetString(TitleKey);
        set => _title = value;
    }

    public string Description
    {
        get => string.IsNullOrWhiteSpace(DescriptionKey) ? _description : LocalizationService.GetString(DescriptionKey);
        set => _description = value;
    }

    public string Image { get; set; } = "";

    /// <summary>
    /// Die Shell-Route für die Spielseite (z.B. "PascalTrianglePage")
    /// WICHTIG: Muss mit Routing.RegisterRoute() übereinstimmen!
    /// </summary>
    public string Route { get; set; } = "";

    public int LevelCount { get; set; } = 50;
    public List<string> Difficulties { get; set; } = new();

    public List<string> Rules
    {
        get => RuleKeys.Count == 0 ? _rules : RuleKeys.Select(LocalizationService.GetString).ToList();
        set => _rules = value;
    }

    public string TitleKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public List<string> RuleKeys { get; set; } = new();

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        LocalizationService.CultureChanged -= _cultureChangedHandler;
        _disposed = true;
    }
}
