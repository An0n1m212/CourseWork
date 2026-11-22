using System.ComponentModel;
using TestingSystem.Abstractions;

namespace TestingSystem.Services;

/// <summary>
/// Локалізація на основі таблиці рядків (див. Strings.cs). При зміні мови піднімає
/// PropertyChanged("Item[]") — усі прив'язки виду {loc:T Key} оновлюються миттєво.
/// Джерело рядків легко замінити на .resx/JSON, не змінюючи інтерфейс.
/// </summary>
public class LocalizationService : ILocalizationService
{
    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.UA;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public string this[string key]
    {
        get
        {
            if (!Strings.Table.TryGetValue(key, out var t)) return $"[{key}]";   // видно, що ключа немає
            return CurrentLanguage switch { AppLanguage.UA => t.Ua, AppLanguage.RU => t.Ru, _ => t.En };
        }
    }

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    public void SetLanguage(AppLanguage language)
    {
        if (language == CurrentLanguage) return;
        CurrentLanguage = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));   // оновити всі {loc:T ...}
        LanguageChanged?.Invoke(this, EventArgs.Empty);                          // оновити тексти, зібрані в коді
    }
}

public static class LocalizationExtensions
{
    /// <summary>Локалізований опис результату операції: ключ = "Err_" + ім'я значення AuthResult.</summary>
    public static string Describe(this ILocalizationService loc, AuthResult result)
        => loc.Format($"Err_{result}", AuthService.MinPasswordLength);
}
