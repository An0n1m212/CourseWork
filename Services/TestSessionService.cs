using TestingSystem.Abstractions;
using TestingSystem.Models;

namespace TestingSystem.Services;

public enum StartError { None, NotAssigned, NoAttemptsLeft, WrongAccessCode, NoQuestions, TestNotFound }

/// <summary>
/// Бізнес-логіка запуску та перевірки тестів: доступ за групами, ліміт спроб, пін-код,
/// підготовка питань (перемішування) і завершення ручної перевірки.
/// Фабрика TestSessionController — UI не створює сесії сам.
/// </summary>
public class TestSessionService
{
    private readonly ITestRepository _tests;
    private readonly IResultRepository _results;
    private readonly GradingEngine _grading;
    private readonly Random _rng = new();

    public TestSessionService(ITestRepository tests, IResultRepository results, GradingEngine grading)
    {
        _tests = tests;
        _results = results;
        _grading = grading;
    }

    public bool IsAvailableFor(Test test, User student)
        => test.Questions.Count > 0
           && (test.AssignedGroupIds.Count == 0 || (student.GroupId is int g && test.AssignedGroupIds.Contains(g)));

    public IReadOnlyList<Test> GetAvailableTests(User student)
        => _tests.GetAll().Where(t => IsAvailableFor(t, student)).ToList();

    public int GetAttemptsUsed(int studentId, int testId) => _results.GetSessions(studentId, testId).Count;

    public (StartError Error, TestSessionController? Controller) TryStart(User student, int testId, string? accessCode)
    {
        var test = _tests.GetById(testId);
        if (test is null) return (StartError.TestNotFound, null);
        if (test.Questions.Count == 0) return (StartError.NoQuestions, null);
        if (!IsAvailableFor(test, student)) return (StartError.NotAssigned, null);

        int used = GetAttemptsUsed(student.Id, test.Id);
        if (test.MaxAttempts > 0 && used >= test.MaxAttempts) return (StartError.NoAttemptsLeft, null);

        if (!string.IsNullOrWhiteSpace(test.AccessCode) && test.AccessCode.Trim() != (accessCode ?? string.Empty).Trim())
            return (StartError.WrongAccessCode, null);

        // Незавершені «закинуті» спроби закриваємо як Expired (вони вже враховані в ліміті спроб)
        foreach (var old in _results.GetSessions(student.Id, test.Id).Where(s => s.Status == ResultStatus.InProgress))
        {
            old.Status = ResultStatus.Expired;
            old.EndTime = DateTime.Now;
            _results.SaveSession(old);
        }

        var session = _results.SaveSession(new TestSession
        {
            StudentId = student.Id, TestId = test.Id, StartTime = DateTime.Now, Status = ResultStatus.InProgress
        });

        return (StartError.None, new TestSessionController(session, test, Prepare(test), _results, _grading));
    }

    /// <summary>Порядок питань і варіантів: перемішування за прапорцями тесту; Sequence перемішується завжди.</summary>
    private List<RuntimeQuestion> Prepare(Test test)
    {
        var questions = test.IsRandomizeQuestions ? Shuffle(test.Questions) : test.Questions.ToList();

        return questions.Select(q =>
        {
            IReadOnlyList<Answer> answers = q.QuestionType switch
            {
                QuestionType.Sequence => ShuffleDifferent(q.Answers),
                QuestionType.Matching => q.Answers.OrderBy(a => a.OrderIndex).ToList(),   // зліва — у заданому порядку
                QuestionType.SingleChoice or QuestionType.MultipleChoice when test.IsRandomizeAnswers => Shuffle(q.Answers),
                _ => q.Answers.OrderBy(a => a.OrderIndex).ToList()
            };
            return new RuntimeQuestion(q, answers);
        }).ToList();
    }

    private List<T> Shuffle<T>(IEnumerable<T> source)
    {
        var list = source.ToList();
        for (int i = list.Count - 1; i > 0; i--)     // Fisher–Yates
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    /// <summary>Перемішує так, щоб початковий (правильний) порядок не збігся з показаним.</summary>
    private List<Answer> ShuffleDifferent(List<Answer> answers)
    {
        var correct = answers.OrderBy(a => a.OrderIndex).Select(a => a.Id).ToList();
        List<Answer> shuffled = Shuffle(answers);
        for (int attempt = 0; attempt < 8 && answers.Count > 1 && shuffled.Select(a => a.Id).SequenceEqual(correct); attempt++)
            shuffled = Shuffle(answers);
        return shuffled;
    }

    /// <summary>
    /// Завершення ручної перевірки: бали вже виставлено в Details (GradingEngine.ApplyManualScore).
    /// Результат PendingTeacherReview -> Completed; пов'язана сесія теж закривається.
    /// </summary>
    public void CompleteReview(Result result, int teacherId, string? comment)
    {
        var test = _tests.GetById(result.TestId);
        if (test is null) throw new InvalidOperationException("Test no longer exists.");

        _grading.FinalizeReview(result, test, teacherId);
        result.TeacherComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        _results.SaveResult(result);

        var session = _results.GetSession(result.SessionId);
        if (session is { Status: ResultStatus.PendingTeacherReview })
        {
            session.Status = ResultStatus.Completed;
            _results.SaveSession(session);
        }
    }
}
