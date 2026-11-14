namespace PharmacyManagement.Domain.Entities;

public sealed class Expense
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public int UserId { get; set; }
}
