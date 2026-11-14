namespace PharmacyManagement.Domain.Entities;

public sealed class StockMovement
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public string? Reference { get; set; }
    public int UserId { get; set; }
    public string? Notes { get; set; }
}
