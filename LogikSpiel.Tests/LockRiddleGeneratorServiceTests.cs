using LogikSpiel.Model;
using LogikSpiel.Services;

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

            var game = generator.GenerateGame("hard", seed: 9000 + i);

            Assert.True(generator.IsGameValid(game));
            Assert.True(SatisfiesNothingCorrectCoverageRule(game.Hints));
        }
    }

    private static bool SatisfiesNothingCorrectCoverageRule(IReadOnlyList<LockHint> hints)
    {
        foreach (var currentHint in hints.Where(h => h.WellPlaced == 0 && h.WrongPlaced == 0))
        {
            if (!hints.Any(h => h != currentHint && h.Slots.Intersect(currentHint.Slots).Any()))
                return false;
        }

        return true;
    }
}
