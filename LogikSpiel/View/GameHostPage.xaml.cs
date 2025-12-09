using LogikSpiel.Model;
using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class GameHostPage : ContentPage, IQueryAttributable
{
    public GameHostPage(GameHostPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not GameHostPageViewModel vm) return;

        if (query.TryGetValue("spec", out var specObj) && specObj is LevelSpec spec)
            vm.Spec = spec;
    }
}
