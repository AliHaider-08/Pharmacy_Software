using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class InventoryService(PharmacyDbContext db, IUserSession session) : IInventoryService
{
    public async Task<IReadOnlyList<StockBatchListItem>> GetBatchesAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var batches = db.StockBatches.AsNoTracking().Include(x => x.Medicine).Include(x => x.Supplier).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            batches = batches.Where(x => x.Medicine.Name.Contains(search) || x.BatchNumber.Contains(search) || (x.Medicine.Barcode != null && x.Medicine.Barcode == search));
        }
        return await batches.OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.Medicine.Name)
            .Select(x => new StockBatchListItem(x.Id, x.MedicineId, x.Medicine.Name, x.BatchNumber, x.ExpiryDate, x.PurchasePrice, x.SalePrice, x.QuantityOnHand, x.Supplier == null ? null : x.Supplier.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovementListItem>> GetMovementsAsync(CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var movements = await (from movement in db.StockMovements.AsNoTracking()
                               join batch in db.StockBatches.AsNoTracking() on movement.BatchId equals batch.Id
                               join medicine in db.Medicines.AsNoTracking() on batch.MedicineId equals medicine.Id
                               orderby movement.OccurredAtUtc descending
                               select new { movement.OccurredAtUtc, Medicine = medicine.Name, Batch = batch.BatchNumber, movement.MovementType, movement.QuantityChange, movement.Reference, movement.Notes })
            .Take(1000).ToListAsync(cancellationToken);
        return movements.Select(x => new StockMovementListItem(x.OccurredAtUtc.ToLocalTime(), x.Medicine, x.Batch, x.MovementType, x.QuantityChange, x.Reference, x.Notes)).ToList();
    }

    public async Task AddBatchAsync(BatchInput input, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        if (input.Quantity <= 0) throw new InvalidOperationException("Opening stock must be greater than zero.");
        if (input.PurchasePrice < 0 || input.SalePrice < 0) throw new InvalidOperationException("Batch prices cannot be negative.");
        if (string.IsNullOrWhiteSpace(input.BatchNumber)) throw new InvalidOperationException("Batch number is required.");
        if (!await db.Medicines.AnyAsync(x => x.Id == input.MedicineId && x.IsActive, cancellationToken)) throw new InvalidOperationException("Select an active medicine.");
        if (await db.StockBatches.AnyAsync(x => x.MedicineId == input.MedicineId && x.BatchNumber == input.BatchNumber.Trim(), cancellationToken)) throw new InvalidOperationException("That batch number already exists for this medicine.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = new StockBatch { MedicineId = input.MedicineId, BatchNumber = input.BatchNumber.Trim(), ExpiryDate = input.ExpiryDate, PurchasePrice = input.PurchasePrice, SalePrice = input.SalePrice, QuantityOnHand = input.Quantity, SupplierId = input.SupplierId };
        db.StockBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        db.StockMovements.Add(new StockMovement { BatchId = batch.Id, OccurredAtUtc = DateTime.UtcNow, MovementType = "Opening stock", QuantityChange = input.Quantity, UserId = session.Current?.Id ?? 1, Notes = "Initial batch entry" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AdjustStockAsync(int batchId, int quantityChange, string reason, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        if (quantityChange == 0) throw new InvalidOperationException("Enter a non-zero stock adjustment.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Enter a reason for the adjustment.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = await db.StockBatches.SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken) ?? throw new InvalidOperationException("Batch no longer exists.");
        if (batch.QuantityOnHand + quantityChange < 0) throw new InvalidOperationException("Stock adjustment cannot make batch quantity negative.");
        batch.QuantityOnHand += quantityChange;
        db.StockMovements.Add(new StockMovement { BatchId = batch.Id, OccurredAtUtc = DateTime.UtcNow, MovementType = "Adjustment", QuantityChange = quantityChange, UserId = session.Current?.Id ?? 1, Notes = reason.Trim() });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
