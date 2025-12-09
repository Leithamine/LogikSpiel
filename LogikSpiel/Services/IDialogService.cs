namespace LogikSpiel.Services;

public interface IDialogService
{
    Task AlertAsync(string title, string message, string cancel);
}