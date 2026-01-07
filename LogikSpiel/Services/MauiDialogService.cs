// LogikSpiel/Services/MauiDialogService.cs
using LogikSpiel.View;
using LogikSpiel.Services.Localization;
using Microsoft.Maui.ApplicationModel;

namespace LogikSpiel.Services;

public sealed class MauiDialogService : IDialogService
{
    private Page? CurrentPage => Shell.Current?.CurrentPage
                                 ?? Application.Current?.Windows?.FirstOrDefault()?.Page;

    public async Task AlertAsync(string title, string message, string? ok = null)
    {
        var okLabel = string.IsNullOrWhiteSpace(ok) ? LocalizationService.GetString("Common_Ok") : ok;
        await ShowDialogAsync(title, message, okLabel, cancel: null);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string? accept = null, string? cancel = null)
    {
        var acceptLabel = string.IsNullOrWhiteSpace(accept) ? LocalizationService.GetString("Common_Yes") : accept;
        var cancelLabel = string.IsNullOrWhiteSpace(cancel) ? LocalizationService.GetString("Common_No") : cancel;
        var result = await ShowDialogAsync(title, message, acceptLabel, cancelLabel);
        return result ?? false;
    }

    public async Task<string?> PickAsync(string title, string cancel, params string[] options)
    {
        var p = CurrentPage;
        if (p is null) return null;
        return await p.DisplayActionSheetAsync(title, cancel, null, options);
    }

    private async Task<bool?> ShowDialogAsync(string title, string message, string accept, string? cancel)
    {
        var p = CurrentPage;
        if (p is null) return null;

        // Gesamten Dialog-Prozess auf dem MainThread sicherstellen
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var dialog = new DialogPage(title, message, accept, cancel);
            await p.Navigation.PushModalAsync(dialog);
            return await dialog.ResultAsync();
        });
    }
}