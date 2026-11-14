namespace PharmacyManagement.Domain.Entities;

public sealed class PharmacySettings
{
    public int Id { get; set; }
    public string PharmacyName { get; set; } = "Pharmacy Management";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string CurrencyCode { get; set; } = "PKR";
    public string InvoicePrefix { get; set; } = "INV";
    public int ExpiryAlertDays { get; set; } = 90;
}
