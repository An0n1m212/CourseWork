using System.Net.Mail;
using TestingSystem.Abstractions;
using TestingSystem.Models;

namespace TestingSystem.Services;

/// <summary>
/// Аутентифікація, реєстрація, операції з профілем і перевірки прав. Працює поверх IUserRepository,
/// тому при зміні сховища не змінюється. Для JWT/Identity/LDAP пишеться інша реалізація IAuthService.
/// </summary>
public class AuthService : IAuthService
{
    public const int MinPasswordLength = 6;

    private readonly IUserRepository _users;
    private readonly IGroupRepository _groups;
    private User? _current;

    public event EventHandler? CurrentUserChanged;

    public AuthService(IUserRepository users, IGroupRepository groups)
    {
        _users = users;
        _groups = groups;
    }

    public User? Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;

        var user = _users.GetByUsername(username.Trim());
        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;

        _current = user;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        return user;
    }

    public void Logout()
    {
        _current = null;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }

    public User? GetCurrentUser() => _current;

    public bool Reauthenticate(string password)
    {
        if (_current is null || string.IsNullOrEmpty(password)) return false;

        var stored = _users.GetById(_current.Id);        // беремо актуальний хеш зі сховища
        return stored != null && PasswordHasher.Verify(password, stored.PasswordHash);
    }

    public bool IsInRole(params Role[] roles) => _current != null && roles.Contains(_current.Role);

    public bool CanManage(Test test)
        => _current != null && (_current.Role == Role.Admin || (_current.Role == Role.Teacher && test.AuthorTeacherId == _current.Id));

    public bool CanManage(Lecture lecture)
        => _current != null && (_current.Role == Role.Admin || (_current.Role == Role.Teacher && lecture.AuthorTeacherId == _current.Id));

    public AuthResult RegisterStudent(string username, string password, string fullName, string email, int? groupId)
    {
        if (string.IsNullOrWhiteSpace(username)) return AuthResult.UsernameRequired;
        if (password.Length < MinPasswordLength) return AuthResult.PasswordTooShort;
        if (string.IsNullOrWhiteSpace(fullName)) return AuthResult.FullNameRequired;
        if (!IsValidEmail(email.Trim())) return AuthResult.InvalidEmail;
        if (groupId is null || _groups.GetById(groupId.Value) is null) return AuthResult.GroupRequired;

        try
        {
            _users.Create(new User
            {
                Username = username.Trim(),
                PasswordHash = PasswordHasher.Hash(password),
                FullName = fullName.Trim(),
                Email = email.Trim(),
                GroupId = groupId,
                Role = Role.Student,               // самореєстрація — лише роль «Учень»
                RegistrationDate = DateTime.Now
            });
            return AuthResult.Success;
        }
        catch (InvalidOperationException) { return AuthResult.UsernameTaken; }
    }

    /// <summary>
    /// Потік: валідація -> IUserRepository.UpdateProfile (InMemory: правка запису в словнику;
    /// SQLite/EF Core: UPDATE Users ...) -> перечитуємо користувача -> повідомляємо UI (шапка).
    /// </summary>
    public AuthResult UpdateProfile(string fullName, string email, string? avatarPath)
    {
        if (_current is null) return AuthResult.UserNotFound;

        fullName = fullName.Trim();
        email = email.Trim();
        if (fullName.Length == 0) return AuthResult.FullNameRequired;
        if (!IsValidEmail(email)) return AuthResult.InvalidEmail;

        if (!_users.UpdateProfile(_current.Id, fullName, email, avatarPath)) return AuthResult.UserNotFound;

        _current = _users.GetById(_current.Id);
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        return AuthResult.Success;
    }

    /// <summary>Зміна пароля: старий пароль -> довжина нового -> підтвердження -> «не той самий» -> запис (CAS).</summary>
    public AuthResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
    {
        if (_current is null) return AuthResult.UserNotFound;

        var stored = _users.GetById(_current.Id);
        if (stored is null) return AuthResult.UserNotFound;

        if (!PasswordHasher.Verify(oldPassword, stored.PasswordHash)) return AuthResult.WrongOldPassword;
        if (newPassword.Length < MinPasswordLength) return AuthResult.PasswordTooShort;
        if (newPassword != confirmPassword) return AuthResult.PasswordMismatch;
        if (PasswordHasher.Verify(newPassword, stored.PasswordHash)) return AuthResult.SamePassword;

        bool ok = _users.ChangePassword(_current.Id, stored.PasswordHash, PasswordHasher.Hash(newPassword));
        if (!ok) return AuthResult.Conflict;

        _current = _users.GetById(_current.Id);
        return AuthResult.Success;
    }

    /// <summary>Смена группы студента: только админ, только для студентов, группа должна существовать.</summary>
    public AuthResult ChangeUserGroup(int userId, int groupId)
    {
        if (!IsInRole(Role.Admin)) return AuthResult.AccessDenied;

        var user = _users.GetById(userId);
        if (user is null || user.Role != Role.Student) return AuthResult.UserNotFound;
        if (_groups.GetById(groupId) is null) return AuthResult.GroupRequired;

        return _users.UpdateGroup(userId, groupId) ? AuthResult.Success : AuthResult.UserNotFound;
    }

    private static bool IsValidEmail(string email)
        => email.Length > 0 && MailAddress.TryCreate(email, out var addr) && addr.Address == email;
}
