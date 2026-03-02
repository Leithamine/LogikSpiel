using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossEquationCountPerLevelTests
{
    [Theory]
    [InlineData("easy")]
    [InlineData("normal")]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenerateGame_EachLevelSeed_StaysBetweenTenAndFifteenEquations(string difficulty)
    {
        var service = new MathCrossGeneratorService();

        for (int level = 1; level <= 100; level++)
        {
            var game = service.GenerateGame(difficulty, seed: level);
            Assert.InRange(game.Equations.Count, 10, 15);
        }
    }
}
