using LogikSpiel.View;
using Microsoft.Maui.ApplicationModel;

namespace LogikSpiel.Services;

public sealed class MauiDialogService : IDialogService
{
    // Hilfsproperty, um die aktuelle Seite zu finden
    private Page? CurrentPage => Shell.Current?.CurrentPage
                                 ?? Application.Current?.Windows?.FirstOrDefault()?.Page;

    public async Task AlertAsync(string title, string message, string ok = "OK")
    {
        await ShowDialogAsync(title, message, ok, cancel: null);
    }

    // NEU: Implementierung für ConfirmAsync
    public async Task<bool> ConfirmAsync(string title, string message, string accept = "Ja", string cancel = "Nein")
    {
        var result = await ShowDialogAsync(title, message, accept, cancel);
        return result ?? false;
    }

    public async Task<string?> PickAsync(string title, string cancel, params string[] options)
    {
        var p = CurrentPage;
        if (p is null) return null;

#pragma warning disable CS0618
        var result = await p.DisplayActionSheetAsync(title, cancel, null, options);
#pragma warning restore CS0618

        if (string.Equals(result, cancel, StringComparison.OrdinalIgnoreCase)) return null;
        return result;
    }

    private async Task<bool?> ShowDialogAsync(string title, string message, string accept, string? cancel)
    {
        var p = CurrentPage;
        if (p is null) return null;

        var dialog = new DialogPage(title, message, accept, cancel);

        await MainThread.InvokeOnMainThreadAsync(async () =>
            await p.Navigation.PushModalAsync(dialog));

        return await dialog.ResultAsync();
    }
}
