using System.Diagnostics;
using System.Windows.Input;
using LogikSpiel.Services;
using LogikSpiel.Services.Localization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Threading;

namespace LogikSpiel.Core;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private int _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    private bool EvaluateCanExecute()
    {
        try
        {
            return _canExecute?.Invoke() ?? true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CANEXECUTE ERROR: {ex}");
            return false;
        }
    }

    public bool CanExecute(object? parameter)
        => _isExecuting == 0 && EvaluateCanExecute();

    public async void Execute(object? parameter)
    {
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            return;

        if (!EvaluateCanExecute())
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            return;
        }

        try
        {
            RaiseCanExecuteChanged();
            await _execute();
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static async Task HandleExceptionAsync(Exception ex)
    {
        Debug.WriteLine($"COMMAND ERROR: {ex}");

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
            catch
            {
                if (Application.Current?.MainPage is Page page)
                {
                    await page.DisplayAlert(
                        LocalizationService.GetString("Common_ErrorTitle"),
                        ex.Message,
                        LocalizationService.GetString("Common_Ok"));
                }
            }
        });
    }
}

public sealed class AsyncCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private int _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public AsyncCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    private bool EvaluateCanExecute(T? parameter)
    {
        try
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CANEXECUTE ERROR: {ex}");
            return false;
        }
    }

    public bool CanExecute(object? parameter)
        => _isExecuting == 0 && EvaluateCanExecute((T?)parameter);

    public async void Execute(object? parameter)
    {
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            return;

        var typedParameter = (T?)parameter;
        if (!EvaluateCanExecute(typedParameter))
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            return;
        }

        try
        {
            RaiseCanExecuteChanged();
            await _execute(typedParameter);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isExecuting, 0);
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static async Task HandleExceptionAsync(Exception ex)
    {
        Debug.WriteLine($"COMMAND ERROR: {ex}");

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
            catch
            {
                if (Application.Current?.MainPage is Page page)
                {
                    await page.DisplayAlert(
                        LocalizationService.GetString("Common_ErrorTitle"),
                        ex.Message,
                        LocalizationService.GetString("Common_Ok"));
                }
            }
        });
    }
}
