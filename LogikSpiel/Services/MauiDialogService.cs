using Microsoft.Maui.Controls;

namespace LogikSpiel.Services;

public sealed class MauiDialogService : IDialogService
{
    // Hilfsproperty, um die aktuelle Seite zu finden
    private Page? CurrentPage => Application.Current?.Windows?.FirstOrDefault()?.Page;

    public async Task AlertAsync(string title, string message, string ok = "OK")
    {
        var p = CurrentPage;
        if (p is null) return;
        await p.DisplayAlert(title, message, ok);
    }

    // NEU: Implementierung für ConfirmAsync
    public async Task<bool> ConfirmAsync(string title, string message, string accept = "Ja", string cancel = "Nein")
    {
        var p = CurrentPage;
        if (p is null) return false;
        // Ruft den systemeigenen Ja/Nein Dialog auf
        return await p.DisplayAlert(title, message, accept, cancel);
    }

    public async Task<string?> PickAsync(string title, string cancel, params string[] options)
    {
        var p = CurrentPage;
        if (p is null) return null;

#pragma warning disable CS0618
        var result = await p.DisplayActionSheet(title, cancel, null, options);
#pragma warning restore CS0618

        if (string.Equals(result, cancel, StringComparison.OrdinalIgnoreCase)) return null;
        return result;
    }
}
