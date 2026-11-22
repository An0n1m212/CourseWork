using System.ComponentModel;
using System.Windows;
using TestingSystem.ViewModels;

namespace TestingSystem.Views;

/// <summary>Code-behind лише для суто візуальних задач: фокус і заборона закриття без розблокування.</summary>
public partial class SessionLockWindow : Window
{
    private bool _allowClose;

    public SessionLockWindow(SessionLockViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += (_, _) =>
        {
            _allowClose = true;
            Close();
        };

        Loaded += (_, _) =>
        {
            Activate();
            PasswordInput.Focus();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Alt+F4 не повинен обходити блокування (але не заважаємо завершенню роботи системи/застосунку)
        if (!_allowClose && !Application.Current.Dispatcher.HasShutdownStarted) e.Cancel = true;
        base.OnClosing(e);
    }
}
