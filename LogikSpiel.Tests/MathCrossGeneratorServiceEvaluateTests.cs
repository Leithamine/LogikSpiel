using System.Reflection;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceEvaluateTests
{
    [Theory]
    [InlineData(2, "+", 3, "×", 4, 20)]
    [InlineData(20, "-", 6, "÷", 2, 7)]
    [InlineData(8, "÷", 2, "+", 3, 7)]
    [InlineData(3, "×", 4, "-", 5, 7)]
    public void Evaluate_ThreeTermExpressions_UsesLeftToRightRule(
        decimal a, string op1, decimal b, string op2, decimal c, decimal expected)
    {
        var result = InvokeEvaluate(a, op1, b, op2, c);

        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value);
    }

    [Fact]
    public void Evaluate_InvalidDivision_ReturnsNull()
    {
        var result = InvokeEvaluate(5, "÷", 2, "+", 1);

        Assert.Null(result);
    }

    private static decimal? InvokeEvaluate(decimal a, string op1, decimal b, string op2, decimal c)
    {
        var method = typeof(MathCrossGeneratorService).GetMethod(
            "Evaluate",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("Evaluate method not found.");

        return (decimal?)method.Invoke(null, new object[] { a, op1, b, op2, c });
    }
}
