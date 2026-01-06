// LogikSpiel/View/DialogPage.xaml.cs
#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace LogikSpiel.View;

public partial class DialogPage : ContentPage
{
    private readonly TaskCompletionSource<bool?> _tcs = new();
    private bool _isClosing = false; // Verhindert Mehrfach-Klicks

    public DialogPage(string title, string message, string accept, string? cancel)
    {
        InitializeComponent();

        TitleLabel.Text = title;
        MessageLabel.Text = message;

        AcceptButton.Text = accept;
        AcceptButton.Clicked += async (_, _) => await CloseAsync(true);

        if (string.IsNullOrWhiteSpace(cancel))
        {
            CancelButton.IsVisible = false;
            // WICHTIG: Wenn nur ein Button da ist, muss er in Spalte 0 starten, um beide zu füllen
            Grid.SetColumn(AcceptButton, 0);
            Grid.SetColumnSpan(AcceptButton, 2);
        }
        else
        {
            CancelButton.Text = cancel;
            CancelButton.Clicked += async (_, _) => await CloseAsync(false);
        }
    }

    public Task<bool?> ResultAsync() => _tcs.Task;

    protected override bool OnBackButtonPressed()
    {
        // Hardware-Back-Button als "Cancel" werten
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(bool? result)
    {
        if (_isClosing) return;
        _isClosing = true;

        // 1. Erst den Dialog vom UI-Stack entfernen
        if (Navigation.ModalStack.Contains(this))
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Navigation.PopModalAsync());
        }

        // 2. Erst JETZT das Ergebnis setzen, damit das ViewModel weiterläuft
        _tcs.TrySetResult(result);
    }
}