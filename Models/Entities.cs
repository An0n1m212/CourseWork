namespace TestingSystem.Models;

// Сутності — прості POCO без прив'язки до UI та конкретної БД. Зв'язки виражені Id-полями (як зовнішні ключі)
// і вкладеними колекціями (Test.Questions, Question.Answers, Result.Details) — для EF Core це
// стандартні навігаційні властивості.

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Group Clone() => (Group)MemberwiseClone();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Category Clone() => (Category)MemberwiseClone();
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    /// <summary>PBKDF2 + сіль. Пароль у відкритому вигляді не зберігається.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? GroupId { get; set; }
    public Role Role { get; set; }
    public string? AvatarPath { get; set; }
    public DateTime RegistrationDate { get; set; }

    public User Clone() => (User)MemberwiseClone();
}

public class Test
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AuthorTeacherId { get; set; }
    public int? CategoryId { get; set; }
    /// <summary>0 — без обмеження часу.</summary>
    public int TimeLimitMinutes { get; set; }
    public double PassScorePercent { get; set; } = 60;
    /// <summary>0 — без обмеження кількості спроб.</summary>
    public int MaxAttempts { get; set; }
    public bool IsRandomizeQuestions { get; set; }
    public bool IsRandomizeAnswers { get; set; }
    /// <summary>Пін-код доступу; порожньо — тест відкритий.</summary>
    public string? AccessCode { get; set; }
    /// <summary>Групи, яким призначено тест; порожньо — доступний усім учням.</summary>
    public List<int> AssignedGroupIds { get; set; } = new();
    /// <summary>Розширення: чи дозволено учню бачити правильні відповіді та пояснення після складання.</summary>
    public bool AllowShowExplanations { get; set; } = true;
    public List<Question> Questions { get; set; } = new();

    public Test DeepClone()
    {
        var c = (Test)MemberwiseClone();
        c.AssignedGroupIds = new List<int>(AssignedGroupIds);
        c.Questions = Questions.Select(q => q.DeepClone()).ToList();
        return c;
    }
}

public class Question
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public int? CategoryId { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public TextEvaluationType TextEvaluationType { get; set; } = TextEvaluationType.AutoExactMatch;
    public double MaxScore { get; set; } = 1;
    public string Explanation { get; set; } = string.Empty;
    public string? MediaPath { get; set; }
    public List<Answer> Answers { get; set; } = new();

    public Question DeepClone()
    {
        var c = (Question)MemberwiseClone();
        c.Answers = Answers.Select(a => a.Clone()).ToList();
        return c;
    }
}

/// <summary>
/// Універсальний варіант відповіді; сенс полів залежить від типу питання:
///  Single/Multiple — Text + IsCorrect;
///  TextAnswer     — кожний запис з IsCorrect=true — допустима відповідь;
///  Matching       — Text (лівий елемент) + CorrectPairText (правий), OrderIndex — порядок зліва;
///  Sequence       — Text + OrderIndex (правильна позиція).
/// </summary>
public class Answer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string CorrectPairText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsCorrect { get; set; }

    public Answer Clone() => (Answer)MemberwiseClone();
}

public class TestSession
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int TestId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public ResultStatus Status { get; set; } = ResultStatus.InProgress;
    /// <summary>Скільки разів учень втрачав фокус вікна / згортав його під час тесту.</summary>
    public int ViolationsCount { get; set; }

    public TestSession Clone() => (TestSession)MemberwiseClone();
}

public class Result
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public int TestId { get; set; }
    public double TotalScore { get; set; }
    public double MaxScore { get; set; }
    public double Percentage { get; set; }
    public bool IsPassed { get; set; }
    public ResultStatus Status { get; set; }
    public DateTime Date { get; set; }
    public string? TeacherComment { get; set; }
    /// <summary>Розширення: хто перевірив роботу вручну (для статистики вчителя).</summary>
    public int? ReviewedByTeacherId { get; set; }
    public List<StudentAnswerDetail> Details { get; set; } = new();

    public Result DeepClone()
    {
        var c = (Result)MemberwiseClone();
        c.Details = Details.Select(d => d.Clone()).ToList();
        return c;
    }
}

/// <summary>
/// Відповідь учня на одне питання. SelectedAnswerIds — упорядкований список:
///  Single/Multiple — вибрані варіанти;
///  Sequence        — Id у порядку, розставленому учнем;
///  Matching        — для кожного лівого елемента (за OrderIndex) Id варіанта, чию праву частину вибрано (0 — не вибрано).
/// Поля-знімки (QuestionText ... Explanation) фіксують питання на момент складання: розбір помилок працює,
/// навіть якщо вчитель пізніше змінить або видалить тест (у SQL — денормалізовані колонки).
/// </summary>
public class StudentAnswerDetail
{
    public int Id { get; set; }
    public int ResultId { get; set; }
    public int QuestionId { get; set; }
    public string GivenTextAnswer { get; set; } = string.Empty;
    public List<int> SelectedAnswerIds { get; set; } = new();
    public string? UploadedFilePath { get; set; }
    public double ScoreAwarded { get; set; }
    /// <summary>false — потрібна ручна перевірка вчителем.</summary>
    public bool IsGraded { get; set; }
    public string? TeacherNote { get; set; }

    // --- знімок питання ---
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public double MaxScore { get; set; }
    public string GivenSummary { get; set; } = string.Empty;
    public string CorrectSummary { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;

    public StudentAnswerDetail Clone()
    {
        var c = (StudentAnswerDetail)MemberwiseClone();
        c.SelectedAnswerIds = new List<int>(SelectedAnswerIds);
        return c;
    }
}
