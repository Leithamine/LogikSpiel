using LogikSpiel.Model;

namespace LogikSpiel.ViewModel;

public sealed class LevelItemViewModel
{
    public LevelSpec Spec { get; }
    public int LevelNumber => Spec.LevelNumber;

    public LevelItemViewModel(LevelSpec spec) => Spec = spec;
}
