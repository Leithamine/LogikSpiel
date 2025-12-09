using LogikSpiel.Services;

public sealed class MauiDialogService : IDialogService
{
    public Task AlertAsync(string title, string message, string cancel)
        => Shell.Current?.DisplayAlertAsync(title, message, cancel) ?? Task.CompletedTask;
}