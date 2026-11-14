namespace PharmacyManagement.Domain.Entities;

public sealed class StockBatch
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ManufactureDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int QuantityOnHand { get; set; }
    public int? SupplierId { get; set; }
    public Medicine Medicine { get; set; } = null!;
    public Supplier? Supplier { get; set; }
}
