using System.Windows;

namespace TestingSystem.Services;

public enum ViolationKind { FocusLost, Minimized }

public sealed class ViolationEventArgs : EventArgs
{
    public int Count { get; }
    public ViolationKind Kind { get; }
    public ViolationEventArgs(int count, ViolationKind kind) { Count = count; Kind = kind; }
}

/// <summary>
/// Античит: фіксує втрату фокусу вікном тесту (Alt+Tab, клік по іншому вікну) і згортання.
/// Перевищення таймера обробляє TestSessionController. Вікна-діалоги самого застосунку
/// (вибір файлу, підтвердження) потрібно обгортати в Suspend(), інакше вони вважатимуться порушенням.
/// </summary>
public sealed class AntiCheatMonitor : IDisposable
{
    private readonly Window _window;
    private int _suspendDepth;

    public int MaxViolations { get; }
    public int Violations { get; private set; }

    public event EventHandler<ViolationEventArgs>? ViolationRegistered;
    public event EventHandler? LimitReached;

    public AntiCheatMonitor(Window window, int maxViolations)
    {
        _window = window;
        MaxViolations = maxViolations;
        _window.Deactivated += OnDeactivated;   // спрацьовує і при Alt+Tab, і при згортанні
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_suspendDepth > 0) return;

        Violations++;
        var kind = _window.WindowState == WindowState.Minimized ? ViolationKind.Minimized : ViolationKind.FocusLost;
        ViolationRegistered?.Invoke(this, new ViolationEventArgs(Violations, kind));

        if (Violations >= MaxViolations) LimitReached?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Тимчасово вимикає облік (на час власних діалогів): using (monitor.Suspend()) { ... }</summary>
    public IDisposable Suspend()
    {
        _suspendDepth++;
        return new Scope(this);
    }

    public void Dispose() => _window.Deactivated -= OnDeactivated;

    private sealed class Scope : IDisposable
    {
        private AntiCheatMonitor? _owner;
        public Scope(AntiCheatMonitor owner) => _owner = owner;
        public void Dispose()
        {
            if (_owner is null) return;
            _owner._suspendDepth--;
            _owner = null;
        }
    }
}
