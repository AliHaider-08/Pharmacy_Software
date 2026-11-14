using PharmacyManagement.Application.Abstractions;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class UserSession : IUserSession
{
    public AuthenticatedUser? Current { get; set; }
}
