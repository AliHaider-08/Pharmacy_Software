namespace PharmacyManagement.Domain.Entities;

public sealed class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Cashier";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}
