namespace PharmacyManagement.Application.Dashboard;

public sealed record DashboardSummary(
    int MedicineCount,
    int StockUnits,
    decimal SalesToday,
    decimal PurchasesToday,
    decimal ProfitToday,
    int LowStockMedicineCount,
    int ExpiringBatchCount,
    int SupplierCount,
    int CustomerCount);
