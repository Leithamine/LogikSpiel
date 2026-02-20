using LogikSpiel.Model;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class LockRiddleGeneratorServiceTests
{
    [Fact]
    public void GenerateGame_Hard_1000Runs_MustStayValidUniqueAndRespectNothingCorrectCoverageRule()
    {
        var generator = new LockRiddleGeneratorService();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        for (int i = 0; i < 1000; i++)
        {
            cts.Token.ThrowIfCancellationRequested();

            var game = generator.GenerateGame("hard", levelNumber: 9000 + i);

            Assert.True(generator.IsGameValid(game));
            Assert.True(SatisfiesNothingCorrectCoverageRule(game.Hints));
        }
    }


    [Fact]
    public void GenerateGame_SameDifficultyAndLevel_IsDeterministic()
    {
        var generator = new LockRiddleGeneratorService();

        var first = generator.GenerateGame("normal", levelNumber: 2);
        var second = generator.GenerateGame("normal", levelNumber: 2);

        Assert.Equal(first.SecretCode, second.SecretCode);
        Assert.Equal(first.Hints.Count, second.Hints.Count);

        for (int i = 0; i < first.Hints.Count; i++)
        {
            Assert.Equal(first.Hints[i].WellPlaced, second.Hints[i].WellPlaced);
            Assert.Equal(first.Hints[i].WrongPlaced, second.Hints[i].WrongPlaced);
            Assert.Equal(first.Hints[i].Slots, second.Hints[i].Slots);
        }
    }

    [Fact]
    public void NothingCorrectRule_RequiresEveryDigitOfZeroZeroHintToReappear()
    {
        var hints = new List<LockHint>
        {
            new() { Slots = ["5", "2", "1"], WellPlaced = 0, WrongPlaced = 0 },
            new() { Slots = ["0", "4", "9"], WellPlaced = 1, WrongPlaced = 0 },
            new() { Slots = ["6", "7", "8"], WellPlaced = 0, WrongPlaced = 2 },
            new() { Slots = ["1", "8", "2"], WellPlaced = 1, WrongPlaced = 0 }
        };

        Assert.False(SatisfiesNothingCorrectCoverageRule(hints));
    }

    private static bool SatisfiesNothingCorrectCoverageRule(IReadOnlyList<LockHint> hints)
    {
        foreach (var currentHint in hints.Where(h => h.WellPlaced == 0 && h.WrongPlaced == 0))
        {
            var currentDigits = currentHint.Slots
                .Select(slot => int.TryParse(slot, out var digit) ? digit : (int?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .Distinct()
                .ToList();

            var otherDigits = hints
                .Where(h => h != currentHint)
                .SelectMany(h => h.Slots)
                .Select(slot => int.TryParse(slot, out var digit) ? digit : (int?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToHashSet();

            if (!currentDigits.All(otherDigits.Contains))
                return false;
        }

        return true;
    }
}
