using System.Windows;
using System.Windows.Controls;
using TestingSystem.Models;

namespace TestingSystem.Views;

/// <summary>Рядок таблиці категорій: назва та кількість тестів/лекцій, де вона використовується.</summary>
public sealed class CategoryRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Usage { get; init; }
}

/// <summary>Керування категоріями тестів і лекцій (доступно лише адміністратору).</summary>
public partial class CategoriesView : UserControl
{
    private readonly AppServices _s;

    public CategoriesView(AppServices services)
    {
        InitializeComponent();
        _s = services;

        if (!_s.Auth.IsInRole(Role.Admin))
            throw new InvalidOperationException("Access denied: Admin role required.");

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
        int selectedId = (CategoriesGrid.SelectedItem as CategoryRow)?.Id ?? 0;

        var tests = _s.Tests.GetAll();
        var lectures = _s.Lectures.GetAll();
        var rows = _s.Categories.GetAll().Select(c => new CategoryRow
        {
            Id = c.Id,
            Name = c.Name,
            Usage = tests.Count(t => t.CategoryId == c.Id)
                    + lectures.Count(l => l.CategoryId == c.Id)
                    + tests.Sum(t => t.Questions.Count(q => q.CategoryId == c.Id))
        }).ToList();

        CategoriesGrid.ItemsSource = rows;
        CategoriesGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == selectedId);
        HideError();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var loc = _s.Localization;
        string name = NameBox.Text.Trim();

        if (name.Length == 0) { ShowError(loc["Err_CategoryNameRequired"]); return; }
        if (_s.Categories.GetAll().Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError(loc["Err_CategoryNameTaken"]);
            return;
        }

        _s.Categories.Save(new Category { Name = name });
        NameBox.Clear();
        Reload();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var loc = _s.Localization;
        if (CategoriesGrid.SelectedItem is not CategoryRow row)
        {
            ShowError(loc["Msg_SelectCategory"]);
            return;
        }

        bool inUse = _s.Tests.GetAll().Any(t => t.CategoryId == row.Id || t.Questions.Any(q => q.CategoryId == row.Id))
                     || _s.Lectures.GetAll().Any(l => l.CategoryId == row.Id);
        if (inUse)
        {
            ShowError(loc["Err_CategoryInUse"]);
            return;
        }

        var answer = MessageBox.Show(
            loc.Format("Msg_ConfirmDeleteCategory", row.Name),
            loc["Msg_ConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        _s.Categories.Delete(row.Id);
        Reload();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;
}
