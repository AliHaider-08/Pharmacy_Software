using PharmacyManagement.Application.Abstractions;

namespace PharmacyManagement.Infrastructure.Services;

internal static class UserAuthorization
{
    public static void RequireManager(IUserSession session)
    {
        if (session.Current?.Role is not ("Administrator" or "Manager")) throw new UnauthorizedAccessException("This action requires a manager or administrator account.");
    }

    public static void RequireAdministrator(IUserSession session)
    {
        if (session.Current?.Role != "Administrator") throw new UnauthorizedAccessException("This action requires an administrator account.");
    }
}
