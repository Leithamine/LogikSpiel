namespace LogikSpiel.Services;

public interface IDialogService
{
    Task<string?> PickAsync(string title, string cancel, params string[] options);
    Task AlertAsync(string title, string message, string? ok = null);

    // NEU: Diese Zeile hat gefehlt
    Task<bool> ConfirmAsync(string title, string message, string? accept = null, string? cancel = null);
}