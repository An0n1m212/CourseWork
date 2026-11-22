using TestingSystem.Models;

namespace TestingSystem.Abstractions;

public interface ITestRepository
{
    IReadOnlyList<Test> GetAll();
    Test? GetById(int id);
    /// <summary>Створює (Id == 0) або оновлює тест з питаннями та відповідями; присвоює Id і зовнішні ключі.</summary>
    Test Save(Test test);
    void Delete(int id);
}
