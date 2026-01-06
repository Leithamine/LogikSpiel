using System.Diagnostics;
using System.Windows.Input;
using LogikSpiel.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace LogikSpiel.Core;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    // In AsyncCommand.cs

    public async void Execute(object? parameter)
    {
        // 1. Sofort abbrechen, wenn wir schon arbeiten
        if (_isExecuting) return;

        // 2. Prüfen, ob wir überhaupt dürfen
        if (!CanExecute(parameter)) return;

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged(); // Deaktiviert Buttons visuell

            await _execute();
        }
        catch (Exception ex)
        {
            // Fehler fangen, damit die App nicht crasht
            System.Diagnostics.Debug.WriteLine($"COMMAND ERROR: {ex}");
        }
        finally
        {
            // Erst hier wieder freigeben
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    // Dasselbe auch für AsyncCommand<T> machen!

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute((T?)parameter);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var dialogService = Application.Current?.Handler?.MauiContext?.Services?.GetService<IDialogService>();
                    if (dialogService is not null)
                    {
                        await dialogService.AlertAsync("Fehler", ex.ToString(), "OK");
                    }
                });
            }
            catch { }
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
