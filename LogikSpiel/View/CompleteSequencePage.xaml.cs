using LogikSpiel.Services;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class CompleteSequencePage : ContentPage, IQueryAttributable
{
    public CompleteSequencePage()
    {
        InitializeComponent();
        BindingContext = AppServices.Get<CompleteSequencePageViewModel>();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not CompleteSequencePageViewModel vm) return;

        var gameId = query.TryGetValue("gameId", out var g) ? g as string : null;
        var diff = query.TryGetValue("difficulty", out var d) ? d as string : "normal";
        var level = query.TryGetValue("level", out var l) && l is int li ? li : 1;

        if (!string.IsNullOrWhiteSpace(gameId))
            _ = vm.LoadAsync(gameId!, diff ?? "normal", level);
    }
}
