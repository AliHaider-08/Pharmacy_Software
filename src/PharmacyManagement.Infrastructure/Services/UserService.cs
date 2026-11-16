using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class UserService(PharmacyDbContext db, IUserSession session) : IUserService
{
    private const int Iterations = 600_000;
    private static readonly string[] Roles = ["Administrator", "Manager", "Cashier"];

    public Task<bool> HasUsersAsync(CancellationToken cancellationToken = default) => db.Users.AnyAsync(cancellationToken);

    public async Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username.Trim(), cancellationToken);
        if (user is null || !user.IsActive || !VerifyPassword(password, user.PasswordHash))
        {
            db.AuditLogs.Add(new AuditLog { OccurredAtUtc = DateTime.UtcNow, Action = "Login failed", EntityName = "AppUser", Details = $"Username: {username.Trim()}" });
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, OccurredAtUtc = DateTime.UtcNow, Action = "Login succeeded", EntityName = "AppUser", EntityId = user.Id.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        return new AuthenticatedUser(user.Id, user.Username, user.Role);
    }

    public async Task<AuthenticatedUser?> RecoverAdministratorPasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default)
    {
        ValidatePassword(newPassword);
        var normalized = username.Trim();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == normalized && x.Role == "Administrator" && x.IsActive, cancellationToken);
        if (user is null) return null;

        user.PasswordHash = HashPassword(newPassword);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            OccurredAtUtc = DateTime.UtcNow,
            Action = "Administrator password recovered locally",
            EntityName = "AppUser",
            EntityId = user.Id.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);
        return new AuthenticatedUser(user.Id, user.Username, user.Role);
    }

    public async Task CreateInitialAdministratorAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(username, password);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken)) throw new InvalidOperationException("An account already exists. Sign in instead.");
        var user = new AppUser { Username = username.Trim(), PasswordHash = HashPassword(password), Role = "Administrator", IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, OccurredAtUtc = DateTime.UtcNow, Action = "Initial administrator created", EntityName = "AppUser", EntityId = user.Id.ToString() });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireAdministrator(session);
        var users = await db.Users.AsNoTracking().OrderBy(x => x.Username).ToListAsync(cancellationToken);
        return users.Select(x => new UserListItem(x.Id, x.Username, x.Role, x.IsActive, x.CreatedAtUtc.ToLocalTime())).ToList();
    }

    public async Task CreateUserAsync(string username, string password, string role, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireAdministrator(session);
        ValidateCredentials(username, password);
        if (!Roles.Contains(role)) throw new InvalidOperationException("Choose Administrator, Manager or Cashier.");
        var normalized = username.Trim();
        if (await db.Users.AnyAsync(x => x.Username == normalized, cancellationToken)) throw new InvalidOperationException("That username is already in use.");
        var user = new AppUser { Username = normalized, PasswordHash = HashPassword(password), Role = role, IsActive = true, CreatedAtUtc = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, OccurredAtUtc = DateTime.UtcNow, Action = "User created", EntityName = "AppUser", EntityId = user.Id.ToString(), Details = $"Role: {role}" });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserActiveAsync(int id, bool active, CancellationToken cancellationToken = default)
    {
        if (id == session.Current?.Id && !active) throw new InvalidOperationException("You cannot deactivate your own active session.");
        UserAuthorization.RequireAdministrator(session);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("User no longer exists.");
        if (!active && user.Role == "Administrator" && await db.Users.CountAsync(x => x.Role == "Administrator" && x.IsActive, cancellationToken) <= 1)
            throw new InvalidOperationException("The last active administrator cannot be deactivated.");
        user.IsActive = active;
        db.AuditLogs.Add(new AuditLog { OccurredAtUtc = DateTime.UtcNow, Action = active ? "User activated" : "User deactivated", EntityName = "AppUser", EntityId = user.Id.ToString() });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(int id, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (id != session.Current?.Id) throw new UnauthorizedAccessException("You can only change your own password.");
        ValidatePassword(newPassword);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken) ?? throw new InvalidOperationException("User is not available.");
        if (!VerifyPassword(currentPassword, user.PasswordHash)) throw new InvalidOperationException("Current password is incorrect.");
        user.PasswordHash = HashPassword(newPassword);
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, OccurredAtUtc = DateTime.UtcNow, Action = "Password changed", EntityName = "AppUser", EntityId = user.Id.ToString() });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3) throw new InvalidOperationException("Username must contain at least 3 characters.");
        ValidatePassword(password);
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 10) throw new InvalidOperationException("Password must be at least 10 characters long.");
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations) || iterations < 100_000) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
