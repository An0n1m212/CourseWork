using System.Collections.ObjectModel;
using TestingSystem.Abstractions;
using TestingSystem.Infrastructure.Mvvm;
using TestingSystem.Models;

namespace TestingSystem.ViewModels;

/// <summary>Вкладка студента «Лекції»: опубліковані лекції його групи, пошук, фільтр за категоріями та читання вибраної.</summary>
public sealed class StudentLecturesViewModel : ViewModelBase, IViewLifecycle
{
    private readonly ILectureService _lectures;
    private readonly IUserRepository _users;
    private readonly ICategoryRepository _categories;
    private readonly ITestRepository _tests;
    private readonly ILocalizationService _loc;

    private readonly List<Lecture> _all = new();
    private LectureItemViewModel? _selectedItem;
    private string _searchText = string.Empty;
    private Option<int?>? _selectedCategory;

    public ObservableCollection<LectureItemViewModel> Items { get; } = new();
    public ObservableCollection<Option<int?>> CategoryOptions { get; } = new();

    public LectureItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value)) return;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedInfo));
            OnPropertyChanged(nameof(SelectedContent));
            OnPropertyChanged(nameof(RelatedTestText));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) ApplyFilter();
        }
    }

    public Option<int?>? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value)) ApplyFilter();
        }
    }

    public bool IsEmpty => Items.Count == 0;
    public string EmptyMessage => _all.Count == 0 ? _loc["Lect_Empty"] : _loc["Filter_NoMatches"];
    public bool HasSelection => _selectedItem != null;
    public string SelectedTitle => _selectedItem?.Title ?? string.Empty;
    public string SelectedContent => _selectedItem?.Source.Content ?? string.Empty;

    public string SelectedInfo
    {
        get
        {
            if (_selectedItem is null) return string.Empty;
            var lecture = _selectedItem.Source;
            var parts = new List<string>();

            string? author = _users.GetById(lecture.AuthorTeacherId)?.FullName;
            if (!string.IsNullOrEmpty(author)) parts.Add(_loc.Format("Lect_By", author));
            if (lecture.CategoryId is int c && _categories.GetById(c)?.Name is { } category) parts.Add(category);
            parts.Add(lecture.UpdatedAt.ToString("dd.MM.yyyy"));
            return string.Join("  •  ", parts);
        }
    }

    public string RelatedTestText
        => _selectedItem?.Source.RelatedTestId is int id && _tests.GetById(id) is { } test
            ? _loc.Format("Lect_RelatedTest", test.Title)
            : string.Empty;

    public RelayCommand ResetFiltersCommand { get; }

    public StudentLecturesViewModel(ILectureService lectures, IUserRepository users, ICategoryRepository categories,
                                    ITestRepository tests, ILocalizationService loc)
    {
        _lectures = lectures;
        _users = users;
        _categories = categories;
        _tests = tests;
        _loc = loc;

        ResetFiltersCommand = new RelayCommand(ResetFilters);
    }

    public void OnShown()
    {
        _loc.LanguageChanged += OnLanguageChanged;
        Reload();
    }

    public void OnHidden() => _loc.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Reload();

    private void Reload()
    {
        int? selectedId = SelectedItem?.Source.Id;

        _all.Clear();
        _all.AddRange(_lectures.GetForCurrentStudent());

        RefreshCategoryOptions();
        ApplyFilter(selectedId);
    }

    private void RefreshCategoryOptions()
    {
        int? categoryId = SelectedCategory?.Value;

        CategoryOptions.Clear();
        CategoryOptions.Add(new Option<int?>(null, _loc["Filter_AllCategories"]));
        foreach (var category in _categories.GetAll())
            CategoryOptions.Add(new Option<int?>(category.Id, category.Name));

        _selectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == categoryId) ?? CategoryOptions[0];
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private void ApplyFilter(int? selectedId = null)
    {
        selectedId ??= SelectedItem?.Source.Id;

        string query = SearchText.Trim();
        int? categoryId = SelectedCategory?.Value;

        var filtered = _all
            .Where(l => categoryId is null || l.CategoryId == categoryId)
            .Where(l => query.Length == 0 || Matches(l, query))
            .ToList();

        Items.Clear();
        foreach (var lecture in filtered)
            Items.Add(new LectureItemViewModel(lecture, lecture.UpdatedAt.ToString("dd.MM.yyyy")));

        SelectedItem = Items.FirstOrDefault(i => i.Source.Id == selectedId) ?? Items.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private static bool Matches(Lecture lecture, string query)
        => lecture.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
           || lecture.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
           || lecture.Content.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void ResetFilters()
    {
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        SelectedCategory = CategoryOptions.FirstOrDefault();
        ApplyFilter();
    }
}
