using System.Windows.Threading;
using TestingSystem.Abstractions;
using TestingSystem.Models;

namespace TestingSystem.Services;

/// <summary>Питання в тому вигляді, в якому його бачить учень (порядок варіантів уже перемішано, якщо потрібно).</summary>
public sealed class RuntimeQuestion
{
    public Question Source { get; }
    /// <summary>Варіанти в порядку показу. Для Matching — ліві елементи за OrderIndex, праві перемішуються у View.</summary>
    public IReadOnlyList<Answer> Answers { get; }

    public RuntimeQuestion(Question source, IReadOnlyList<Answer> answers)
    {
        Source = source;
        Answers = answers;
    }
}

/// <summary>
/// Керує однією спробою тестування: таймер, відповіді, порушення та ПЕРЕХОДИ СТАНІВ сесії.
///
///   [створена] --Begin()--> InProgress
///   InProgress --Submit()--> Completed              (усі відповіді перевірено автоматично)
///   InProgress --Submit()--> PendingTeacherReview   (є відповіді, що потребують ручної перевірки)
///   InProgress --таймер сплив (Expire())--> Expired (сесія); результат: Expired або PendingTeacherReview
///   PendingTeacherReview --перевірка вчителем--> Completed (див. TestSessionService.CompleteReview)
///
/// З кінцевих станів назад повернутися не можна: після Complete() усі методи зміни стану ігноруються.
/// </summary>
public sealed class TestSessionController
{
    public const int WarningSeconds = 60;

    private readonly IResultRepository _results;
    private readonly GradingEngine _grading;
    private readonly Dictionary<int, StudentAnswerDetail> _answers = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _warned;

    public TestSession Session { get; }
    public Test Test { get; }
    public IReadOnlyList<RuntimeQuestion> Questions { get; }
    public DateTime? Deadline { get; private set; }
    public Result? Result { get; private set; }

    public bool HasTimeLimit => Test.TimeLimitMinutes > 0;
    public bool IsFinished => Session.Status != ResultStatus.InProgress;

    /// <summary>Залишок часу (null — без обмеження).</summary>
    public TimeSpan? Remaining
    {
        get
        {
            if (Deadline is null) return null;
            var left = Deadline.Value - DateTime.Now;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }

    public int AnsweredCount => _answers.Values.Count(AnswerState.HasAnswer);

    public event EventHandler? Tick;                       // щосекунди
    public event EventHandler? TimeWarning;                // залишилася остання хвилина (один раз)
    public event EventHandler? BeforeComplete;             // перед підрахунком: View зберігає поточну відповідь
    public event EventHandler<Result>? Finished;           // спробу завершено (надсилання або таймаут)

    public TestSessionController(TestSession session, Test test, IReadOnlyList<RuntimeQuestion> questions,
                                 IResultRepository results, GradingEngine grading)
    {
        Session = session;
        Test = test;
        Questions = questions;
        _results = results;
        _grading = grading;
    }

    /// <summary>Старт: фіксуємо час, рахуємо дедлайн і запускаємо таймер.</summary>
    public void Begin()
    {
        Session.StartTime = DateTime.Now;
        Session.Status = ResultStatus.InProgress;
        Deadline = HasTimeLimit ? Session.StartTime.AddMinutes(Test.TimeLimitMinutes) : null;
        _results.SaveSession(Session);

        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (IsFinished) return;

        Tick?.Invoke(this, EventArgs.Empty);

        if (Remaining is { } left)
        {
            if (!_warned && left.TotalSeconds <= WarningSeconds && left.TotalSeconds > 0)
            {
                _warned = true;
                TimeWarning?.Invoke(this, EventArgs.Empty);
            }
            if (left <= TimeSpan.Zero) Expire();           // час вийшов -> автоматичне надсилання
        }
    }

    public void SaveAnswer(StudentAnswerDetail detail)
    {
        if (IsFinished) return;
        _answers[detail.QuestionId] = detail.Clone();
    }

    public StudentAnswerDetail? GetAnswer(int questionId) => _answers.TryGetValue(questionId, out var d) ? d.Clone() : null;

    public bool IsAnswered(int questionId) => AnswerState.HasAnswer(GetAnswer(questionId));

    public void SetCurrentIndex(int index)
    {
        if (IsFinished) return;
        Session.CurrentQuestionIndex = index;
        _results.SaveSession(Session);
    }

    public void RegisterViolation()
    {
        if (IsFinished) return;
        Session.ViolationsCount++;
        _results.SaveSession(Session);
    }

    public Result Submit() => Complete(timedOut: false);
    public Result Expire() => Complete(timedOut: true);

    private Result Complete(bool timedOut)
    {
        if (IsFinished) return Result!;

        BeforeComplete?.Invoke(this, EventArgs.Empty);

        _timer.Stop();
        _timer.Tick -= OnTimerTick;

        Session.EndTime = DateTime.Now;
        var result = _grading.BuildResult(Session, Test, _answers, timedOut);

        // Сесія при таймауті залишається Expired навіть якщо частина відповідей чекає перевірки вчителем
        Session.Status = timedOut ? ResultStatus.Expired : result.Status;
        _results.SaveSession(Session);

        Result = _results.SaveResult(result);
        Finished?.Invoke(this, Result);
        return Result;
    }
}
