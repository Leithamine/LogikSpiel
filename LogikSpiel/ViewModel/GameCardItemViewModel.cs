using LogikSpiel.Model;

namespace LogikSpiel.ViewModel;

public sealed class GameCardItemViewModel
{
    public GameDefinition Model { get; }
    public string Id => Model.Id;
    public string Title => Model.Title;
    public string Image => Model.Image;

    public GameCardItemViewModel(GameDefinition model) => Model = model;
}
