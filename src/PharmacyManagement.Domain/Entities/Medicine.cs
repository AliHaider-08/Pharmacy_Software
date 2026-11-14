namespace PharmacyManagement.Domain.Entities;

public sealed class Medicine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? BrandName { get; set; }
    public int? CategoryId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public decimal DefaultPurchasePrice { get; set; }
    public decimal DefaultSalePrice { get; set; }
    public int MinimumStockLevel { get; set; }
    public string Unit { get; set; } = "Unit";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Category? Category { get; set; }
    public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
}
