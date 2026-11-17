using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Application.Abstractions;

public sealed record MedicineListItem(int Id, string Name, string? GenericName, string? BrandName, string? CategoryName, string? Manufacturer, string? Barcode, decimal PurchasePrice, decimal SalePrice, int MinimumStock, int Stock, bool IsActive);
public sealed record CategoryListItem(int Id, string Name, string? Description, int MedicineCount);
public sealed record StockBatchListItem(int Id, int MedicineId, string MedicineName, string BatchNumber, DateOnly? ExpiryDate, decimal PurchasePrice, decimal SalePrice, int Quantity, string? SupplierName);
public sealed record StockMovementListItem(DateTime DateLocal, string Medicine, string Batch, string Type, int QuantityChange, string? Reference, string? Notes);
public sealed record MedicineInput(int? Id, string Name, string? GenericName, string? BrandName, int? CategoryId, string? Manufacturer, string? Barcode, string? Description, decimal PurchasePrice, decimal SalePrice, int MinimumStock, string Unit);
public sealed record MedicineWithOpeningStockInput(MedicineInput Medicine, string? BatchNumber, DateOnly? ExpiryDate, int OpeningQuantity, int? SupplierId = null);
public sealed record BatchInput(int MedicineId, string BatchNumber, DateOnly? ExpiryDate, decimal PurchasePrice, decimal SalePrice, int Quantity, int? SupplierId);
public sealed record CartLineRequest(int MedicineId, int Quantity);
public sealed record CheckoutRequest(IReadOnlyList<CartLineRequest> Lines, decimal Discount, decimal PaidAmount, string PaymentMethod, int? CustomerId = null, int UserId = 1);
public sealed record CheckoutLine(string MedicineName, int Quantity, decimal UnitPrice, decimal Discount, decimal LineTotal);
public sealed record CheckoutResult(string InvoiceNumber, DateTime SoldAtLocal, decimal Subtotal, decimal Discount, decimal Total, decimal Paid, decimal Change, string PaymentMethod, IReadOnlyList<CheckoutLine> Lines);
public sealed record SaleListItem(string InvoiceNumber, DateTime SoldAtLocal, decimal Total, decimal Paid, string PaymentMethod);

public interface ICatalogService
{
    Task<IReadOnlyList<MedicineListItem>> SearchMedicinesAsync(string? query = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryListItem>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default);
    Task SaveMedicineAsync(MedicineInput input, CancellationToken cancellationToken = default);
    Task SaveMedicineWithOpeningStockAsync(MedicineWithOpeningStockInput input, CancellationToken cancellationToken = default);
    Task SetMedicineActiveAsync(int medicineId, bool isActive, CancellationToken cancellationToken = default);
    Task SaveCategoryAsync(int? id, string name, string? description, CancellationToken cancellationToken = default);
    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);
}

public interface IInventoryService
{
    Task<IReadOnlyList<StockBatchListItem>> GetBatchesAsync(string? query = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovementListItem>> GetMovementsAsync(CancellationToken cancellationToken = default);
    Task AddBatchAsync(BatchInput input, CancellationToken cancellationToken = default);
    Task AdjustStockAsync(int batchId, int quantityChange, string reason, CancellationToken cancellationToken = default);
}

public interface ISalesService
{
    Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleListItem>> GetRecentSalesAsync(CancellationToken cancellationToken = default);
    Task<CheckoutResult> GetReceiptAsync(string invoiceNumber, CancellationToken cancellationToken = default);
}
