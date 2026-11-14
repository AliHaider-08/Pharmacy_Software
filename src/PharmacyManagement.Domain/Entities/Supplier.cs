namespace PharmacyManagement.Domain.Entities;

public sealed class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal OpeningBalance { get; set; }
    public ICollection<StockBatch> Batches { get; set; } = new List<StockBatch>();
}
