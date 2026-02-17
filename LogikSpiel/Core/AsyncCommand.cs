using System.Diagnostics;
using System.Windows.Input;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Threading; // WICHTIG: Für Interlocked

namespace LogikSpiel.Core;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private int _isExecuting; // 0 = false, 1 = true (für Interlocked)

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
        => _isExecuting == 0 && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        // Atomare Prüfung: Wenn _isExecuting 0 war, setze es auf 1 und gib true zurück
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            return; // Bereits am Ausführen

        if (!CanExecute(parameter))
        {
            Interlocked.Exchange(ref _isExecuting, 0); // Zurücksetzen
            return;
        }

        try
        {
            RaiseCanExecuteChanged();
            await _execute();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"COMMAND ERROR: {ex}");

            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        var dialogService = AppServices.Get<IDialogService>();
                        await dialogService.AlertAsync(
                            LocalizationService.GetString("Common_ErrorTitle"),
                            ex.Message,
                            LocalizationService.GetString("Common_Ok"));
                    }
                    catch { /* Silent fail */ }
                });
            }
            catch { }
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0); // Atomar zurücksetzen
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private int _isExecuting; // 0 = false, 1 = true (für Interlocked)

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
        => _isExecuting == 0 && (_canExecute?.Invoke((T?)parameter) ?? true);

    public async void Execute(object? parameter)
    {
        // Atomare Prüfung: Wenn _isExecuting 0 war, setze es auf 1 und gib true zurück
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            return; // Bereits am Ausführen

        if (!CanExecute(parameter))
        {
            Interlocked.Exchange(ref _isExecuting, 0); // Zurücksetzen
            return;
        }

        try
        {
            RaiseCanExecuteChanged();
            await _execute((T?)parameter);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"COMMAND ERROR: {ex}");

            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        var dialogService = AppServices.Get<IDialogService>();
                        await dialogService.AlertAsync(
                            LocalizationService.GetString("Common_ErrorTitle"),
                            ex.Message,
                            LocalizationService.GetString("Common_Ok"));
                    }
                    catch { /* Silent fail */ }
                });
            }
            catch { }
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0); // Atomar zurücksetzen
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}