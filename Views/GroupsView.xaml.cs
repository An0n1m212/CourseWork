using System.Windows;
using System.Windows.Controls;
using TestingSystem.Abstractions;
using TestingSystem.Models;
using TestingSystem.Services;

namespace TestingSystem.Views;

/// <summary>Строка таблицы групп: название и число студентов. Id = 0 — служебный пункт «Все группы».</summary>
public sealed class GroupRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Students { get; init; }
}

/// <summary>Строка таблицы студентов выбранной группы.</summary>
public sealed class StudentRow
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
}

public partial class GroupsView : UserControl
{
    private readonly AppServices _s;

    public GroupsView(AppServices services)
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
        var loc = _s.Localization;
        int selectedId = (GroupsGrid.SelectedItem as GroupRow)?.Id ?? 0;
        int? targetId = (TargetGroupCombo.SelectedItem as Group)?.Id;

        var users = _s.Users.GetAll();
        var studentsByGroup = users
            .Where(u => u.GroupId.HasValue)
            .GroupBy(u => u.GroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var groups = _s.Groups.GetAll();
        var rows = new List<GroupRow>
        {
            new() { Id = 0, Name = loc["Filter_AllGroups"], Students = users.Count(u => u.Role == Role.Student) }
        };
        rows.AddRange(groups.Select(g => new GroupRow
        {
            Id = g.Id, Name = g.Name, Students = studentsByGroup.TryGetValue(g.Id, out int n) ? n : 0
        }));

        GroupsGrid.ItemsSource = rows;
        GroupsGrid.SelectedItem = rows.FirstOrDefault(r => r.Id == selectedId) ?? rows[0];

        TargetGroupCombo.ItemsSource = groups;
        TargetGroupCombo.SelectedItem = groups.FirstOrDefault(g => g.Id == targetId) ?? groups.FirstOrDefault();

        RefreshStudents();
        HideError();
    }

    private void GroupsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshStudents();
    private void Search_Changed(object sender, TextChangedEventArgs e) => RefreshStudents();

    private void RefreshStudents()
    {
        if (SearchBox is null || StudentsGrid is null) return;

        int groupId = (GroupsGrid.SelectedItem as GroupRow)?.Id ?? 0;
        string q = SearchBox.Text.Trim();
        var groupNames = _s.Groups.GetAll().ToDictionary(g => g.Id, g => g.Name);

        StudentsGrid.ItemsSource = _s.Users.GetStudents()
            .Where(u => groupId == 0 || u.GroupId == groupId)
            .Select(u => new StudentRow
            {
                Id = u.Id, FullName = u.FullName, Username = u.Username, Email = u.Email,
                GroupName = u.GroupId is int gid && groupNames.TryGetValue(gid, out var gn) ? gn : "—"
            })
            .Where(r => q.Length == 0
                     || r.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || r.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || r.Email.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var loc = _s.Localization;
        string name = NameBox.Text.Trim();

        if (name.Length == 0) { ShowError(loc["Err_GroupNameRequired"]); return; }
        if (_s.Groups.GetAll().Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            ShowError(loc["Err_GroupNameTaken"]);
            return;
        }

        _s.Groups.Save(new Group { Name = name });
        NameBox.Clear();
        Reload();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var loc = _s.Localization;
        if (GroupsGrid.SelectedItem is not GroupRow row || row.Id == 0) { ShowError(loc["Msg_SelectGroup"]); return; }

        if (_s.Users.GetAll().Any(u => u.GroupId == row.Id)) { ShowError(loc["Err_GroupHasStudents"]); return; }

        var answer = MessageBox.Show(
            loc.Format("Msg_ConfirmDeleteGroup", row.Name),
            loc["Msg_ConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        _s.Groups.Delete(row.Id);
        Reload();
    }

    /// <summary>Админ меняет группу выбранного студента.</summary>
    private void MoveToGroup_Click(object sender, RoutedEventArgs e)
    {
        var loc = _s.Localization;
        if (StudentsGrid.SelectedItem is not StudentRow student) { ShowError(loc["Msg_SelectStudent"]); return; }
        if (TargetGroupCombo.SelectedItem is not Group target) { ShowError(loc["Err_GroupRequired"]); return; }

        var result = _s.Auth.ChangeUserGroup(student.Id, target.Id);
        if (result != AuthResult.Success) { ShowError(loc.Describe(result)); return; }

        Reload();
        MessageBox.Show(loc.Format("Msg_GroupChanged", student.FullName, target.Name));
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;
}
