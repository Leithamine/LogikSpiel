using LogikSpiel.ViewModel;

namespace LogikSpiel.View;

public partial class LearnPage : ContentPage, IQueryAttributable
{
    private readonly LearnPageViewModel vm;

    public LearnPage(LearnPageViewModel vm)
    {
        InitializeComponent();
        this.vm = vm;
        BindingContext = this.vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("gameId", out var idObj) && idObj is string id)
            vm.GameId = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await vm.LoadAsync();
    }
}
