namespace PharmacyManagement.Application.Abstractions;

public sealed record UserListItem(int Id, string Username, string Role, bool IsActive, DateTime CreatedAtLocal);
public sealed record AuthenticatedUser(int Id, string Username, string Role);

public interface IUserService
{
    Task<bool> HasUsersAsync(CancellationToken cancellationToken = default);
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<AuthenticatedUser?> RecoverAdministratorPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default);
    Task CreateInitialAdministratorAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task CreateUserAsync(string username, string password, string role, CancellationToken cancellationToken = default);
    Task SetUserActiveAsync(int id, bool active, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int id, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}

public interface IUserSession
{
    AuthenticatedUser? Current { get; set; }
}
