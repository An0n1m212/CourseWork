using System.Windows;
using System.Windows.Controls;
using TestingSystem.Abstractions;

namespace TestingSystem.Views;

/// <summary>
/// Перемикачі мови (UA/RU/EN) і теми (Light/Dark). Використовується і в шапці, і в профілі:
/// обидва екземпляри синхронізуються через події сервісів.
/// </summary>
public partial class SettingsPanel : UserControl
{
    private ILocalizationService? _loc;
    private IThemeService? _theme;
    private bool _updating, _subscribed;

    public SettingsPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
    }

    public void Init(ILocalizationService loc, IThemeService theme)
    {
        _loc = loc;
        _theme = theme;
        LangCombo.ItemsSource = Enum.GetValues<AppLanguage>();
        if (IsLoaded) Subscribe(); else Refresh();
    }

    private void Subscribe()
    {
        if (_loc is null || _theme is null || _subscribed) return;
        _loc.LanguageChanged += OnChanged;
        _theme.ThemeChanged += OnChanged;
        _subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (_loc is null || _theme is null || !_subscribed) return;
        _loc.LanguageChanged -= OnChanged;
        _theme.ThemeChanged -= OnChanged;
        _subscribed = false;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_loc is null || _theme is null) return;
        _updating = true;
        LangCombo.SelectedItem = _loc.CurrentLanguage;
        ThemeButton.Content = _theme.CurrentTheme == AppTheme.Light
            ? "☀️ " + _loc["Theme_Light"]
            : "🌙 " + _loc["Theme_Dark"];
        _updating = false;
    }

    private void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || _loc is null || LangCombo.SelectedItem is not AppLanguage lang) return;
        _loc.SetLanguage(lang);
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
        => _theme?.SetTheme(_theme.CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
}
