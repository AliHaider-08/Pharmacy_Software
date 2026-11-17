using PharmacyManagement.Application.Dashboard;

namespace PharmacyManagement.Application.Abstractions;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(int expiryAlertDays = 90, CancellationToken cancellationToken = default);
}
