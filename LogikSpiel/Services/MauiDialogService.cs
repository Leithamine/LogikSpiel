namespace LogikSpiel.Services;

public sealed class MauiDialogService : IDialogService
{
    private Page? CurrentPage => Application.Current?.Windows?.FirstOrDefault()?.Page;

    public async Task AlertAsync(string title, string message, string ok = "OK")
    {
        var p = CurrentPage;
        if (p is null) return;
        await p.DisplayAlertAsync(title, message, ok);
    }

    public async Task<string?> PickAsync(string title, string cancel, params string[] options)
    {
        var p = CurrentPage;
        if (p is null) return null;

#pragma warning disable CS0618
        // (falls DisplayActionSheet mal als obsolete markiert ist)
        var result = await p.DisplayActionSheet(title, cancel, null, options);
#pragma warning restore CS0618

        if (string.Equals(result, cancel, StringComparison.OrdinalIgnoreCase)) return null;
        return result;
    }
}
