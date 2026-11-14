namespace PharmacyManagement.Domain.Entities;

public sealed class Sale
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SoldAtUtc { get; set; }
    public int? CustomerId { get; set; }
    public int UserId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
}

public sealed class SaleLine
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int BatchId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public Sale Sale { get; set; } = null!;
    public StockBatch Batch { get; set; } = null!;
}
