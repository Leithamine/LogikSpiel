using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class LockRiddleGeneratorServiceTests
{
    [Fact]
    public void GenerateGame_Hard_1000Times_MeetsValidityAndZeroZeroRule()
    {
        var generator = new LockRiddleGeneratorService();

        for (var i = 0; i < 1000; i++)
        {
            var game = generator.GenerateGame("hard");

            Assert.True(generator.IsGameValid(game));
            Assert.True(HasZeroZeroCoverage(game));
        }
    }

    private static bool HasZeroZeroCoverage(LogikSpiel.Model.LockRiddleGame game)
    {
        var nothingHints = game.Hints.Where(h => h.WellPlaced == 0 && h.WrongPlaced == 0);

        foreach (var currentHint in nothingHints)
        {
            var hasCoverage = game.Hints.Any(h =>
                !ReferenceEquals(h, currentHint) &&
                h.Slots.Intersect(currentHint.Slots).Any());

            if (!hasCoverage)
                return false;
        }

        return true;
    }
}
