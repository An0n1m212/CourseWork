using TestingSystem.Models;

namespace TestingSystem.Abstractions;

public interface IUserRepository
{
    User? GetById(int id);
    User? GetByUsername(string username);
    IReadOnlyList<User> GetAll();
    IReadOnlyList<User> GetStudents();

    /// <summary>Присвоює Id; кидає InvalidOperationException, якщо логін зайнятий.</summary>
    User Create(User user);

    bool UpdateProfile(int userId, string fullName, string email, string? avatarPath);

    /// <summary>Меняет группу пользователя (UPDATE Users SET GroupId=@groupId WHERE Id=@id).</summary>
    bool UpdateGroup(int userId, int? groupId);

    /// <summary>Compare-and-swap: хеш змінюється лише якщо збережений хеш дорівнює oldPasswordHash.</summary>
    bool ChangePassword(int userId, string oldPasswordHash, string newPasswordHash);

    void Delete(int id);
}
