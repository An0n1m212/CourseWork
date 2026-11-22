using System.Windows.Data;
using System.Windows.Markup;
using TestingSystem.Abstractions;

namespace TestingSystem.Infrastructure;

/// <summary>
/// XAML-розширення локалізації: Text="{loc:T Login_Title}".
/// Створює прив'язку до індексатора ILocalizationService, тому текст змінюється одразу під час зміни мови.
/// Service задається один раз у Composition Root (App.OnStartup) — це єдине «статичне» місце,
/// потрібне лише тому, що в XAML-розширень немає доступу до DI.
/// </summary>
public class TExtension : MarkupExtension
{
    public static ILocalizationService? Service { get; set; }

    public string Key { get; set; }

    public TExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Service is null) return Key;   // режим дизайнера

        var binding = new Binding($"[{Key}]") { Source = Service, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
