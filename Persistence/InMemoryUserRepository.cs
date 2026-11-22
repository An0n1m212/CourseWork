using TestingSystem.Abstractions;
using TestingSystem.Models;

namespace TestingSystem.Persistence;

/// <summary>Користувачі в пам'яті. Як і БД, віддає КОПІЇ записів.</summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();   // «таблиця Users»
    private readonly object _lock = new();
    private int _nextId;

    public User? GetById(int id) { lock (_lock) return _users.TryGetValue(id, out var u) ? u.Clone() : null; }

    public User? GetByUsername(string username)
    {
        lock (_lock)
            return _users.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))?.Clone();
    }

    public IReadOnlyList<User> GetAll() { lock (_lock) return _users.Values.OrderBy(u => u.Id).Select(u => u.Clone()).ToList(); }

    public IReadOnlyList<User> GetStudents()
    {
        lock (_lock) return _users.Values.Where(u => u.Role == Role.Student).OrderBy(u => u.FullName).Select(u => u.Clone()).ToList();
    }

    public User Create(User user)
    {
        lock (_lock)
        {
            if (_users.Values.Any(u => string.Equals(u.Username, user.Username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Username '{user.Username}' is already taken.");
            user.Id = ++_nextId;
            _users[user.Id] = user.Clone();
            return user;
        }
    }

    /// <summary>
    /// ЗАРАЗ (InMemory): правимо збережений запис у словнику. У SQLite/EF Core той самий виклик стане:
    ///   UPDATE Users SET FullName=@fullName, Email=@email, AvatarPath=@avatar WHERE Id=@id
    /// (EF Core: завантажити сутність, змінити поля, SaveChanges()). Сервіси та UI не змінюються.
    /// </summary>
    public bool UpdateProfile(int userId, string fullName, string email, string? avatarPath)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var stored)) return false;
            stored.FullName = fullName;
            stored.Email = email;
            stored.AvatarPath = avatarPath;
            return true;
        }
    }

    /// <summary>
    /// Compare-and-swap. SQL-аналог:
    ///   UPDATE Users SET PasswordHash=@new WHERE Id=@id AND PasswordHash=@old   -- 1 рядок = успіх
    /// Перевірку СТАРОГО ПАРОЛЯ виконує AuthService (хеші з сіллю напряму не порівняти).
    /// </summary>
    public bool ChangePassword(int userId, string oldPasswordHash, string newPasswordHash)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var stored) || stored.PasswordHash != oldPasswordHash) return false;
            stored.PasswordHash = newPasswordHash;
            return true;
        }
    }

    public bool UpdateGroup(int userId, int? groupId)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(userId, out var stored)) return false;
            stored.GroupId = groupId;
            return true;
        }
    }

    public void Delete(int id) { lock (_lock) _users.Remove(id); }
}
