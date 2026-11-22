using TestingSystem.Models;

namespace TestingSystem.Abstractions;

/// <summary>Результати та сесії тестування (сесія — «спроба», результат — її підсумок).</summary>
public interface IResultRepository
{
    Result SaveResult(Result result);              // вставка/оновлення
    Result? GetById(int id);
    IReadOnlyList<Result> GetAll();
    IReadOnlyList<Result> GetByStudent(int studentId);
    IReadOnlyList<Result> GetByTest(int testId);
    IReadOnlyList<Result> GetPendingReview();
    void DeleteResult(int id);

    TestSession SaveSession(TestSession session);  // вставка/оновлення
    TestSession? GetSession(int id);
    IReadOnlyList<TestSession> GetSessions(int studentId, int testId);
}
