using System.Windows;
using System.Windows.Controls;
using TestingSystem.Models;

namespace TestingSystem.Views;

public record TestListItem(Test Source, string Title, string Description, string Meta);

public partial class TestsManagementView : UserControl
{
    private readonly AppServices _s;
    private IReadOnlyList<TestListItem> _all = Array.Empty<TestListItem>();
    private bool _isReloadingFilters;

    public TestsManagementView(AppServices services)
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

    /// <summary>Тести, якими користувач має право керувати (вчитель — свої, адмін — усі), з пошуком і фільтрами.</summary>
    private void Reload()
    {
        RefreshCategoryFilter();

        var loc = _s.Localization;
        var categories = _s.Categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var groups = _s.Groups.GetAll().ToDictionary(g => g.Id, g => g.Name);

        _all = _s.Tests.GetAll().Where(_s.Auth.CanManage).Select(t =>
        {
            string cat = t.CategoryId is int cid && categories.TryGetValue(cid, out var cn) ? cn : "—";
            string assigned = t.AssignedGroupIds.Count == 0
                ? loc["Meta_AllGroups"]
                : string.Join(", ", t.AssignedGroupIds.Select(id => groups.TryGetValue(id, out var gn) ? gn : "?"));

            string meta = string.Join("  •  ", new[]
            {
                $"{loc["Lbl_Category"]}: {cat}",
                t.TimeLimitMinutes > 0 ? loc.Format("Card_Time", t.TimeLimitMinutes) : loc["Card_NoLimit"],
                loc.Format("Card_Rules", t.PassScorePercent, t.Questions.Count),
                t.MaxAttempts > 0 ? loc.Format("Meta_MaxAttempts", t.MaxAttempts) : loc["Meta_UnlimitedAttempts"],
                string.IsNullOrWhiteSpace(t.AccessCode) ? "" : "🔒 " + loc["Card_Protected"],
                $"{loc["Lbl_Groups"]}: {assigned}"
            }.Where(x => x.Length > 0));

            return new TestListItem(t, t.Title, t.Description, meta);
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
            .Where(i => categoryId == 0 || i.Source.CategoryId == categoryId)
            .Where(i => query.Length == 0
                        || i.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || i.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || i.Meta.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        TestsList.ItemsSource = filtered;
        EmptyText.Text = _all.Count == 0 ? loc["Msg_NoTestsYet"] : loc["Filter_NoMatches"];
        EmptyText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        CategoryFilter.SelectedIndex = 0;
        ApplyFilters();
    }

    private void Create_Click(object sender, RoutedEventArgs e) => OpenEditor(null);
    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void TestsList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (TestsList.SelectedItem is not TestListItem item)
        {
            MessageBox.Show(_s.Localization["Msg_SelectTest"]); return;
        }
        if (!_s.Auth.CanManage(item.Source))             // права: вчитель редагує лише свої тести
        {
            MessageBox.Show(_s.Localization["Err_Forbidden"]); return;
        }
        OpenEditor(_s.Tests.GetById(item.Source.Id));
    }

    private void OpenEditor(Test? existing)
    {
        var editor = new TestEditorWindow(_s, existing) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() == true) Reload();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var loc = _s.Localization;
        if (TestsList.SelectedItem is not TestListItem item)
        {
            MessageBox.Show(loc["Msg_SelectTest"]); return;
        }
        if (!_s.Auth.CanManage(item.Source))
        {
            MessageBox.Show(loc["Err_Forbidden"]); return;
        }
        if (MessageBox.Show(loc.Format("Msg_ConfirmDelete", item.Title), loc["Msg_ConfirmTitle"], MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        _s.Tests.Delete(item.Source.Id);
        Reload();
    }
}
