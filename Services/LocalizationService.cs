using System.ComponentModel;
using System.Windows;
using TestingSystem.Abstractions;

namespace TestingSystem.Services;

/// <summary>
/// Локалізація на основі словників ресурсів <c>Languages/{UA|EN}.xaml</c>.
/// Ключ словника — той самий, що й у розширенні <c>{loc:T Key}</c>; значення — звичайний <c>sys:String</c>.
/// При зміні мови сервіс піднімає PropertyChanged("Item[]") — усі прив'язки оновлюються миттєво —
/// і підміняє словник у Application.Resources, тому працює й звичайний <c>{DynamicResource Key}</c>.
/// Порожні рядки в XAML зберегти неможливо (обмеження BAML): якщо рядок не потрібен — не додавайте ключ.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<AppLanguage, ResourceDictionary> _dictionaries = new();
    private ResourceDictionary? _active;   // словник, підключений до Application.Resources

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.UA;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public LocalizationService()
    {
        // Вантажимо всі словники одразу: помилка в XAML з'явиться на старті, а не при перемиканні мови.
        foreach (var language in Enum.GetValues<AppLanguage>())
            _dictionaries[language] = Load(language);

        Apply(CurrentLanguage);
    }

    private static ResourceDictionary Load(AppLanguage language) => new()
    {
        Source = new Uri($"pack://application:,,,/Languages/{language}.xaml", UriKind.Absolute)
    };

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            var value = Lookup(CurrentLanguage, key);
            return value ?? $"[{key}]";   // видно, що ключа немає
        }
    }

    /// <summary>Ключ поточної мови; якщо його там немає — англійський варіант (щоб інтерфейс не ламався).</summary>
    private string? Lookup(AppLanguage language, string key)
    {
        if (_dictionaries.TryGetValue(language, out var dictionary) && dictionary.Contains(key))
            return dictionary[key] as string;

        if (language != AppLanguage.EN && _dictionaries[AppLanguage.EN].Contains(key))
            return _dictionaries[AppLanguage.EN][key] as string;

        return null;
    }

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    public void SetLanguage(AppLanguage language)
    {
        if (language == CurrentLanguage) return;
        CurrentLanguage = language;
        Apply(language);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));   // оновити всі {loc:T ...}
        LanguageChanged?.Invoke(this, EventArgs.Empty);                          // оновити тексти, зібрані в коді
    }

    /// <summary>Підключає словник поточної мови до Application.Resources (для DynamicResource / FindResource).</summary>
    private void Apply(AppLanguage language)
    {
        var app = Application.Current;
        if (app is null) return;   // наприклад, у тестах без WPF-застосунку

        if (_active is not null) app.Resources.MergedDictionaries.Remove(_active);
        _active = _dictionaries[language];
        app.Resources.MergedDictionaries.Add(_active);
    }
}

public static class LocalizationExtensions
{
    /// <summary>Локалізований опис результату операції: ключ = "Err_" + ім'я значення AuthResult.</summary>
    public static string Describe(this ILocalizationService loc, AuthResult result)
        => loc.Format($"Err_{result}", AuthService.MinPasswordLength);
}
