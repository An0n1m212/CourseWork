using TestingSystem.Models;

namespace TestingSystem.Abstractions;

/// <summary>Налаштування блокування за бездіяльністю (змінюються в одному місці — Composition Root).</summary>
public sealed record InactivitySettings(TimeSpan Timeout, int MaxUnlockAttempts = 5);

/// <summary>Джерело поточного часу (підмінюється в тестах — Dependency Inversion).</summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>Періодичний «тік» (у WPF — DispatcherTimer, у тестах — ручний).</summary>
public interface ITicker
{
    event EventHandler? Tick;
    void Start();
    void Stop();
}

/// <summary>Джерело подій активності користувача (миша, клавіатура, дотики).</summary>
public interface IActivitySource
{
    event EventHandler? ActivityDetected;
}

/// <summary>Відстежує бездіяльність і повідомляє, що ліміт часу вичерпано.</summary>
public interface IInactivityMonitor
{
    TimeSpan Timeout { get; }
    bool IsRunning { get; }

    /// <summary>Запускає (або перезапускає) відлік бездіяльності.</summary>
    void Start();
    void Stop();

    /// <summary>Призупиняє блокування (наприклад, на час проходження тесту): using (monitor.Suspend()) { ... }</summary>
    IDisposable Suspend();

    /// <summary>Спрацює один раз; після нього монітор зупинено до наступного Start().</summary>
    event EventHandler? TimedOut;
}

public enum UnlockResult { Success, WrongPassword, TooManyAttempts }

/// <summary>Бізнес-логіка блокування сеансу: коли блокувати, як знімати, коли примусово виходити.</summary>
public interface ISessionLockService
{
    bool IsLocked { get; }
    int AttemptsLeft { get; }

    /// <summary>Блокує сеанс негайно (викликається і за таймаутом бездіяльності).</summary>
    void Lock();

    /// <summary>Повторна автентифікація поточного користувача.</summary>
    UnlockResult TryUnlock(string password);

    /// <summary>Вийти з акаунта прямо з екрана блокування.</summary>
    void ForceLogout();
}

/// <summary>Показує екран блокування. Виклик блокуючий: повертається, коли сеанс розблоковано або завершено.</summary>
public interface ILockScreenPresenter
{
    void ShowLockScreen(ISessionLockService lockService);
}

/// <summary>Навігація між станами застосунку (прибирає залежність сервісів від конкретних вікон).</summary>
public interface ISessionNavigator
{
    /// <summary>Завершує сеанс, закриває робочі вікна і показує екран входу.</summary>
    void NavigateToLogin();
}
