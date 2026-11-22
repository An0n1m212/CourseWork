using System.IO;
using TestingSystem.Models;

namespace TestingSystem.Services;

/// <summary>
/// Рушій оцінювання. Автоматично перевіряє всі типи, крім «ручних» (FileUpload і TextAnswer з
/// ManualTeacherReview): вони отримують IsGraded=false, а робота — статус PendingTeacherReview.
/// Не залежить ні від UI, ні від сховища — його легко покрити unit-тестами.
/// </summary>
public class GradingEngine
{
    private const double Eps = 1e-9;

    /// <summary>
    /// Нормалізація текстової відповіді: .Trim() прибирає пробіли з країв, нижній регістр робить порівняння
    /// нечутливим до регістру. Використовуємо ToLowerInvariant — це .ToLower() без залежності від
    /// регіональних налаштувань ПК (інакше, наприклад, у турецькій культурі "I" перетвориться на "ı").
    /// </summary>
    public static string Normalize(string? input) => (input ?? string.Empty).Trim().ToLowerInvariant();

    // ============================== оцінювання однієї відповіді ==============================

    public void GradeDetail(Question q, StudentAnswerDetail d)
    {
        // знімок питання (потрібен для розбору помилок і аналітики)
        d.QuestionId = q.Id;
        d.QuestionText = q.Text;
        d.QuestionType = q.QuestionType;
        d.MaxScore = q.MaxScore;
        d.Explanation = q.Explanation;
        d.CorrectSummary = DescribeCorrect(q);
        d.GivenSummary = DescribeGiven(q, d);

        switch (q.QuestionType)
        {
            case QuestionType.SingleChoice:
                Set(d, GradeSingle(q, d));
                break;
            case QuestionType.MultipleChoice:
                Set(d, GradeMultiple(q, d));
                break;
            case QuestionType.Matching:
                Set(d, GradeMatching(q, d));
                break;
            case QuestionType.Sequence:
                Set(d, GradeSequence(q, d));
                break;
            case QuestionType.TextAnswer when q.TextEvaluationType == TextEvaluationType.ManualTeacherReview:
                MarkManual(d, hasContent: !string.IsNullOrWhiteSpace(d.GivenTextAnswer));
                break;
            case QuestionType.TextAnswer:
                Set(d, GradeText(q, d));
                break;
            case QuestionType.FileUpload:
                MarkManual(d, hasContent: !string.IsNullOrWhiteSpace(d.UploadedFilePath));
                break;
        }

        void Set(StudentAnswerDetail x, double score) { x.ScoreAwarded = Math.Round(score, 2); x.IsGraded = true; }
    }

    /// <summary>Порожню відповідь на «ручне» питання перевіряти не потрібно (0 балів); непорожня йде вчителю.</summary>
    private static void MarkManual(StudentAnswerDetail d, bool hasContent)
    {
        d.ScoreAwarded = 0;
        d.IsGraded = !hasContent;
    }

    // SingleChoice (RadioButton): точний збіг — вибрано рівно один і він правильний
    private static double GradeSingle(Question q, StudentAnswerDetail d)
    {
        if (d.SelectedAnswerIds.Count != 1) return 0;
        var chosen = q.Answers.FirstOrDefault(a => a.Id == d.SelectedAnswerIds[0]);
        return chosen is { IsCorrect: true } ? q.MaxScore : 0;
    }

    // MultipleChoice (CheckBox): (правильно вибрані − неправильно вибрані) / усього правильних, не нижче 0.
    // Усі правильні і жодного зайвого = повний бал, частина правильних = частковий бал.
    private static double GradeMultiple(Question q, StudentAnswerDetail d)
    {
        var correct = q.Answers.Where(a => a.IsCorrect).Select(a => a.Id).ToHashSet();
        if (correct.Count == 0) return 0;

        var selected = d.SelectedAnswerIds.Distinct().ToList();
        int right = selected.Count(correct.Contains);
        int wrong = selected.Count - right;
        return q.MaxScore * Math.Max(0, (right - wrong) / (double)correct.Count);
    }

    // TextAnswer (AutoExactMatch / AutoContains): порівняння нормалізованих рядків
    private static double GradeText(Question q, StudentAnswerDetail d)
    {
        string given = Normalize(d.GivenTextAnswer);
        if (given.Length == 0) return 0;

        var accepted = q.Answers.Where(a => a.IsCorrect).Select(a => Normalize(a.Text)).Where(s => s.Length > 0).ToList();

        bool ok = q.TextEvaluationType == TextEvaluationType.AutoContains
            ? accepted.Any(a => given.Contains(a))     // відповідь МІСТИТЬ допустимий підрядок
            : accepted.Contains(given);                // відповідь ЗБІГАЄТЬСЯ з одним із допустимих
        return ok ? q.MaxScore : 0;
    }

    // Matching: по балу за кожну правильну пару
    private static double GradeMatching(Question q, StudentAnswerDetail d)
    {
        var left = q.Answers.OrderBy(a => a.OrderIndex).ToList();
        if (left.Count == 0) return 0;

        int right = 0;
        for (int i = 0; i < left.Count && i < d.SelectedAnswerIds.Count; i++)
            if (d.SelectedAnswerIds[i] == left[i].Id) right++;   // вибрано праву частину саме цього варіанта
        return q.MaxScore * right / left.Count;
    }

    // Sequence: повний бал лише за повністю правильний порядок
    private static double GradeSequence(Question q, StudentAnswerDetail d)
    {
        var expected = q.Answers.OrderBy(a => a.OrderIndex).Select(a => a.Id).ToList();
        return expected.Count > 0 && expected.SequenceEqual(d.SelectedAnswerIds) ? q.MaxScore : 0;
    }

    // ============================== результат спроби ==============================

    /// <summary>Будує результат: оцінює відповіді на всі питання тесту (без відповіді = 0 балів).</summary>
    public Result BuildResult(TestSession session, Test test, IReadOnlyDictionary<int, StudentAnswerDetail> given, bool timedOut)
    {
        var result = new Result
        {
            SessionId = session.Id, StudentId = session.StudentId, TestId = test.Id, Date = DateTime.Now
        };

        foreach (var q in test.Questions)
        {
            var d = given.TryGetValue(q.Id, out var g) ? g.Clone() : new StudentAnswerDetail { QuestionId = q.Id };
            GradeDetail(q, d);
            result.Details.Add(d);
        }

        Recalculate(result, test);

        bool pending = result.Details.Any(x => !x.IsGraded);
        result.Status = pending ? ResultStatus.PendingTeacherReview
                      : timedOut ? ResultStatus.Expired
                      : ResultStatus.Completed;
        result.IsPassed = !pending && result.Percentage >= test.PassScorePercent;
        return result;
    }

    /// <summary>Учитель виставляє бал за «ручну» відповідь (бал обмежується діапазоном 0..MaxScore).</summary>
    public void ApplyManualScore(StudentAnswerDetail d, double score, string? note)
    {
        d.ScoreAwarded = Math.Round(Math.Clamp(score, 0, d.MaxScore), 2);
        d.TeacherNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        d.IsGraded = true;
    }

    /// <summary>Завершення перевірки: PendingTeacherReview -> Completed, перерахунок підсумків і ознаки «склав».</summary>
    public void FinalizeReview(Result result, Test test, int teacherId)
    {
        Recalculate(result, test);
        result.Status = ResultStatus.Completed;
        result.IsPassed = result.Percentage >= test.PassScorePercent;
        result.ReviewedByTeacherId = teacherId;
    }

    private static void Recalculate(Result r, Test test)
    {
        r.TotalScore = Math.Round(r.Details.Sum(x => x.ScoreAwarded), 2);
        r.MaxScore = Math.Round(r.Details.Sum(x => x.MaxScore), 2);
        r.Percentage = r.MaxScore > Eps ? Math.Round(100 * r.TotalScore / r.MaxScore, 1) : 0;
    }

    // ============================== текстові описи ==============================

    private static IEnumerable<Answer> Ordered(Question q) => q.Answers.OrderBy(a => a.OrderIndex);

    private static string DescribeCorrect(Question q) => q.QuestionType switch
    {
        QuestionType.SingleChoice or QuestionType.MultipleChoice
            => string.Join("; ", q.Answers.Where(a => a.IsCorrect).Select(a => a.Text)),
        QuestionType.TextAnswer when q.TextEvaluationType == TextEvaluationType.ManualTeacherReview => string.Empty,
        QuestionType.TextAnswer => string.Join(" / ", q.Answers.Where(a => a.IsCorrect).Select(a => a.Text)),
        QuestionType.Matching => string.Join("; ", Ordered(q).Select(a => $"{a.Text} → {a.CorrectPairText}")),
        QuestionType.Sequence => string.Join(" → ", Ordered(q).Select(a => a.Text)),
        _ => string.Empty
    };

    private static string DescribeGiven(Question q, StudentAnswerDetail d)
    {
        string TextOf(int id) => q.Answers.FirstOrDefault(a => a.Id == id)?.Text ?? "?";

        return q.QuestionType switch
        {
            QuestionType.SingleChoice or QuestionType.MultipleChoice
                => string.Join("; ", d.SelectedAnswerIds.Select(TextOf)),
            QuestionType.TextAnswer => d.GivenTextAnswer,
            QuestionType.Matching => string.Join("; ", Ordered(q).Select((a, i) =>
                $"{a.Text} → {(i < d.SelectedAnswerIds.Count ? q.Answers.FirstOrDefault(x => x.Id == d.SelectedAnswerIds[i])?.CorrectPairText : null) ?? "—"}")),
            QuestionType.Sequence => string.Join(" → ", d.SelectedAnswerIds.Select(TextOf)),
            QuestionType.FileUpload => string.IsNullOrWhiteSpace(d.UploadedFilePath) ? string.Empty : Path.GetFileName(d.UploadedFilePath),
            _ => string.Empty
        };
    }
}

/// <summary>Чи містить відповідь вміст (використовується для статусу «відповідено» в навігації).</summary>
public static class AnswerState
{
    public static bool HasAnswer(StudentAnswerDetail? d)
        => d != null && (d.SelectedAnswerIds.Any(id => id > 0)
                         || !string.IsNullOrWhiteSpace(d.GivenTextAnswer)
                         || !string.IsNullOrWhiteSpace(d.UploadedFilePath));
}
