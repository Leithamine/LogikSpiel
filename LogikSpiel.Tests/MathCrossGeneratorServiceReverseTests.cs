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

    private static object? InvokeReverse(int c, string op, string difficulty)
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
            return reverse.Invoke(service, new object[] { c, op, settings, new Random(12345) });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }
}
