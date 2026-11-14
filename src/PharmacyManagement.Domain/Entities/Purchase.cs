namespace PharmacyManagement.Domain.Entities;

public sealed class Purchase
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public DateTime PurchasedAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public ICollection<PurchaseLine> Lines { get; set; } = new List<PurchaseLine>();
}

public sealed class PurchaseLine
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int MedicineId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal LineTotal { get; set; }
}
