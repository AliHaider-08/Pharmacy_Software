using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class BackOfficeService(PharmacyDbContext db, IUserSession session) : IBackOfficeService
{
    public async Task<IReadOnlyList<SupplierListItem>> GetSuppliersAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.CompanyName != null && x.CompanyName.Contains(term)) || (x.Phone != null && x.Phone.Contains(term)));
        }
        return await query.OrderBy(x => x.Name).Select(x => new SupplierListItem(x.Id, x.Name, x.CompanyName, x.Phone, x.Email, x.Address, x.OpeningBalance)).ToListAsync(cancellationToken);
    }

    public async Task SaveSupplierAsync(SupplierListItem input, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        if (string.IsNullOrWhiteSpace(input.Name)) throw new InvalidOperationException("Supplier name is required.");
        if (input.OpeningBalance < 0) throw new InvalidOperationException("Opening balance cannot be negative.");
        Supplier supplier;
        if (input.Id > 0) supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken) ?? throw new InvalidOperationException("Supplier no longer exists.");
        else { supplier = new Supplier(); db.Suppliers.Add(supplier); }
        supplier.Name = input.Name.Trim();
        supplier.CompanyName = Clean(input.CompanyName);
        supplier.Phone = Clean(input.Phone);
        supplier.Email = Clean(input.Email);
        supplier.Address = Clean(input.Address);
        supplier.OpeningBalance = input.OpeningBalance;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSupplierAsync(int id, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("Supplier no longer exists.");
        if (await db.Purchases.AnyAsync(x => x.SupplierId == id, cancellationToken) || await db.StockBatches.AnyAsync(x => x.SupplierId == id, cancellationToken))
            throw new InvalidOperationException("This supplier has purchase or batch history and cannot be deleted.");
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerListItem>> GetCustomersAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.Phone != null && x.Phone.Contains(term)) || (x.Email != null && x.Email.Contains(term)));
        }
        return await query.OrderBy(x => x.Name).Select(x => new CustomerListItem(x.Id, x.Name, x.Phone, x.Email, x.Address, x.CustomerType)).ToListAsync(cancellationToken);
    }

    public async Task SaveCustomerAsync(CustomerListItem input, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        if (string.IsNullOrWhiteSpace(input.Name)) throw new InvalidOperationException("Customer name is required.");
        Customer customer;
        if (input.Id > 0) customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken) ?? throw new InvalidOperationException("Customer no longer exists.");
        else { customer = new Customer(); db.Customers.Add(customer); }
        customer.Name = input.Name.Trim();
        customer.Phone = Clean(input.Phone);
        customer.Email = Clean(input.Email);
        customer.Address = Clean(input.Address);
        customer.CustomerType = string.IsNullOrWhiteSpace(input.CustomerType) ? "Retail" : input.CustomerType.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCustomerAsync(int id, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("Customer no longer exists.");
        if (await db.Sales.AnyAsync(x => x.CustomerId == id, cancellationToken)) throw new InvalidOperationException("This customer has sales history and cannot be deleted.");
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseListItem>> GetPurchasesAsync(CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var purchases = await db.Purchases.AsNoTracking().OrderByDescending(x => x.PurchasedAtUtc).Take(500).ToListAsync(cancellationToken);
        var supplierNames = await db.Suppliers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return purchases.Select(x => new PurchaseListItem(x.InvoiceNumber, x.PurchasedAtUtc.ToLocalTime(), supplierNames.GetValueOrDefault(x.SupplierId, "Unknown supplier"), x.GrandTotal, x.PaidAmount, x.PaymentStatus)).ToList();
    }

    public async Task<PurchaseResult> CreatePurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        if (request.Lines.Count == 0) throw new InvalidOperationException("Add at least one purchase item.");
        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId, cancellationToken)) throw new InvalidOperationException("Select an existing supplier.");
        if (request.Lines.Any(x => x.Quantity <= 0 || x.PurchasePrice < 0 || x.SalePrice < 0 || string.IsNullOrWhiteSpace(x.BatchNumber))) throw new InvalidOperationException("Each item needs a batch number, positive quantity and valid prices.");
        if (request.Lines.GroupBy(x => $"{x.MedicineId}\0{x.BatchNumber.Trim()}", StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) throw new InvalidOperationException("A medicine and batch can only appear once on a purchase.");
        var receivedEntries = new List<(StockBatch Batch, int Quantity)>();
        foreach (var item in request.Lines)
            if (!await db.Medicines.AnyAsync(x => x.Id == item.MedicineId && x.IsActive, cancellationToken)) throw new InvalidOperationException("A purchase item is no longer active.");

        var subtotal = request.Lines.Sum(x => x.Quantity * x.PurchasePrice);
        if (request.Discount < 0 || request.Discount > subtotal) throw new InvalidOperationException("Discount cannot exceed the purchase subtotal.");
        var discount = decimal.Round(request.Discount, 2, MidpointRounding.AwayFromZero);
        var total = subtotal - discount;
        if (request.PaidAmount < 0 || request.PaidAmount > total) throw new InvalidOperationException("Paid amount must be between zero and the invoice total.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var settings = await db.Settings.FirstOrDefaultAsync(cancellationToken);
        var invoice = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? $"PUR-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"
            : request.InvoiceNumber.Trim();
        if (await db.Purchases.AnyAsync(x => x.InvoiceNumber == invoice, cancellationToken)) throw new InvalidOperationException("That purchase invoice number already exists.");

        var purchase = new Purchase
        {
            InvoiceNumber = invoice,
            SupplierId = request.SupplierId,
            PurchasedAtUtc = DateTime.UtcNow,
            Subtotal = subtotal,
            Discount = discount,
            GrandTotal = total,
            PaidAmount = request.PaidAmount,
            PaymentStatus = request.PaidAmount == total ? "Paid" : request.PaidAmount == 0 ? "Unpaid" : "Partial"
        };
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in request.Lines)
        {
            var medicine = await db.Medicines.SingleAsync(x => x.Id == item.MedicineId, cancellationToken);
            var batchNumber = item.BatchNumber.Trim();
            var batch = await db.StockBatches.SingleOrDefaultAsync(x => x.MedicineId == item.MedicineId && x.BatchNumber == batchNumber, cancellationToken);
            if (batch is null)
            {
                batch = new StockBatch { MedicineId = item.MedicineId, BatchNumber = batchNumber, ExpiryDate = item.ExpiryDate, PurchasePrice = item.PurchasePrice, SalePrice = item.SalePrice, QuantityOnHand = item.Quantity, SupplierId = request.SupplierId };
                db.StockBatches.Add(batch);
                medicine.DefaultPurchasePrice = item.PurchasePrice;
                medicine.DefaultSalePrice = item.SalePrice;
            }
            else
            {
                if (batch.PurchasePrice != item.PurchasePrice || batch.SalePrice != item.SalePrice || batch.ExpiryDate != item.ExpiryDate)
                    throw new InvalidOperationException($"The existing batch {batchNumber} has different price or expiry details.");
                batch.QuantityOnHand += item.Quantity;
            }
            purchase.Lines.Add(new PurchaseLine { MedicineId = item.MedicineId, BatchNumber = batchNumber, ExpiryDate = item.ExpiryDate, Quantity = item.Quantity, PurchasePrice = item.PurchasePrice, SalePrice = item.SalePrice, LineTotal = item.Quantity * item.PurchasePrice });
            receivedEntries.Add((batch, item.Quantity));
        }
        await db.SaveChangesAsync(cancellationToken);
        db.StockMovements.AddRange(receivedEntries.Select(x => new StockMovement { BatchId = x.Batch.Id, OccurredAtUtc = DateTime.UtcNow, MovementType = "Purchase", QuantityChange = x.Quantity, Reference = invoice, UserId = session.Current?.Id ?? 1 }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PurchaseResult(invoice, subtotal, discount, total, request.PaidAmount);
    }

    public async Task<IReadOnlyList<ExpenseListItem>> GetExpensesAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var query = db.Expenses.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(x => x.OccurredAtUtc >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.OccurredAtUtc < to.Value.Date.AddDays(1).ToUniversalTime());
        var expenses = await query.OrderByDescending(x => x.OccurredAtUtc).ToListAsync(cancellationToken);
        return expenses.Select(x => new ExpenseListItem(x.Id, x.OccurredAtUtc.ToLocalTime(), x.Category, x.Amount, x.PaymentMethod, x.Description)).ToList();
    }

    public async Task AddExpenseAsync(ExpenseInput input, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        if (string.IsNullOrWhiteSpace(input.Category)) throw new InvalidOperationException("Choose an expense category.");
        if (input.Amount <= 0) throw new InvalidOperationException("Expense amount must be greater than zero.");
        db.Expenses.Add(new Expense { Category = input.Category.Trim(), OccurredAtUtc = input.Date.ToUniversalTime(), Amount = input.Amount, PaymentMethod = input.PaymentMethod, Description = Clean(input.Description), UserId = session.Current?.Id ?? input.UserId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PharmacySettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await db.Settings.FirstOrDefaultAsync(cancellationToken) ?? new PharmacySettings();
    }

    public async Task SaveSettingsAsync(PharmacySettings settings, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireAdministrator(session);
        if (string.IsNullOrWhiteSpace(settings.PharmacyName)) throw new InvalidOperationException("Pharmacy name is required.");
        if (settings.ExpiryAlertDays < 0) throw new InvalidOperationException("Expiry alert days cannot be negative.");
        var entity = await db.Settings.FirstOrDefaultAsync(cancellationToken);
        if (entity is null) { entity = new PharmacySettings { Id = 1 }; db.Settings.Add(entity); }
        entity.PharmacyName = settings.PharmacyName.Trim();
        entity.Address = Clean(settings.Address);
        entity.Phone = Clean(settings.Phone);
        entity.LogoPath = Clean(settings.LogoPath);
        entity.CurrencyCode = string.IsNullOrWhiteSpace(settings.CurrencyCode) ? "PKR" : settings.CurrencyCode.Trim().ToUpperInvariant();
        entity.InvoicePrefix = string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? "INV" : settings.InvoicePrefix.Trim();
        entity.ExpiryAlertDays = settings.ExpiryAlertDays;
        entity.ShowDashboardSales = settings.ShowDashboardSales;
        entity.ShowDashboardMedicines = settings.ShowDashboardMedicines;
        entity.ShowDashboardStock = settings.ShowDashboardStock;
        entity.ShowDashboardLowStock = settings.ShowDashboardLowStock;
        entity.ShowDashboardExpiry = settings.ShowDashboardExpiry;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
