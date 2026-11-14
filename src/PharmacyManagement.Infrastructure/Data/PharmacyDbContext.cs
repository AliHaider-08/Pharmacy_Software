using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Infrastructure.Data;

public sealed class PharmacyDbContext(DbContextOptions<PharmacyDbContext> options) : DbContext(options)
{
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PharmacySettings> Settings => Set<PharmacySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Medicine>(e =>
        {
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("Barcode IS NOT NULL AND Barcode <> ''");
            e.HasIndex(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.DefaultPurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.DefaultSalePrice).HasPrecision(12, 2);
            e.HasOne(x => x.Category).WithMany(x => x.Medicines).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<StockBatch>(e =>
        {
            e.HasIndex(x => new { x.MedicineId, x.BatchNumber }).IsUnique();
            e.HasIndex(x => x.ExpiryDate);
            e.ToTable(t => t.HasCheckConstraint("CK_StockBatch_Quantity_NonNegative", "QuantityOnHand >= 0"));
            e.Property(x => x.PurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.SalePrice).HasPrecision(12, 2);
            e.HasOne(x => x.Medicine).WithMany(x => x.Batches).HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supplier).WithMany(x => x.Batches).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<Sale>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.HasIndex(x => x.SoldAtUtc);
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.Discount).HasPrecision(12, 2);
            e.Property(x => x.GrandTotal).HasPrecision(12, 2);
            e.Property(x => x.PaidAmount).HasPrecision(12, 2);
        });
        modelBuilder.Entity<SaleLine>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Property(x => x.UnitCost).HasPrecision(12, 2);
            e.Property(x => x.Discount).HasPrecision(12, 2);
            e.HasOne(x => x.Sale).WithMany(x => x.Lines).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Purchase>(e =>
        {
            e.HasIndex(x => x.InvoiceNumber).IsUnique();
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.Discount).HasPrecision(12, 2);
            e.Property(x => x.GrandTotal).HasPrecision(12, 2);
            e.Property(x => x.PaidAmount).HasPrecision(12, 2);
        });
        modelBuilder.Entity<PurchaseLine>(e =>
        {
            e.Property(x => x.PurchasePrice).HasPrecision(12, 2);
            e.Property(x => x.SalePrice).HasPrecision(12, 2);
            e.Property(x => x.LineTotal).HasPrecision(12, 2);
        });
        modelBuilder.Entity<Supplier>().Property(x => x.OpeningBalance).HasPrecision(12, 2);
        modelBuilder.Entity<Expense>().Property(x => x.Amount).HasPrecision(12, 2);
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<PharmacySettings>().HasData(new PharmacySettings { Id = 1 });
    }
}

