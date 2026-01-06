#nullable enable
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace LogikSpiel.View;

public partial class DialogPage : ContentPage
{
    private readonly TaskCompletionSource<bool?> _tcs = new();

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
        _ = CloseAsync(null);
        return true;
    }

    private async Task CloseAsync(bool? result)
    {
        if (!_tcs.TrySetResult(result))
            return;

        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync();
    }
}
