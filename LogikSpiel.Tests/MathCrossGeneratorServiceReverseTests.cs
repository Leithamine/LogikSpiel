using System.Reflection;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceReverseTests
{
    [Theory]
    [InlineData("easy", -1)]
    [InlineData("easy", 0)]
    [InlineData("easy", 1)]
    [InlineData("normal", -1)]
    [InlineData("normal", 0)]
    [InlineData("normal", 1)]
    [InlineData("hard", -1)]
    [InlineData("hard", 0)]
    [InlineData("hard", 1)]
    [InlineData("master", -1)]
    [InlineData("master", 0)]
    [InlineData("master", 1)]
    public void Reverse_Addition_BoundaryValues_DoNotThrow(string difficulty, int c)
    {
        var exception = Record.Exception(() => InvokeReverse(c, "+", difficulty));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("easy", -1)]
    [InlineData("easy", 0)]
    [InlineData("easy", 1)]
    [InlineData("normal", -1)]
    [InlineData("normal", 0)]
    [InlineData("normal", 1)]
    [InlineData("hard", -1)]
    [InlineData("hard", 0)]
    [InlineData("hard", 1)]
    [InlineData("master", -1)]
    [InlineData("master", 0)]
    [InlineData("master", 1)]
    public void Reverse_Multiplication_BoundaryValues_DoNotThrow(string difficulty, int c)
    {
        var exception = Record.Exception(() => InvokeReverse(c, "×", difficulty));
        Assert.Null(exception);
    }


    [Theory]
    [InlineData("hard", -1)]
    [InlineData("hard", 0)]
    [InlineData("master", -1)]
    [InlineData("master", 0)]
    public void Reverse_Subtraction_AllowsNegativeOperandsWhenDifficultyAllows(string difficulty, int c)
    {
        bool foundNegativeOperand = false;

        for (int seed = 1; seed <= 200; seed++)
        {
            var reverseResult = InvokeReverse(c, "-", difficulty, seed);
            if (reverseResult == null) continue;

            var tupleType = reverseResult.GetType();
            int a = (int)(tupleType.GetField("Item1")?.GetValue(reverseResult)
                ?? throw new InvalidOperationException("Could not read Item1 from reverse tuple."));
            int b = (int)(tupleType.GetField("Item2")?.GetValue(reverseResult)
                ?? throw new InvalidOperationException("Could not read Item2 from reverse tuple."));

            if (a < 0 || b < 0)
            {
                foundNegativeOperand = true;
                break;
            }
        }

        Assert.True(foundNegativeOperand,
            $"Expected Reverse('-', c={c}) to allow at least one negative operand for '{difficulty}'.");
    }

    private static object? InvokeReverse(int c, string op, string difficulty, int seed = 12345)
    {
        var service = new MathCrossGeneratorService();
        var type = typeof(MathCrossGeneratorService);

        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");

        var settings = getSettings.Invoke(null, new object[] { difficulty })
            ?? throw new InvalidOperationException("Settings could not be created.");

        var reverse = type.GetMethod("Reverse", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Reverse method not found.");

        try
        {
            return reverse.Invoke(service, new object[] { c, op, settings, new Random(seed) });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}
