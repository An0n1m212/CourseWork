using TestingSystem.Abstractions;

namespace TestingSystem.Services;

/// <summary>
/// Оркеструє блокування сеансу. Залежить лише від абстракцій (DIP):
/// монітор бездіяльності, сервіс автентифікації, показ екрана блокування і навігація.
/// Життєвий цикл монітора прив'язаний до стану входу: користувач увійшов — відлік іде, вийшов — зупинено.
/// </summary>
public sealed class SessionLockService : ISessionLockService
{
    private readonly IInactivityMonitor _monitor;
    private readonly IAuthService _auth;
    private readonly ILockScreenPresenter _presenter;
    private readonly ISessionNavigator _navigator;
    private readonly InactivitySettings _settings;

    private int _failedAttempts;

    public bool IsLocked { get; private set; }
    public int AttemptsLeft => Math.Max(0, _settings.MaxUnlockAttempts - _failedAttempts);

    public SessionLockService(IInactivityMonitor monitor, IAuthService auth, ILockScreenPresenter presenter,
                              ISessionNavigator navigator, InactivitySettings settings)
    {
        _monitor = monitor;
        _auth = auth;
        _presenter = presenter;
        _navigator = navigator;
        _settings = settings;

        _auth.CurrentUserChanged += OnCurrentUserChanged;
        _monitor.TimedOut += (_, _) => Lock();
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        if (_auth.GetCurrentUser() is null) _monitor.Stop();
        else if (!IsLocked) _monitor.Start();
    }

    public void Lock()
    {
        if (IsLocked || _auth.GetCurrentUser() is null) return;

        IsLocked = true;
        _failedAttempts = 0;
        _monitor.Stop();

        try
        {
            _presenter.ShowLockScreen(this);      // блокуючий виклик: повернеться після розблокування або виходу
        }
        finally
        {
            IsLocked = false;
            if (_auth.GetCurrentUser() != null) _monitor.Start();   // розблокували — знову стежимо за бездіяльністю
        }
    }

    public UnlockResult TryUnlock(string password)
    {
        if (!IsLocked) return UnlockResult.Success;

        if (_auth.Reauthenticate(password))
        {
            _failedAttempts = 0;
            return UnlockResult.Success;
        }

        _failedAttempts++;
        if (_failedAttempts >= _settings.MaxUnlockAttempts)
        {
            ForceLogout();                        // захист від підбору пароля
            return UnlockResult.TooManyAttempts;
        }
        return UnlockResult.WrongPassword;
    }

    public void ForceLogout() => _navigator.NavigateToLogin();
}
