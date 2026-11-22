using System.Collections.ObjectModel;
using TestingSystem.Abstractions;
using TestingSystem.Infrastructure.Mvvm;
using TestingSystem.Models;

namespace TestingSystem.ViewModels;

/// <summary>Фільтр лекцій викладача за станом публікації.</summary>
public enum LectureStatusFilter { All, Published, Draft }

/// <summary>Вкладка викладача «Лекції»: список власних лекцій, пошук і фільтри, створення, редагування, видалення.</summary>
public sealed class LecturesViewModel : ViewModelBase, IViewLifecycle
{
    private readonly ILectureService _lectures;
    private readonly ICategoryRepository _categories;
    private readonly IGroupRepository _groups;
    private readonly ILocalizationService _loc;
    private readonly IDialogService _dialogs;
    private readonly IViewModelFactory _factory;

    private readonly List<Lecture> _all = new();
    private LectureItemViewModel? _selectedItem;
    private string _searchText = string.Empty;
    private Option<int?>? _selectedCategory;
    private Option<LectureStatusFilter>? _selectedStatus;

    public ObservableCollection<LectureItemViewModel> Items { get; } = new();
    public ObservableCollection<Option<int?>> CategoryOptions { get; } = new();
    public ObservableCollection<Option<LectureStatusFilter>> StatusOptions { get; } = new();

    public LectureItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
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

    public Option<LectureStatusFilter>? SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value)) ApplyFilter();
        }
    }

    public bool IsEmpty => Items.Count == 0;
    public string EmptyMessage => _all.Count == 0 ? _loc["Msg_NoLecturesYet"] : _loc["Filter_NoMatches"];

    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }

    public LecturesViewModel(ILectureService lectures, ICategoryRepository categories, IGroupRepository groups,
                             ILocalizationService loc, IDialogService dialogs, IViewModelFactory factory)
    {
        _lectures = lectures;
        _categories = categories;
        _groups = groups;
        _loc = loc;
        _dialogs = dialogs;
        _factory = factory;

        NewCommand = new RelayCommand(() => OpenEditor(new Lecture()));
        EditCommand = new RelayCommand(Edit, () => SelectedItem != null);
        DeleteCommand = new RelayCommand(Delete, () => SelectedItem != null);
        ResetFiltersCommand = new RelayCommand(ResetFilters);
    }

    public void OnShown()
    {
        _loc.LanguageChanged += OnLanguageChanged;
        Reload();
    }

    public void OnHidden() => _loc.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Reload();

    public void Reload()
    {
        int? selectedId = SelectedItem?.Source.Id;

        _all.Clear();
        _all.AddRange(_lectures.GetManageable());

        RefreshFilterOptions();
        ApplyFilter(selectedId);
    }

    private void RefreshFilterOptions()
    {
        int? categoryId = SelectedCategory?.Value;
        LectureStatusFilter status = SelectedStatus?.Value ?? LectureStatusFilter.All;

        CategoryOptions.Clear();
        CategoryOptions.Add(new Option<int?>(null, _loc["Filter_AllCategories"]));
        foreach (var category in _categories.GetAll())
            CategoryOptions.Add(new Option<int?>(category.Id, category.Name));

        StatusOptions.Clear();
        StatusOptions.Add(new Option<LectureStatusFilter>(LectureStatusFilter.All, _loc["Filter_AllStatuses"]));
        StatusOptions.Add(new Option<LectureStatusFilter>(LectureStatusFilter.Published, _loc["Lect_Published"]));
        StatusOptions.Add(new Option<LectureStatusFilter>(LectureStatusFilter.Draft, _loc["Lect_Draft"]));

        _selectedCategory = CategoryOptions.FirstOrDefault(o => o.Value == categoryId) ?? CategoryOptions[0];
        _selectedStatus = StatusOptions.FirstOrDefault(o => o.Value == status) ?? StatusOptions[0];
        OnPropertyChanged(nameof(SelectedCategory));
        OnPropertyChanged(nameof(SelectedStatus));
    }

    private void ApplyFilter(int? selectedId = null)
    {
        selectedId ??= SelectedItem?.Source.Id;

        string query = SearchText.Trim();
        int? categoryId = SelectedCategory?.Value;
        var status = SelectedStatus?.Value ?? LectureStatusFilter.All;

        var filtered = _all
            .Where(l => categoryId is null || l.CategoryId == categoryId)
            .Where(l => status switch
            {
                LectureStatusFilter.Published => l.IsPublished,
                LectureStatusFilter.Draft => !l.IsPublished,
                _ => true
            })
            .Where(l => query.Length == 0 || Matches(l, query))
            .ToList();

        var categories = _categories.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var groups = _groups.GetAll().ToDictionary(g => g.Id, g => g.Name);

        Items.Clear();
        foreach (var lecture in filtered)
            Items.Add(new LectureItemViewModel(lecture, BuildMeta(lecture, categories, groups)));

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
        SelectedStatus = StatusOptions.FirstOrDefault();
        ApplyFilter();
    }

    private string BuildMeta(Lecture l, Dictionary<int, string> categories, Dictionary<int, string> groups)
    {
        var parts = new List<string> { _loc[l.IsPublished ? "Lect_Published" : "Lect_Draft"] };
        if (l.CategoryId is int c && categories.TryGetValue(c, out var categoryName)) parts.Add(categoryName);
        parts.Add(l.AssignedGroupIds.Count == 0
            ? _loc["Meta_AllGroups"]
            : string.Join(", ", l.AssignedGroupIds.Select(id => groups.TryGetValue(id, out var gn) ? gn : "?")));
        parts.Add(l.UpdatedAt.ToString("dd.MM.yyyy HH:mm"));
        if (l.IsAiGenerated) parts.Add("✨ AI");
        return string.Join("  •  ", parts);
    }

    private void Edit()
    {
        if (SelectedItem is null) return;
        var lecture = _lectures.GetById(SelectedItem.Source.Id);
        if (lecture is null) { _dialogs.ShowMessage(_loc["Err_Forbidden"]); return; }
        OpenEditor(lecture);
    }

    private void OpenEditor(Lecture lecture)
    {
        var editor = _factory.Create<LectureEditorViewModel>(lecture);
        if (_dialogs.ShowDialog(editor) == true) Reload();
    }

    private void Delete()
    {
        if (SelectedItem is null) return;
        if (!_dialogs.Confirm(_loc.Format("Lect_ConfirmDelete", SelectedItem.Title))) return;

        try { _lectures.Delete(SelectedItem.Source.Id); }
        catch (UnauthorizedAccessException) { _dialogs.ShowMessage(_loc["Err_Forbidden"]); return; }
        Reload();
    }
}
