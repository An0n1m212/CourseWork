using TestingSystem.Models;

namespace TestingSystem.Abstractions;

/// <summary>Коди результатів операцій: UI підбирає локалізований текст за ключем "Err_" + код.</summary>
public enum AuthResult
{
    Success,
    UsernameRequired, UsernameTaken,
    FullNameRequired, InvalidEmail, GroupRequired,
    PasswordTooShort, PasswordMismatch, WrongOldPassword, SamePassword,
    UserNotFound, Conflict, AccessDenied
}

public interface IAuthService
{
    User? Login(string username, string password);
    void Logout();
    User? GetCurrentUser();
    event EventHandler? CurrentUserChanged;

    AuthResult RegisterStudent(string username, string password, string fullName, string email, int? groupId);
    AuthResult UpdateProfile(string fullName, string email, string? avatarPath);
    AuthResult ChangePassword(string oldPassword, string newPassword, string confirmPassword);

    /// <summary>Админ переводит студента в другую группу.</summary>
    AuthResult ChangeUserGroup(int userId, int groupId);

    /// <summary>
    /// Повторна перевірка пароля ПОТОЧНОГО користувача (розблокування після бездіяльності).
    /// Не змінює сесію і не піднімає CurrentUserChanged.
    /// </summary>
    bool Reauthenticate(string password);

    /// <summary>Перевірка прав за роллю поточного користувача.</summary>
    bool IsInRole(params Role[] roles);

    /// <summary>Адмін керує будь-якими тестами, вчитель — лише своїми.</summary>
    bool CanManage(Test test);

    /// <summary>Те саме для лекцій: адмін — будь-які, викладач — власні.</summary>
    bool CanManage(Lecture lecture);
}
