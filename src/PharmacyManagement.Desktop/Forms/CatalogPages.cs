using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Desktop.Forms;

public sealed class MedicinesPage : DataPage
{
    private readonly ICatalogService _catalog;
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 250 };
    private readonly CheckBox _showInactive = new() { Text = "Show inactive", AutoSize = true, Margin = new Padding(4, 12, 12, 4) };

    public MedicinesPage(ICatalogService catalog) : base("Medicines", "Maintain the medicine catalog, barcode, pricing and reorder levels.")
    {
        _catalog = catalog;
        var add = ActionButton("+  Add medicine", true);
        var edit = ActionButton("Edit");
        var toggle = ActionButton("Activate / deactivate");
        Toolbar.Controls.Add(SearchBox);
        Toolbar.Controls.Add(_showInactive);
        Toolbar.Controls.Add(add);
        Toolbar.Controls.Add(edit);
        Toolbar.Controls.Add(toggle);
        add.Click += async (_, _) => await EditAsync(null);
        edit.Click += async (_, _) => await EditAsync(Selected());
        toggle.Click += async (_, _) => await ToggleAsync();
        Grid.CellDoubleClick += async (_, _) => await EditAsync(Selected());
        _showInactive.CheckedChanged += async (_, _) => await RefreshAsync();
        SearchBox.TextChanged += (_, _) => { _searchTimer.Stop(); _searchTimer.Start(); };
        _searchTimer.Tick += async (_, _) => { _searchTimer.Stop(); await RefreshAsync(); };
        Load += async (_, _) => await RefreshAsync();
    }

    private MedicineListItem? Selected() => Grid.CurrentRow?.DataBoundItem as MedicineListItem;

    private async Task RefreshAsync()
    {
        try
        {
            Grid.DataSource = await _catalog.SearchMedicinesAsync(SearchBox.Text, _showInactive.Checked);
            if (Grid.Columns.Count == 0) return;
            Grid.Columns[nameof(MedicineListItem.Id)]!.Visible = false;
            Grid.Columns[nameof(MedicineListItem.Name)]!.HeaderText = "Medicine";
            Grid.Columns[nameof(MedicineListItem.GenericName)]!.HeaderText = "Generic name";
            Grid.Columns[nameof(MedicineListItem.BrandName)]!.HeaderText = "Brand";
            Grid.Columns[nameof(MedicineListItem.CategoryName)]!.HeaderText = "Category";
            Grid.Columns[nameof(MedicineListItem.Manufacturer)]!.HeaderText = "Manufacturer";
            Grid.Columns[nameof(MedicineListItem.Barcode)]!.HeaderText = "Barcode";
            Grid.Columns[nameof(MedicineListItem.PurchasePrice)]!.HeaderText = "Cost";
            Grid.Columns[nameof(MedicineListItem.SalePrice)]!.HeaderText = "Price";
            Grid.Columns[nameof(MedicineListItem.MinimumStock)]!.HeaderText = "Min. stock";
            Grid.Columns[nameof(MedicineListItem.Stock)]!.HeaderText = "On hand";
            Grid.Columns[nameof(MedicineListItem.IsActive)]!.HeaderText = "Active";
            Grid.Columns[nameof(MedicineListItem.PurchasePrice)]!.DefaultCellStyle.Format = "N2";
            Grid.Columns[nameof(MedicineListItem.SalePrice)]!.DefaultCellStyle.Format = "N2";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task EditAsync(MedicineListItem? medicine)
    {
        try
        {
            var categories = await _catalog.GetCategoryOptionsAsync();
            using var dialog = new MedicineEditor(_catalog, categories, medicine);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await RefreshAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ToggleAsync()
    {
        if (Selected() is not { } medicine) { MessageBox.Show("Select a medicine first."); return; }
        var action = medicine.IsActive ? "deactivate" : "activate";
        if (!Confirm($"{char.ToUpper(action[0])}{action[1..]} {medicine.Name}?")) return;
        try { await _catalog.SetMedicineActiveAsync(medicine.Id, !medicine.IsActive); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

internal sealed class MedicineEditor : Form
{
    private readonly ICatalogService _catalog;
    private readonly MedicineListItem? _existing;
    private readonly TextBox _name = new();
    private readonly TextBox _generic = new();
    private readonly TextBox _brand = new();
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _manufacturer = new();
    private readonly TextBox _barcode = new();
    private readonly TextBox _description = new();
    private readonly NumericUpDown _purchase = MoneyInput();
    private readonly NumericUpDown _sale = MoneyInput();
    private readonly NumericUpDown _minimum = new() { Minimum = 0, Maximum = 1000000, ThousandsSeparator = true };
    private readonly TextBox _unit = new() { Text = "Unit" };
    private readonly TextBox _batchNumber = new();
    private readonly DateTimePicker _batchExpiry = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly NumericUpDown _openingQuantity = new() { Minimum = 0, Maximum = 10000000, ThousandsSeparator = true };

    public MedicineEditor(ICatalogService catalog, IReadOnlyList<Category> categories, MedicineListItem? existing)
    {
        _catalog = catalog;
        _existing = existing;
        Text = existing is null ? "Add medicine" : "Edit medicine";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 760);
        Font = new Font("Segoe UI", 9f);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 16, Padding = new Padding(18), AutoScroll = true };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        AddField(layout, 0, "Medicine name *", _name);
        AddField(layout, 1, "Generic name", _generic);
        AddField(layout, 2, "Brand name", _brand);
        _category.DataSource = categories.ToList();
        _category.DisplayMember = nameof(Category.Name);
        _category.ValueMember = nameof(Category.Id);
        AddField(layout, 3, "Category", _category);
        AddField(layout, 4, "Manufacturer", _manufacturer);
        AddField(layout, 5, "Barcode", _barcode);
        _description.Multiline = true;
        _description.Height = 54;
        AddField(layout, 6, "Description", _description);
        AddField(layout, 7, "Purchase price", _purchase);
        AddField(layout, 8, "Sale price", _sale);
        AddField(layout, 9, "Minimum stock", _minimum);
        AddField(layout, 10, "Unit", _unit);
        var stockHeading = new Label { Text = "Opening stock (optional)", AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = PharmacyColors.Navy, Anchor = AnchorStyles.Left };
        layout.Controls.Add(stockHeading, 0, 11); layout.SetColumnSpan(stockHeading, 2);
        AddField(layout, 12, "Initial quantity", _openingQuantity);
        AddField(layout, 13, "Batch number", _batchNumber);
        AddField(layout, 14, "Expiry date", _batchExpiry);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = DataPage.ActionButton("Save medicine", true);
        var cancel = DataPage.ActionButton("Cancel");
        save.Click += async (_, _) => await SaveAsync();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        layout.Controls.Add(actions, 1, 15);

        if (existing is not null)
        {
            _name.Text = existing.Name;
            _generic.Text = existing.GenericName ?? "";
            _brand.Text = existing.BrandName ?? "";
            _manufacturer.Text = existing.Manufacturer ?? "";
            _barcode.Text = existing.Barcode ?? "";
            _purchase.Value = Math.Clamp(existing.PurchasePrice, _purchase.Minimum, _purchase.Maximum);
            _sale.Value = Math.Clamp(existing.SalePrice, _sale.Minimum, _sale.Maximum);
            _minimum.Value = Math.Clamp(existing.MinimumStock, (int)_minimum.Minimum, (int)_minimum.Maximum);
            var match = categories.FirstOrDefault(x => x.Name == existing.CategoryName);
            if (match is null) _category.SelectedIndex = -1;
            else _category.SelectedValue = match.Id;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            var medicineInput = new MedicineInput(_existing?.Id, _name.Text, _generic.Text, _brand.Text, _category.SelectedValue is int id ? id : null, _manufacturer.Text, _barcode.Text, _description.Text, _purchase.Value, _sale.Value, (int)_minimum.Value, _unit.Text);
            await _catalog.SaveMedicineWithOpeningStockAsync(new MedicineWithOpeningStockInput(medicineInput, _batchNumber.Text, _batchExpiry.Checked ? DateOnly.FromDateTime(_batchExpiry.Value) : null, (int)_openingQuantity.Value));
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }

    private static NumericUpDown MoneyInput() => new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true, Dock = DockStyle.Left, Width = 220 };
    private static void AddField(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, input is TextBox { Multiline: true } ? 68 : 42));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = PharmacyColors.Text }, 0, row);
        input.Dock = DockStyle.Fill;
        table.Controls.Add(input, 1, row);
    }
}

public sealed class CategoriesPage : DataPage
{
    private readonly ICatalogService _catalog;

    public CategoriesPage(ICatalogService catalog) : base("Categories", "Group medicines into searchable categories.")
    {
        _catalog = catalog;
        var add = ActionButton("+  Add category", true);
        var edit = ActionButton("Edit");
        var delete = ActionButton("Delete unused");
        Toolbar.Controls.Add(SearchBox);
        Toolbar.Controls.Add(add);
        Toolbar.Controls.Add(edit);
        Toolbar.Controls.Add(delete);
        add.Click += async (_, _) => await EditAsync(null);
        edit.Click += async (_, _) => await EditAsync(Selected());
        delete.Click += async (_, _) => await DeleteAsync();
        Grid.CellDoubleClick += async (_, _) => await EditAsync(Selected());
        SearchBox.TextChanged += async (_, _) => await RefreshAsync();
        Load += async (_, _) => await RefreshAsync();
    }

    private CategoryListItem? Selected() => Grid.CurrentRow?.DataBoundItem as CategoryListItem;

    private async Task RefreshAsync()
    {
        try
        {
            var rows = await _catalog.GetCategoriesAsync();
            Grid.DataSource = rows.Where(x => string.IsNullOrWhiteSpace(SearchBox.Text) || x.Name.Contains(SearchBox.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (Grid.Columns.Count == 0) return;
            Grid.Columns[nameof(CategoryListItem.Id)]!.Visible = false;
            Grid.Columns[nameof(CategoryListItem.Name)]!.HeaderText = "Category";
            Grid.Columns[nameof(CategoryListItem.Description)]!.HeaderText = "Description";
            Grid.Columns[nameof(CategoryListItem.MedicineCount)]!.HeaderText = "Medicines";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task EditAsync(CategoryListItem? category)
    {
        var name = category?.Name ?? "";
        var description = category?.Description ?? "";
        using var form = new Form { Text = category is null ? "Add category" : "Edit category", StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, ClientSize = new Size(420, 210), MaximizeBox = false, MinimizeBox = false };
        var nameInput = new TextBox { Text = name, Dock = DockStyle.Fill };
        var descriptionInput = new TextBox { Text = description, Dock = DockStyle.Fill, Multiline = true };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Name", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        layout.Controls.Add(nameInput, 1, 0);
        layout.Controls.Add(new Label { Text = "Description", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        layout.Controls.Add(descriptionInput, 1, 1);
        var save = ActionButton("Save", true);
        save.Anchor = AnchorStyles.Right;
        save.Click += async (_, _) =>
        {
            try { await _catalog.SaveCategoryAsync(category?.Id, nameInput.Text, descriptionInput.Text); form.DialogResult = DialogResult.OK; form.Close(); }
            catch (Exception ex) { ShowError(ex); }
        };
        layout.Controls.Add(save, 1, 2);
        form.Controls.Add(layout);
        if (form.ShowDialog(FindForm()) == DialogResult.OK) await RefreshAsync();
    }

    private async Task DeleteAsync()
    {
        if (Selected() is not { } item) { MessageBox.Show("Select a category first."); return; }
        if (!Confirm($"Delete category '{item.Name}'?")) return;
        try { await _catalog.DeleteCategoryAsync(item.Id); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}
