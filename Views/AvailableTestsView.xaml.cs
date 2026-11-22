using System.Windows;
using System.Windows.Controls;
using TestingSystem.Models;
using TestingSystem.Services;

namespace TestingSystem.Views;

/// <summary>Дані картки тесту (проєкція Test для відображення).</summary>
public record TestCard(
    Test Source, string Title, string Category, string Description,
    string TimeLine, string RulesLine, string AttemptsLine, string LockLine, bool CanStart);

/// <summary>Каталог доступних тестів (картки), пошук, фільтр за категоріями та запуск проходження.</summary>
public partial class AvailableTestsView : UserControl
{
    private readonly AppServices _s;
    private IReadOnlyList<TestCard> _all = Array.Empty<TestCard>();
    private bool _isReloadingFilters;

    public AvailableTestsView(AppServices services)
    {
        InitializeComponent();
        _s = services;

        Loaded += (_, _) =>
        {
            _s.Localization.LanguageChanged -= OnLanguageChanged;
            _s.Localization.LanguageChanged += OnLanguageChanged;
            Reload();
        };
        Unloaded += (_, _) => _s.Localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Reload();

    private void Reload()
    {
        var user = _s.Auth.GetCurrentUser();
        if (user is null) return;

        RefreshCategoryFilter();

        var loc = _s.Localization;
        var categories = _s.Categories.GetAll().ToDictionary(c => c.Id, c => c.Name);

        _all = _s.Sessions.GetAvailableTests(user).Select(t =>
        {
            int used = _s.Sessions.GetAttemptsUsed(user.Id, t.Id);
            bool hasAttempts = t.MaxAttempts == 0 || used < t.MaxAttempts;

            return new TestCard(
                t, t.Title,
                t.CategoryId is int cid && categories.TryGetValue(cid, out var cn) ? cn : "",
                t.Description,
                "⏱ " + (t.TimeLimitMinutes > 0 ? loc.Format("Card_Time", t.TimeLimitMinutes) : loc["Card_NoLimit"]),
                "🎯 " + loc.Format("Card_Rules", t.PassScorePercent, t.Questions.Count),
                "🔁 " + (t.MaxAttempts > 0 ? loc.Format("Card_Attempts", used, t.MaxAttempts) : loc.Format("Card_AttemptsUnlimited", used)),
                string.IsNullOrWhiteSpace(t.AccessCode) ? "" : "🔒 " + loc["Card_Protected"],
                hasAttempts);
        }).ToList();

        ApplyFilters();
    }

    /// <summary>Пункт Id = 0 — службовий «Усі категорії»; вибір зберігається після зміни мови.</summary>
    private void RefreshCategoryFilter()
    {
        int selectedId = (CategoryFilter.SelectedItem as Category)?.Id ?? 0;

        var options = new List<Category> { new() { Id = 0, Name = _s.Localization["Filter_AllCategories"] } };
        options.AddRange(_s.Categories.GetAll());

        _isReloadingFilters = true;
        CategoryFilter.ItemsSource = options;
        CategoryFilter.SelectedItem = options.FirstOrDefault(c => c.Id == selectedId) ?? options[0];
        _isReloadingFilters = false;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isReloadingFilters || CategoryFilter.SelectedItem is null) return;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var loc = _s.Localization;
        int categoryId = (CategoryFilter.SelectedItem as Category)?.Id ?? 0;
        string query = SearchBox.Text.Trim();

        var filtered = _all
            .Where(c => categoryId == 0 || c.Source.CategoryId == categoryId)
            .Where(c => query.Length == 0
                        || c.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || c.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || c.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Cards.ItemsSource = filtered;
        EmptyText.Text = _all.Count == 0 ? loc["Msg_NoTests"] : loc["Filter_NoMatches"];
        EmptyText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        CategoryFilter.SelectedIndex = 0;
        ApplyFilters();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TestCard card) return;
        var user = _s.Auth.GetCurrentUser()!;
        var loc = _s.Localization;

        // Тест захищений пін-кодом — запитуємо його до створення сесії
        string? code = null;
        if (!string.IsNullOrWhiteSpace(card.Source.AccessCode))
        {
            var dialog = new AccessCodeDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            code = dialog.Code;
        }

        var (error, controller) = _s.Sessions.TryStart(user, card.Source.Id, code);
        if (error != StartError.None)
        {
            MessageBox.Show(loc[$"StartErr_{error}"]);
            Reload();
            return;
        }

        new TestRunWindow(_s, controller!) { Owner = Window.GetWindow(this) }.ShowDialog();
        Reload();   // оновити лічильник спроб
    }
}
