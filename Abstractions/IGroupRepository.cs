using TestingSystem.Models;

namespace TestingSystem.Abstractions;

public interface IGroupRepository
{
    IReadOnlyList<Group> GetAll();
    Group? GetById(int id);
    /// <summary>Створює (Id == 0) або оновлює групу.</summary>
    Group Save(Group group);
    void Delete(int id);
}
