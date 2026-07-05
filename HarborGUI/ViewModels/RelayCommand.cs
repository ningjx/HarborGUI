using System.Windows.Input;

namespace HarborGUI.ViewModels;

/// <summary>
/// 通用 ICommand 实现，支持异步执行
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task>? _executeAsync;
    private readonly Action<object?>? _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isExecuting;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Func<Task> executeAsync, Func<object?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            if (_executeAsync != null)
                await _executeAsync();
            else
                _execute?.Invoke(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        // 需要在 UI 线程触发
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        else
            System.Windows.Application.Current?.Dispatcher.Invoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }
}
