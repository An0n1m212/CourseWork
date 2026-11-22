using System.Windows;
using System.Windows.Controls;
using TestingSystem.Abstractions;

namespace TestingSystem.Services;

/// <summary>
/// Світла/темна тема. Два шари:
///  1) Application.ThemeMode (вбудована Fluent-тема WPF, .NET 9+) перефарбовує стандартні контроли;
///  2) словник Themes/{Light|Dark}Colors.xaml дає застосунку власні пензлі (картки, вторинний текст, помилки...).
/// Екрани використовують ці пензлі через DynamicResource, тому перемикання діє одразу.
/// </summary>
public class ThemeService : IThemeService
{
    private ResourceDictionary? _palette;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;
    public event EventHandler? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        var app = Application.Current;

        var palette = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{theme}Colors.xaml", UriKind.Absolute)
        };
        if (_palette != null) app.Resources.MergedDictionaries.Remove(_palette);
        app.Resources.MergedDictionaries.Add(palette);
        _palette = palette;

        var mode = theme == AppTheme.Dark ? ThemeMode.Dark : ThemeMode.Light;
        app.ThemeMode = mode;
        foreach (Window w in app.Windows) w.ThemeMode = mode;   // заголовок вікна та вже відкриті вікна

        FixListBoxItemStyle(app);

        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Стиль ListBoxItem з Fluent-теми прив'язує HorizontalContentAlignment/VerticalContentAlignment до батьківського
    /// ItemsControl; при заміні вмісту списку це дає нешкідливі повідомлення «Не вдається знайти джерело: RelativeSource
    /// FindAncestor» у вікні «Помилки прив'язки XAML». Явно задані значення замінюють прив'язку, а решта оформлення
    /// береться з теми (BasedOn). Якщо стиль теми не знайдено — нічого не змінюємо.
    /// </summary>
    private static void FixListBoxItemStyle(Application app)
    {
        app.Resources.Remove(typeof(ListBoxItem));                       // прибираємо свій минулий стиль (інакше ланцюжок BasedOn зростає)
        if (app.TryFindResource(typeof(ListBoxItem)) is not Style themeStyle) return;

        var style = new Style(typeof(ListBoxItem), themeStyle);
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        app.Resources[typeof(ListBoxItem)] = style;
    }
}
