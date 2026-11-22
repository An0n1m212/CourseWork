using System.Windows.Input;
using TestingSystem.Abstractions;

namespace TestingSystem.Infrastructure;

/// <summary>
/// Фіксує введення користувача в УСІХ вікнах застосунку через InputManager (миша, клавіатура, дотики, перо).
/// Живе весь час роботи застосунку, тому відписка не потрібна. Створювати потрібно в UI-потоці.
/// </summary>
public sealed class WpfInputActivitySource : IActivitySource
{
    public event EventHandler? ActivityDetected;

    public WpfInputActivitySource() => InputManager.Current.PreProcessInput += OnPreProcessInput;

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs or TouchEventArgs or StylusEventArgs)
            ActivityDetected?.Invoke(this, EventArgs.Empty);
    }
}
