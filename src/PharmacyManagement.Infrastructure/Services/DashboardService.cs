using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Application.Dashboard;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class DashboardService(PharmacyDbContext db) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var localDayStart = DateTime.Today;
        var todayUtc = localDayStart.ToUniversalTime();
        var tomorrowUtc = localDayStart.AddDays(1).ToUniversalTime();
        var nearExpiry = DateOnly.FromDateTime(localDayStart.AddDays(90));

        var medicines = await db.Medicines.AsNoTracking().Where(x => x.IsActive).Select(x => new
        {
            x.Id,
            x.MinimumStockLevel,
            Stock = (int?)x.Batches.Sum(b => b.QuantityOnHand) ?? 0
        }).ToListAsync(cancellationToken);

        var salesToday = await db.Sales.AsNoTracking()
            .Where(x => x.SoldAtUtc >= todayUtc && x.SoldAtUtc < tomorrowUtc)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;
        var purchasesToday = await db.Purchases.AsNoTracking()
            .Where(x => x.PurchasedAtUtc >= todayUtc && x.PurchasedAtUtc < tomorrowUtc)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;
        var profitToday = await db.SaleLines.AsNoTracking()
            .Where(x => x.Sale.SoldAtUtc >= todayUtc && x.Sale.SoldAtUtc < tomorrowUtc)
            .SumAsync(x => (decimal?)((x.UnitPrice - x.UnitCost) * x.Quantity - x.Discount), cancellationToken) ?? 0m;
        var expiring = await db.StockBatches.AsNoTracking().CountAsync(x => x.QuantityOnHand > 0 && x.ExpiryDate != null && x.ExpiryDate <= nearExpiry && x.ExpiryDate >= DateOnly.FromDateTime(localDayStart), cancellationToken);

        return new DashboardSummary(
            medicines.Count,
            medicines.Sum(x => x.Stock),
            salesToday,
            purchasesToday,
            profitToday,
            medicines.Count(x => x.Stock <= x.MinimumStockLevel),
            expiring,
            await db.Suppliers.AsNoTracking().CountAsync(cancellationToken),
            await db.Customers.AsNoTracking().CountAsync(cancellationToken));
    }
}
