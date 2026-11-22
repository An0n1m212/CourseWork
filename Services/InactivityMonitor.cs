using TestingSystem.Abstractions;

namespace TestingSystem.Services;

/// <summary>
/// Відраховує час без користувацької активності. Відповідає ЛИШЕ за це:
/// що робити після спливання часу, вирішує підписник події TimedOut (SessionLockService).
/// Час береться з IClock за «настінним» годинником, тому після сну/гібернації ПК таймаут спрацює одразу.
/// </summary>
public sealed class InactivityMonitor : IInactivityMonitor
{
    private readonly IActivitySource _activity;
    private readonly IClock _clock;
    private readonly ITicker _ticker;
    private readonly InactivitySettings _settings;

    private DateTime _lastActivity;
    private int _suspended;

    public event EventHandler? TimedOut;

    public TimeSpan Timeout => _settings.Timeout;
    public bool IsRunning { get; private set; }

    public InactivityMonitor(IActivitySource activity, IClock clock, ITicker ticker, InactivitySettings settings)
    {
        _activity = activity;
        _clock = clock;
        _ticker = ticker;
        _settings = settings;
    }

    /// <summary>Ідемпотентно: повторний виклик просто скидає відлік.</summary>
    public void Start()
    {
        _lastActivity = _clock.Now;
        if (IsRunning) return;

        IsRunning = true;
        _activity.ActivityDetected += OnActivity;
        _ticker.Tick += OnTick;
        _ticker.Start();
    }

    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        _activity.ActivityDetected -= OnActivity;
        _ticker.Tick -= OnTick;
        _ticker.Stop();
    }

    public IDisposable Suspend()
    {
        _suspended++;
        return new SuspendScope(this);
    }

    private void OnActivity(object? sender, EventArgs e) => _lastActivity = _clock.Now;

    private void OnTick(object? sender, EventArgs e)
    {
        if (!IsRunning) return;

        if (_suspended > 0)                       // наприклад, іде тест: відлік не ведеться
        {
            _lastActivity = _clock.Now;
            return;
        }

        if (_clock.Now - _lastActivity < _settings.Timeout) return;

        Stop();                                   // спрацювати лише один раз; перезапуск — після розблокування
        TimedOut?.Invoke(this, EventArgs.Empty);
    }

    private sealed class SuspendScope : IDisposable
    {
        private InactivityMonitor? _owner;
        public SuspendScope(InactivityMonitor owner) => _owner = owner;

        public void Dispose()
        {
            if (_owner is null) return;
            _owner._suspended--;
            _owner._lastActivity = _owner._clock.Now;   // після паузи відлік починається заново
            _owner = null;
        }
    }
}
