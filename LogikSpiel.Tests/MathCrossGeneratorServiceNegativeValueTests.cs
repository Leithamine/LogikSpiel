using System.Reflection;
using LogikSpiel.Services;
using Xunit;

namespace LogikSpiel.Tests;

public class MathCrossGeneratorServiceNegativeValueTests
{
    [Theory]
    [InlineData("hard")]
    [InlineData("master")]
    public void GenValue_HardAndMaster_CanGenerateNegativeValues(string difficulty)
    {
        var service = new MathCrossGeneratorService();
        var type = typeof(MathCrossGeneratorService);

        var getSettings = type.GetMethod("GetSettings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetSettings method not found.");
        var settings = getSettings.Invoke(null, new object[] { difficulty })
            ?? throw new InvalidOperationException("Settings could not be created.");

        var genValue = type.GetMethod("GenValue", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenValue method not found.");

        var rnd = new Random(12345);
        bool foundNegative = false;

        for (int i = 0; i < 1000; i++)
        {
            var value = (decimal)genValue.Invoke(service, new object[] { settings, rnd })!;
            if (value < 0)
            {
                foundNegative = true;
                break;
            }
        }

        Assert.True(foundNegative, $"Expected difficulty '{difficulty}' to generate at least one negative value.");
    }
}
