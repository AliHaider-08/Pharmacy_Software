using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Application.Abstractions;

public sealed record SupplierListItem(int Id, string Name, string? CompanyName, string? Phone, string? Email, string? Address, decimal OpeningBalance);
public sealed record CustomerListItem(int Id, string Name, string? Phone, string? Email, string? Address, string CustomerType);
public sealed record PurchaseListItem(string InvoiceNumber, DateTime PurchasedAtLocal, string Supplier, decimal Total, decimal Paid, string Status);
public sealed record ExpenseListItem(int Id, DateTime DateLocal, string Category, decimal Amount, string PaymentMethod, string? Description);
public sealed record PurchaseItemInput(int MedicineId, string BatchNumber, DateOnly? ExpiryDate, int Quantity, decimal PurchasePrice, decimal SalePrice, string MedicineName = "");
public sealed record PurchaseRequest(int SupplierId, string? InvoiceNumber, decimal Discount, decimal PaidAmount, IReadOnlyList<PurchaseItemInput> Lines);
public sealed record PurchaseResult(string InvoiceNumber, decimal Subtotal, decimal Discount, decimal Total, decimal Paid);
public sealed record ExpenseInput(string Category, DateTime Date, decimal Amount, string PaymentMethod, string? Description, int UserId = 1);

public interface IBackOfficeService
{
    Task<IReadOnlyList<SupplierListItem>> GetSuppliersAsync(string? search = null, CancellationToken cancellationToken = default);
    Task SaveSupplierAsync(SupplierListItem supplier, CancellationToken cancellationToken = default);
    Task DeleteSupplierAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerListItem>> GetCustomersAsync(string? search = null, CancellationToken cancellationToken = default);
    Task SaveCustomerAsync(CustomerListItem customer, CancellationToken cancellationToken = default);
    Task DeleteCustomerAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseListItem>> GetPurchasesAsync(CancellationToken cancellationToken = default);
    Task<PurchaseResult> CreatePurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseListItem>> GetExpensesAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task AddExpenseAsync(ExpenseInput input, CancellationToken cancellationToken = default);
    Task<PharmacySettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(PharmacySettings settings, CancellationToken cancellationToken = default);
}

public interface IDatabaseBackupService
{
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);
    Task RestoreBackupAsync(string sourcePath, CancellationToken cancellationToken = default);
}

public sealed record ReportLine(string Date, string Reference, string Description, decimal Amount, decimal Paid, decimal Balance);
public sealed record StockReportLine(string Medicine, string Category, string Batch, string Expiry, int Quantity, decimal Cost, decimal Price);
public interface IReportService
{
    Task<IReadOnlyList<ReportLine>> GetSalesReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportLine>> GetPurchaseReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportLine>> GetExpenseReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockReportLine>> GetStockReportAsync(bool lowStockOnly = false, CancellationToken cancellationToken = default);
}
