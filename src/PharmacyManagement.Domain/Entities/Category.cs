namespace PharmacyManagement.Domain.Entities;

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Medicine> Medicines { get; set; } = new List<Medicine>();
}
