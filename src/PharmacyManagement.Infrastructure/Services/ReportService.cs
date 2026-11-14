using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class ReportService(PharmacyDbContext db, IUserSession session) : IReportService
{
    public async Task<IReadOnlyList<ReportLine>> GetSalesReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var start = from.Date.ToUniversalTime(); var end = to.Date.AddDays(1).ToUniversalTime();
        var rows = await db.Sales.AsNoTracking().Where(x => x.SoldAtUtc >= start && x.SoldAtUtc < end)
            .OrderByDescending(x => x.SoldAtUtc).ToListAsync(cancellationToken);
        return rows.Select(x => new ReportLine(x.SoldAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), x.InvoiceNumber, x.CustomerId == null ? "Walk-in customer" : "Registered customer", x.GrandTotal, x.PaidAmount, x.GrandTotal - x.PaidAmount)).ToList();
    }

    public async Task<IReadOnlyList<ReportLine>> GetPurchaseReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var start = from.Date.ToUniversalTime(); var end = to.Date.AddDays(1).ToUniversalTime();
        var rows = await db.Purchases.AsNoTracking().Where(x => x.PurchasedAtUtc >= start && x.PurchasedAtUtc < end)
            .OrderByDescending(x => x.PurchasedAtUtc).ToListAsync(cancellationToken);
        return rows.Select(x => new ReportLine(x.PurchasedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), x.InvoiceNumber, $"Supplier #{x.SupplierId}", x.GrandTotal, x.PaidAmount, x.GrandTotal - x.PaidAmount)).ToList();
    }

    public async Task<IReadOnlyList<ReportLine>> GetExpenseReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var start = from.Date.ToUniversalTime(); var end = to.Date.AddDays(1).ToUniversalTime();
        var rows = await db.Expenses.AsNoTracking().Where(x => x.OccurredAtUtc >= start && x.OccurredAtUtc < end)
            .OrderByDescending(x => x.OccurredAtUtc).ToListAsync(cancellationToken);
        return rows.Select(x => new ReportLine(x.OccurredAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), x.Category, x.Description ?? "Expense", x.Amount, x.Amount, 0)).ToList();
    }

    public async Task<IReadOnlyList<StockReportLine>> GetStockReportAsync(bool lowStockOnly = false, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var medicineStock = await db.Medicines.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Id, x.MinimumStockLevel, Stock = x.Batches.Sum(b => (int?)b.QuantityOnHand) ?? 0 }).ToListAsync(cancellationToken);
        var lowIds = medicineStock.Where(x => x.Stock <= x.MinimumStockLevel).Select(x => x.Id).ToHashSet();
        var batches = db.StockBatches.AsNoTracking().Include(x => x.Medicine).ThenInclude(x => x.Category).AsQueryable();
        if (lowStockOnly) batches = batches.Where(x => lowIds.Contains(x.MedicineId));
        var records = await batches.OrderBy(x => x.Medicine.Name).ThenBy(x => x.ExpiryDate)
            .Select(x => new { Medicine = x.Medicine.Name, Category = x.Medicine.Category == null ? "Uncategorized" : x.Medicine.Category.Name, x.BatchNumber, x.ExpiryDate, x.QuantityOnHand, x.PurchasePrice, x.SalePrice }).ToListAsync(cancellationToken);
        return records.Select(x => new StockReportLine(x.Medicine, x.Category, x.BatchNumber, x.ExpiryDate?.ToString("yyyy-MM-dd") ?? "No expiry", x.QuantityOnHand, x.PurchasePrice, x.SalePrice)).ToList();
    }
}
