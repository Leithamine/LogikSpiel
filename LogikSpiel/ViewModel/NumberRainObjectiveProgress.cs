#nullable enable
using LogikSpiel.Core;

namespace LogikSpiel.ViewModel;

public sealed class NumberRainObjectiveProgress : ObservableObject
{
    private int _count;

    public string Title { get; }
    public int Target { get; }
    public Func<int, bool> Predicate { get; }

    public int Count
    {
        get => _count;
        private set
        {
            if (SetProperty(ref _count, value))
                OnPropertyChanged(nameof(ProgressText));
        }
    }

    public string ProgressText => $"{Count}/{Target}";
    public bool IsCompleted => Count >= Target;

    public NumberRainObjectiveProgress(string title, int target, Func<int, bool> predicate)
    {
        Title = title;
        Target = target;
        Predicate = predicate;
    }

    public void Increment()
    {
        Count = Math.Min(Target, Count + 1);
        OnPropertyChanged(nameof(IsCompleted));
    }
}
