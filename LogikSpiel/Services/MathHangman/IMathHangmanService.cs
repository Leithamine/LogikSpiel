#nullable enable
using LogikSpiel.Model.MathHangman;
using LogikSpiel.ViewModel;

namespace LogikSpiel.Services.MathHangman;

public interface IMathHangmanService
{
    int GenerateSecretNumber(int seed, string difficultyKey);
    IReadOnlyList<NumberProperty> GetAllTrueProperties(int n);
}
