using LogikSpiel.Model;
using LogikSpiel.Services;

namespace LogikSpiel.Tests;

public sealed class LockRiddleGeneratorServiceTests
{
    [Fact]
    public void GenerateGame_Hard_1000Times_AlwaysUniqueAndUsefulNothingHint()
    {
        var sut = new LockRiddleGeneratorService();

        for (var i = 0; i < 1000; i++)
        {
            var game = sut.GenerateGame("hard");

            Assert.True(sut.IsGameValid(game));
            Assert.True(NothingHintDigitsAppearInOtherHints(game.Hints));
            Assert.True(HasExactlyOneSolution(game));
        }
    }

    private static bool NothingHintDigitsAppearInOtherHints(IReadOnlyList<LockHint> hints)
    {
        for (var i = 0; i < hints.Count; i++)
        {
            var hint = hints[i];
            if (hint.WellPlaced != 0 || hint.WrongPlaced != 0)
            {
                continue;
            }

            var slots = hint.Slots.ToHashSet();
            var appears = hints
                .Where((_, index) => index != i)
                .SelectMany(h => h.Slots)
                .Any(slots.Contains);

            if (!appears)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactlyOneSolution(LockRiddleGame game)
    {
        var solver = new ConstraintSolver(game.SecretCode.Length, game.Hints);
        var solutions = solver.FindAllSolutions(maxSolutions: 2);
        return solutions.Count == 1 && solutions[0] == game.SecretCode;
    }
}
