using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class CatalogService(PharmacyDbContext db, IUserSession session) : ICatalogService
{
    public async Task<IReadOnlyList<MedicineListItem>> SearchMedicinesAsync(string? query = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var search = query?.Trim();
        var medicines = db.Medicines.AsNoTracking().Include(x => x.Category).AsQueryable();
        if (!includeInactive) medicines = medicines.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            medicines = medicines.Where(x => x.Name.Contains(search) || (x.GenericName != null && x.GenericName.Contains(search)) || (x.Barcode != null && x.Barcode == search));

        return await medicines.OrderBy(x => x.Name).Select(x => new MedicineListItem(
            x.Id, x.Name, x.GenericName, x.BrandName, x.Category!.Name, x.Manufacturer, x.Barcode,
            x.DefaultPurchasePrice, x.DefaultSalePrice, x.MinimumStockLevel,
            x.Batches.Sum(b => (int?)b.QuantityOnHand) ?? 0, x.IsActive)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryListItem>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await db.Categories.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new CategoryListItem(x.Id, x.Name, x.Description, x.Medicines.Count)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Category>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default) =>
        await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task SaveMedicineAsync(MedicineInput input, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var name = input.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Medicine name is required.");
        if (input.PurchasePrice < 0 || input.SalePrice < 0 || input.MinimumStock < 0) throw new InvalidOperationException("Prices and minimum stock cannot be negative.");
        if (input.CategoryId is not null && !await db.Categories.AnyAsync(x => x.Id == input.CategoryId, cancellationToken)) throw new InvalidOperationException("Select an existing category.");

        var barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
        if (barcode is not null && await db.Medicines.AnyAsync(x => x.Barcode == barcode && x.Id != input.Id, cancellationToken))
            throw new InvalidOperationException("That barcode is already assigned to another medicine.");

        Medicine medicine;
        if (input.Id.HasValue)
        {
            medicine = await db.Medicines.SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken)
                ?? throw new InvalidOperationException("Medicine no longer exists.");
        }
        else
        {
            medicine = new Medicine();
            db.Medicines.Add(medicine);
        }

        medicine.Name = name;
        medicine.GenericName = Clean(input.GenericName);
        medicine.BrandName = Clean(input.BrandName);
        medicine.CategoryId = input.CategoryId;
        medicine.Manufacturer = Clean(input.Manufacturer);
        medicine.Barcode = barcode;
        medicine.Description = Clean(input.Description);
        medicine.DefaultPurchasePrice = input.PurchasePrice;
        medicine.DefaultSalePrice = input.SalePrice;
        medicine.MinimumStockLevel = input.MinimumStock;
        medicine.Unit = string.IsNullOrWhiteSpace(input.Unit) ? "Unit" : input.Unit.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetMedicineActiveAsync(int medicineId, bool isActive, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var medicine = await db.Medicines.SingleOrDefaultAsync(x => x.Id == medicineId, cancellationToken)
            ?? throw new InvalidOperationException("Medicine no longer exists.");
        medicine.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveCategoryAsync(int? id, string name, string? description, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Category name is required.");
        if (await db.Categories.AnyAsync(x => x.Name == name && x.Id != id, cancellationToken)) throw new InvalidOperationException("A category with that name already exists.");
        Category category;
        if (id.HasValue) category = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("Category no longer exists.");
        else { category = new Category(); db.Categories.Add(category); }
        category.Name = name;
        category.Description = Clean(description);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireManager(session);
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new InvalidOperationException("Category no longer exists.");
        if (await db.Medicines.AnyAsync(x => x.CategoryId == id, cancellationToken)) throw new InvalidOperationException("This category is in use. Move its medicines before deleting it.");
        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
