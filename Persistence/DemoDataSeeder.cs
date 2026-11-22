using System.IO;
using TestingSystem.Abstractions;
using TestingSystem.Models;
using TestingSystem.Services;

namespace TestingSystem.Persistence;

/// <summary>
/// Демо-дані. Працює ЛИШЕ через інтерфейси, тому підходить і для первинного наповнення
/// порожньої SQLite/PostgreSQL бази (виклик можна залишити, але захистити перевіркою "якщо база порожня").
/// </summary>
public static class DemoDataSeeder
{
    public static void Seed(IUserRepository users, IGroupRepository groups, ICategoryRepository categories,
                            ITestRepository tests, IResultRepository results, ILectureRepository lectures, GradingEngine grading)
    {
        // ---------- групи та категорії ----------
        var kn = groups.Save(new Group { Name = "КН-25-1" });
        groups.Save(new Group { Name = "ІБ-24-2" });
        var catProg = categories.Save(new Category { Name = "Програмування" });
        var catDb = categories.Save(new Category { Name = "Бази даних" });
        categories.Save(new Category { Name = "Загальні знання" });

        // ---------- користувачі (пароль у всіх: 1234) ----------
        users.Create(NewUser("admin",    "Адміністратор системи", "admin@uni.edu",   null,  Role.Admin,   300));
        var teacher = users.Create(NewUser("teacher",  "Іванов І.І.",     "ivanov@uni.edu",  null,  Role.Teacher, 200));
        var s1      = users.Create(NewUser("student1", "Петров Петро",     "petrov@uni.edu",  kn.Id, Role.Student, 60));
        var s2      = users.Create(NewUser("student2", "Сидоров Олексій", "sidorov@uni.edu", kn.Id, Role.Student, 45));

        // ---------- тест 1: 10 хвилин, пін-код 1234, УСІ 6 типів питань ----------
        var full = tests.Save(new Test
        {
            Title = "Комплексний тест: патерни та C#",
            Description = "Демонструє всі типи питань. Пін-код доступу: 1234",
            AuthorTeacherId = teacher.Id, CategoryId = catProg.Id,
            TimeLimitMinutes = 10, PassScorePercent = 60, MaxAttempts = 3,
            IsRandomizeQuestions = false, IsRandomizeAnswers = true,
            AccessCode = "1234", AssignedGroupIds = { kn.Id }, AllowShowExplanations = true,
            Questions =
            {
                Single(catProg.Id, "Яке ключове слово оголошує константу часу компіляції в C#?", 1,
                    "const обчислюється на етапі компіляції, readonly — під час створення об'єкта.",
                    ("const", true), ("readonly", false), ("static", false)),

                Multiple(catProg.Id, "Які з перелічених типів є посилальними?", 2,
                    "string і object — посилальні типи; int і double — значущі.",
                    ("string", true), ("int", false), ("object", true), ("double", false)),

                TextQ(catProg.Id, "Як називається метод — точка входу консольної програми C#? (одне слово)", 1,
                    TextEvaluationType.AutoExactMatch, "Метод називається Main.", "main"),

                TextQ(catProg.Id, "Назвіть будь-який патерн проєктування з книги «банди чотирьох» (GoF).", 1,
                    TextEvaluationType.AutoContains, "Підходять Singleton, Factory, Observer, Strategy, Adapter тощо.",
                    "singleton", "factory", "observer", "strategy", "adapter", "decorator"),

                TextQ(catProg.Id, "Своїми словами поясніть, навіщо потрібен патерн «Репозиторій».", 3,
                    TextEvaluationType.ManualTeacherReview, "Репозиторій ізолює логіку доступу до даних від бізнес-логіки та UI."),

                Matching(catProg.Id, "Зіставте термін та означення.", 3, "Це три базові поняття архітектури.",
                    ("Repository", "Абстракція доступу до даних"),
                    ("Dependency Injection", "Передавання залежностей ззовні"),
                    ("Strategy", "Взаємозамінні алгоритми")),

                Sequence(catDb.Id, "Розставте кроки роботи з базою даних у правильному порядку.", 2,
                    "З'єднання відкривають, створюють команду, виконують її та закривають з'єднання.",
                    "Відкрити з'єднання", "Створити команду", "Виконати запит", "Закрити з'єднання"),

                FileQ(catProg.Id, "Завантажте файл із розв'язком лабораторної роботи (до 10 МБ).", 2,
                    "Допустимі документи, архіви та вихідний код.")
            }
        });

        // ---------- тест 2: короткий, без коду й часу, для всіх груп ----------
        var db = tests.Save(new Test
        {
            Title = "Основи баз даних",
            Description = "Три простих питання без обмеження часу",
            AuthorTeacherId = teacher.Id, CategoryId = catDb.Id,
            TimeLimitMinutes = 0, PassScorePercent = 60, MaxAttempts = 0,
            IsRandomizeAnswers = false, AllowShowExplanations = true,
            Questions =
            {
                Single(catDb.Id, "Яка команда SQL читає дані з таблиці?", 1, "SELECT вибирає рядки.",
                    ("SELECT", true), ("INSERT", false), ("DELETE", false)),
                Single(catDb.Id, "Що означає абревіатура PK?", 1, "Primary Key — первинний ключ.",
                    ("Primary Key", true), ("Public Key", false), ("Private Key", false)),
                Single(catDb.Id, "Який оператор об'єднує таблиці за умовою?", 1, "JOIN з'єднує рядки таблиць.",
                    ("JOIN", true), ("GROUP", false), ("ORDER", false))
            }
        });

        // ---------- результати ----------
        // 1) Робота учня, що очікує перевірки (є ручні питання) — для демонстрації екрана перевірки
        string? demoFile = CreateDemoUpload();
        var q = full.Questions;
        Submit(results, grading, s1, full, DateTime.Now.AddHours(-3),
            Pick(q[0], 0), PickMany(q[1], 0, 2), Typed(q[2], "  MAIN "), Typed(q[3], "Я знаю патерн Singleton"),
            Typed(q[4], "Репозиторій приховує деталі зберігання даних: UI та бізнес-логіка працюють з інтерфейсом, а реалізацію (пам'ять, SQLite) можна замінити."),
            Matched(q[5], 0, 1, 1),            // третя пара обрана неправильно -> частковий бал 2 з 3
            Ordered(q[6], 0, 1, 2, 3), UploadedFile(q[7], demoFile));

        // 2) Перевірені спроби з тесту 2: динаміка успішності та сертифікати
        Submit(results, grading, s1, db, DateTime.Now.AddDays(-3), Pick(db.Questions[0], 0), Pick(db.Questions[1], 1), Pick(db.Questions[2], 2)); // 33%
        Submit(results, grading, s1, db, DateTime.Now.AddDays(-1), Pick(db.Questions[0], 0), Pick(db.Questions[1], 0), Pick(db.Questions[2], 2)); // 67%
        Submit(results, grading, s2, db, DateTime.Now.AddDays(-2), Pick(db.Questions[0], 0), Pick(db.Questions[1], 0), Pick(db.Questions[2], 0)); // 100%

        // ---------- лекції ----------
        // 1) для групи КН-25-1, пов'язана з комплексним тестом
        lectures.Save(new Lecture
        {
            Title = "Патерн «Репозиторій»", Summary = "Як відокремити доступ до даних від бізнес-логіки та інтерфейсу.",
            Content = RepositoryLecture, AuthorTeacherId = teacher.Id, CategoryId = catProg.Id,
            AssignedGroupIds = { kn.Id }, IsPublished = true, RelatedTestId = full.Id,
            CreatedAt = DateTime.Now.AddDays(-10), UpdatedAt = DateTime.Now.AddDays(-2)
        });

        // 2) для всіх груп
        lectures.Save(new Lecture
        {
            Title = "Основи SQL: SELECT і JOIN", Summary = "Вибірка даних і об'єднання таблиць.",
            Content = SqlLecture, AuthorTeacherId = teacher.Id, CategoryId = catDb.Id,
            IsPublished = true, RelatedTestId = db.Id,
            CreatedAt = DateTime.Now.AddDays(-6), UpdatedAt = DateTime.Now.AddDays(-1)
        });

        // 3) чернетка: студентам не видно
        lectures.Save(new Lecture
        {
            Title = "Чернетка: Dependency Injection", Summary = "Заготовка лекції — ще не опублікована.",
            Content = "# Dependency Injection\n\nЗаготовка лекції. Опублікуйте її, коли текст буде готовий.",
            AuthorTeacherId = teacher.Id, CategoryId = catProg.Id, IsPublished = false,
            CreatedAt = DateTime.Now.AddHours(-5), UpdatedAt = DateTime.Now.AddHours(-5)
        });
    }


    private const string RepositoryLecture = """
        # Патерн «Репозиторій»

        ## Навіщо він потрібен
        Репозиторій **приховує спосіб зберігання даних** за інтерфейсом. Інтерфейс і бізнес-логіка працюють з абстракцією,
        а де лежать дані — у пам'яті, SQLite чи PostgreSQL — вирішує конкретна реалізація.

        ## Основні ідеї
        - Інтерфейс описує операції: отримати, зберегти, видалити.
        - Реалізація знає про сховище: список у пам'яті, SQL-запити, EF Core.
        - Заміна сховища не вимагає переписувати інтерфейс.

        ## Приклад
        ```
        interface ITestRepository
        {
            Test? GetById(int id);
            Test Save(Test test);
            void Delete(int id);
        }
        ```

        ## Підсумки
        1. Репозиторій відокремлює доступ до даних від логіки.
        2. Разом з Dependency Injection дає просту заміну сховища.

        ## Питання для самоперевірки
        1. Яку проблему розв'язує репозиторій?
        2. Чим InMemory-реалізація відрізняється від SQL-реалізації для коду, що її викликає?
        """;

    private const string SqlLecture = """
        # Основи SQL: SELECT і JOIN

        ## SELECT
        Команда **SELECT** вибирає рядки з таблиці:

        ```
        SELECT Name, Age FROM Students WHERE Age > 18;
        ```

        ## JOIN
        **JOIN** об'єднує рядки двох таблиць за умовою:

        ```
        SELECT s.Name, g.Title
        FROM Students s
        JOIN Groups g ON g.Id = s.GroupId;
        ```

        ## Запам'ятайте
        - `INNER JOIN` повертає лише рядки, що збіглися.
        - `LEFT JOIN` зберігає всі рядки лівої таблиці.
        - Умова об'єднання задається після `ON`.
        """;

    // ----------------------- допоміжні методи -----------------------

    private static User NewUser(string login, string fullName, string email, int? groupId, Role role, int daysAgo) => new()
    {
        Username = login, FullName = fullName, Email = email, GroupId = groupId, Role = role,
        PasswordHash = PasswordHasher.Hash("1234"), RegistrationDate = DateTime.Now.AddDays(-daysAgo)
    };

    private static void Submit(IResultRepository results, GradingEngine grading, User student, Test test, DateTime when,
                               params StudentAnswerDetail[] given)
    {
        var session = results.SaveSession(new TestSession
        {
            StudentId = student.Id, TestId = test.Id, StartTime = when.AddMinutes(-7), EndTime = when,
            CurrentQuestionIndex = test.Questions.Count - 1, Status = ResultStatus.Completed
        });

        var result = grading.BuildResult(session, test, given.ToDictionary(d => d.QuestionId), timedOut: false);
        result.Date = when;

        session.Status = result.Status;
        results.SaveSession(session);
        results.SaveResult(result);
    }

    private static string? CreateDemoUpload()
    {
        try
        {
            string dir = Path.Combine(AppFiles.Root, "uploads");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "demo_lab_solution.txt");
            File.WriteAllText(path, "Демо-файл учня: розв'язок лабораторної роботи.\nТут міг би бути ваш код.");
            return path;
        }
        catch { return null; }
    }

    // --- відповіді учня ---
    private static StudentAnswerDetail Pick(Question q, int index) => new() { QuestionId = q.Id, SelectedAnswerIds = { q.Answers[index].Id } };
    private static StudentAnswerDetail PickMany(Question q, params int[] indexes) => new() { QuestionId = q.Id, SelectedAnswerIds = indexes.Select(i => q.Answers[i].Id).ToList() };
    private static StudentAnswerDetail Typed(Question q, string text) => new() { QuestionId = q.Id, GivenTextAnswer = text };
    private static StudentAnswerDetail UploadedFile(Question q, string? path) => new() { QuestionId = q.Id, UploadedFilePath = path };

    /// <summary>Sequence: індекси варіантів у порядку, розставленому учнем.</summary>
    private static StudentAnswerDetail Ordered(Question q, params int[] indexes)
        => new() { QuestionId = q.Id, SelectedAnswerIds = indexes.Select(i => q.Answers[i].Id).ToList() };

    /// <summary>Matching: для кожного лівого рядка — індекс варіанта, чия права частина обрана.</summary>
    private static StudentAnswerDetail Matched(Question q, params int[] rightOwnerIndexes)
        => new() { QuestionId = q.Id, SelectedAnswerIds = rightOwnerIndexes.Select(i => q.Answers[i].Id).ToList() };

    // --- конструктори питань ---
    private static Question Single(int cat, string text, double max, string explanation, params (string Text, bool Correct)[] answers) => new()
    {
        CategoryId = cat, Text = text, QuestionType = QuestionType.SingleChoice, MaxScore = max, Explanation = explanation,
        Answers = answers.Select((a, i) => new Answer { Text = a.Text, IsCorrect = a.Correct, OrderIndex = i }).ToList()
    };

    private static Question Multiple(int cat, string text, double max, string explanation, params (string Text, bool Correct)[] answers) => new()
    {
        CategoryId = cat, Text = text, QuestionType = QuestionType.MultipleChoice, MaxScore = max, Explanation = explanation,
        Answers = answers.Select((a, i) => new Answer { Text = a.Text, IsCorrect = a.Correct, OrderIndex = i }).ToList()
    };

    private static Question TextQ(int cat, string text, double max, TextEvaluationType eval, string explanation, params string[] accepted) => new()
    {
        CategoryId = cat, Text = text, QuestionType = QuestionType.TextAnswer, TextEvaluationType = eval, MaxScore = max, Explanation = explanation,
        Answers = accepted.Select((a, i) => new Answer { Text = a, IsCorrect = true, OrderIndex = i }).ToList()
    };

    private static Question Matching(int cat, string text, double max, string explanation, params (string Left, string Right)[] pairs) => new()
    {
        CategoryId = cat, Text = text, QuestionType = QuestionType.Matching, MaxScore = max, Explanation = explanation,
        Answers = pairs.Select((p, i) => new Answer { Text = p.Left, CorrectPairText = p.Right, OrderIndex = i }).ToList()
    };

    private static Question Sequence(int cat, string text, double max, string explanation, params string[] itemsInCorrectOrder) => new()
    {
        CategoryId = cat, Text = text, QuestionType = QuestionType.Sequence, MaxScore = max, Explanation = explanation,
        Answers = itemsInCorrectOrder.Select((t, i) => new Answer { Text = t, OrderIndex = i }).ToList()
    };

    private static Question FileQ(int cat, string text, double max, string explanation) => new()
    {
        CategoryId = cat, Text = text, QuestionType = QuestionType.FileUpload, MaxScore = max, Explanation = explanation
    };
}
