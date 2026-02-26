using LogikSpiel.Model;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossValueNormalizerTests
{
    [Theory]
    [InlineData("-", "−")]
    [InlineData("−", "−")]
    [InlineData("x", "×")]
    [InlineData("X", "×")]
    [InlineData("*", "×")]
    [InlineData("/", "÷")]
    [InlineData(":", "÷")]
    [InlineData("+", "+")]
    public void NormalizeOperator_MapsAliases(string input, string expected)
    {
        Assert.Equal(expected, MathCrossValueNormalizer.NormalizeOperator(input));
    }

    [Theory]
    [InlineData("−1", -1)]
    [InlineData("-1", -1)]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    public void TryParseNumber_HandlesSymbolsAndSeparators(string input, double expected)
    {
        Assert.True(MathCrossValueNormalizer.TryParseNumber(input, out var parsed));
        Assert.Equal(expected, parsed, 6);
    }

    [Theory]
    [InlineData("5", "5")]
    [InlineData("5.0", "5")]
    [InlineData("−1", "-1")]
    public void AreNumbersEqual_IntegerMode_RequiresExactIntegerEquivalence(string userInput, string solution)
    {
        Assert.True(MathCrossValueNormalizer.AreNumbersEqual(userInput, solution, allowDecimals: false));
    }

    [Theory]
    [InlineData("1.5", "1.50005", true)]
    [InlineData("1.5", "1.5002", false)]
    public void AreNumbersEqual_MasterMode_UsesSmallTolerance(string userInput, string solution, bool expected)
    {
        Assert.Equal(expected, MathCrossValueNormalizer.AreNumbersEqual(userInput, solution, allowDecimals: true));
    }

}
