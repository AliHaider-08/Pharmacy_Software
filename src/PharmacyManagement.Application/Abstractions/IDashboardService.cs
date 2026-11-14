using PharmacyManagement.Application.Dashboard;

namespace PharmacyManagement.Application.Abstractions;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
