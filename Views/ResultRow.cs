using TestingSystem.Models;

namespace TestingSystem.Views;

/// <summary>Рядок таблиці результатів (пласка проєкція для DataGrid).</summary>
public record ResultRow(
    Result Source, string Student, string Group, string Test, string Points, double Percent,
    bool Passed, string PassedText, ResultStatus Status, string StatusText, DateTime Date, int Violations)
{
    public ResultExportRow ToExport()
        => new(Student, Group, Test, Source.TotalScore, Source.MaxScore, Percent, Passed, Status, Date, Violations);
}

/// <summary>Збирає ResultRow з Result, підтягуючи імена учнів, груп і тестів.</summary>
public class ResultRowFactory
{
    private readonly AppServices _s;
    public ResultRowFactory(AppServices services) => _s = services;

    public IReadOnlyList<ResultRow> Build(IEnumerable<Result> results)
    {
        var loc = _s.Localization;
        var users = _s.Users.GetAll().ToDictionary(u => u.Id);
        var groups = _s.Groups.GetAll().ToDictionary(g => g.Id, g => g.Name);
        var titles = _s.Tests.GetAll().ToDictionary(t => t.Id, t => t.Title);

        return results.Select(r =>
        {
            users.TryGetValue(r.StudentId, out var student);
            string group = student?.GroupId is int gid && groups.TryGetValue(gid, out var gn) ? gn : "—";
            bool pending = r.Status == ResultStatus.PendingTeacherReview;
            return new ResultRow(
                r,
                student?.FullName ?? loc["Unknown_Student"],
                group,
                titles.TryGetValue(r.TestId, out var title) ? title : loc["Deleted_Test"],
                $"{r.TotalScore:0.##}/{r.MaxScore:0.##}",
                r.Percentage, r.IsPassed,
                pending ? "…" : r.IsPassed ? "✓" : "✗",
                r.Status, loc[$"Status_{r.Status}"], r.Date,
                _s.Results.GetSession(r.SessionId)?.ViolationsCount ?? 0);
        }).OrderByDescending(x => x.Date).ToList();
    }
}
