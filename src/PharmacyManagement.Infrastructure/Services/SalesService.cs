using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class SalesService(PharmacyDbContext db, IUserSession session) : ISalesService
{
    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (session.Current is null) throw new UnauthorizedAccessException("Sign in before creating a sale.");
        var quantities = request.Lines.Where(x => x.Quantity > 0).GroupBy(x => x.MedicineId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        if (quantities.Count == 0) throw new InvalidOperationException("Add at least one medicine to the cart.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var allocations = new List<(StockBatch Batch, int Quantity)>();
        foreach (var (medicineId, requestedQuantity) in quantities)
        {
            var medicine = await db.Medicines.SingleOrDefaultAsync(x => x.Id == medicineId && x.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("A selected medicine is no longer active.");
            var eligibleBatches = await db.StockBatches.Where(x => x.MedicineId == medicineId && x.QuantityOnHand > 0 && (x.ExpiryDate == null || x.ExpiryDate >= today))
                .OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);
            var available = eligibleBatches.Sum(x => x.QuantityOnHand);
            if (available < requestedQuantity) throw new InvalidOperationException($"Not enough unexpired stock for {medicine.Name}. Available: {available}.");
            var remaining = requestedQuantity;
            foreach (var batch in eligibleBatches)
            {
                var take = Math.Min(remaining, batch.QuantityOnHand);
                if (take > 0) allocations.Add((batch, take));
                remaining -= take;
                if (remaining == 0) break;
            }
        }

        var subtotal = allocations.Sum(x => x.Batch.SalePrice * x.Quantity);
        if (request.Discount < 0 || request.Discount > subtotal) throw new InvalidOperationException("Discount must be between zero and the subtotal.");
        var discount = decimal.Round(request.Discount, 2, MidpointRounding.AwayFromZero);
        var total = subtotal - discount;
        if (request.PaidAmount < 0) throw new InvalidOperationException("Paid amount cannot be negative.");
        if (request.CustomerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == request.CustomerId.Value, cancellationToken)) throw new InvalidOperationException("Selected customer no longer exists.");

        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var invoicePrefix = string.IsNullOrWhiteSpace(settings?.InvoicePrefix) ? "INV" : settings.InvoicePrefix.Trim();
        var invoice = $"{invoicePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var actorId = session.Current?.Id ?? request.UserId;
        var sale = new Sale { InvoiceNumber = invoice, SoldAtUtc = DateTime.UtcNow, CustomerId = request.CustomerId, UserId = actorId, Subtotal = subtotal, Discount = discount, GrandTotal = total, PaidAmount = Math.Min(request.PaidAmount, total), PaymentMethod = request.PaymentMethod };
        decimal assignedDiscount = 0;
        for (var index = 0; index < allocations.Count; index++)
        {
            var (batch, quantity) = allocations[index];
            var lineAmount = batch.SalePrice * quantity;
            var proportionalDiscount = subtotal == 0 ? 0 : decimal.Round(discount * lineAmount / subtotal, 2, MidpointRounding.AwayFromZero);
            var lineDiscount = index == allocations.Count - 1 ? discount - assignedDiscount : Math.Min(discount - assignedDiscount, proportionalDiscount);
            assignedDiscount += lineDiscount;
            batch.QuantityOnHand -= quantity;
            sale.Lines.Add(new SaleLine { Batch = batch, Quantity = quantity, UnitPrice = batch.SalePrice, UnitCost = batch.PurchasePrice, Discount = lineDiscount });
            db.StockMovements.Add(new StockMovement { BatchId = batch.Id, OccurredAtUtc = DateTime.UtcNow, MovementType = "Sale", QuantityChange = -quantity, Reference = invoice, UserId = actorId });
        }
        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var receiptLines = sale.Lines.Select(line => new CheckoutLine(
            line.Batch.Medicine.Name,
            line.Quantity,
            line.UnitPrice,
            line.Discount,
            line.UnitPrice * line.Quantity - line.Discount)).ToList();
        return new CheckoutResult(invoice, sale.SoldAtUtc.ToLocalTime(), subtotal, discount, total, sale.PaidAmount, Math.Max(0, request.PaidAmount - total), sale.PaymentMethod, receiptLines);
    }

    public async Task<IReadOnlyList<SaleListItem>> GetRecentSalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await db.Sales.AsNoTracking().OrderByDescending(x => x.SoldAtUtc).Take(100).ToListAsync(cancellationToken);
        return sales.Select(x => new SaleListItem(x.InvoiceNumber, x.SoldAtUtc.ToLocalTime(), x.GrandTotal, x.PaidAmount, x.PaymentMethod)).ToList();
    }

    public async Task<CheckoutResult> GetReceiptAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var sale = await db.Sales.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Batch).ThenInclude(x => x.Medicine)
            .SingleOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber, cancellationToken)
            ?? throw new InvalidOperationException("Sale receipt was not found.");
        var lines = sale.Lines.Select(line => new CheckoutLine(line.Batch.Medicine.Name, line.Quantity, line.UnitPrice, line.Discount, line.UnitPrice * line.Quantity - line.Discount)).ToList();
        return new CheckoutResult(sale.InvoiceNumber, sale.SoldAtUtc.ToLocalTime(), sale.Subtotal, sale.Discount, sale.GrandTotal, sale.PaidAmount, Math.Max(0, sale.PaidAmount - sale.GrandTotal), sale.PaymentMethod, lines);
    }
}
