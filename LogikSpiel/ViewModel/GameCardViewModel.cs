using LogikSpiel.Core;
using LogikSpiel.Model;

namespace LogikSpiel.ViewModel;

public sealed class GameCardViewModel : ObservableObject
{
    public GameDefinition Game { get; }
    public string Title => Game.Title;
    public string Image => Game.Image;
    public string Id => Game.Id;

    public GameCardViewModel(GameDefinition game) => Game = game;
}
