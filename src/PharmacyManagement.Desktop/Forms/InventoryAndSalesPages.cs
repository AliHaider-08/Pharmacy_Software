using System.ComponentModel;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Desktop.Forms;

public sealed class InventoryPage : DataPage
{
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;

    public InventoryPage(ICatalogService catalog, IInventoryService inventory) : base("Inventory & batches", "Receive stock by batch, review expiry dates and record adjustments.")
    {
        _catalog = catalog;
        _inventory = inventory;
        var add = ActionButton("+  Receive batch", true);
        var adjust = ActionButton("Adjust stock");
        var history = ActionButton("Movement history");
        Toolbar.Controls.Add(SearchBox);
        Toolbar.Controls.Add(add);
        Toolbar.Controls.Add(adjust);
        Toolbar.Controls.Add(history);
        add.Click += async (_, _) => await AddBatchAsync();
        adjust.Click += async (_, _) => await AdjustAsync();
        history.Click += async (_, _) => await ShowHistoryAsync();
        SearchBox.TextChanged += async (_, _) => await RefreshAsync();
        Load += async (_, _) => await RefreshAsync();
    }

    private StockBatchListItem? Selected() => Grid.CurrentRow?.DataBoundItem as StockBatchListItem;

    private async Task RefreshAsync()
    {
        try
        {
            Grid.DataSource = await _inventory.GetBatchesAsync(SearchBox.Text);
            if (Grid.Columns.Count == 0) return;
            Grid.Columns[nameof(StockBatchListItem.Id)]!.Visible = false;
            Grid.Columns[nameof(StockBatchListItem.MedicineId)]!.Visible = false;
            Grid.Columns[nameof(StockBatchListItem.MedicineName)]!.HeaderText = "Medicine";
            Grid.Columns[nameof(StockBatchListItem.BatchNumber)]!.HeaderText = "Batch";
            Grid.Columns[nameof(StockBatchListItem.ExpiryDate)]!.HeaderText = "Expiry";
            Grid.Columns[nameof(StockBatchListItem.PurchasePrice)]!.HeaderText = "Cost";
            Grid.Columns[nameof(StockBatchListItem.SalePrice)]!.HeaderText = "Sale price";
            Grid.Columns[nameof(StockBatchListItem.Quantity)]!.HeaderText = "On hand";
            Grid.Columns[nameof(StockBatchListItem.SupplierName)]!.HeaderText = "Supplier";
            Grid.Columns[nameof(StockBatchListItem.PurchasePrice)]!.DefaultCellStyle.Format = "N2";
            Grid.Columns[nameof(StockBatchListItem.SalePrice)]!.DefaultCellStyle.Format = "N2";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task AddBatchAsync()
    {
        try
        {
            using var editor = new BatchEditor(await _catalog.SearchMedicinesAsync());
            if (editor.ShowDialog(FindForm()) != DialogResult.OK || editor.Result is null) return;
            await _inventory.AddBatchAsync(editor.Result);
            await RefreshAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task AdjustAsync()
    {
        if (Selected() is not { } batch) { MessageBox.Show("Select a batch first."); return; }
        using var dialog = new AdjustmentDialog(batch);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try { await _inventory.AdjustStockAsync(batch.Id, dialog.QuantityChange, dialog.Reason); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ShowHistoryAsync()
    {
        try
        {
            var rows = await _inventory.GetMovementsAsync();
            using var form = new Form { Text = "Stock movement history · latest 1,000", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(980, 520) };
            var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = rows, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = column.Name switch { nameof(StockMovementListItem.DateLocal) => "Date", nameof(StockMovementListItem.QuantityChange) => "Quantity change", _ => column.Name };
            form.Controls.Add(grid);
            form.ShowDialog(FindForm());
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

internal sealed class BatchEditor : Form
{
    private readonly ComboBox _medicine = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _batch = new();
    private readonly DateTimePicker _expiry = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly NumericUpDown _purchase = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly NumericUpDown _sale = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly NumericUpDown _quantity = new() { Minimum = 1, Maximum = 10000000, ThousandsSeparator = true, Value = 1 };
    public BatchInput? Result { get; private set; }

    public BatchEditor(IReadOnlyList<MedicineListItem> medicines)
    {
        Text = "Receive stock batch";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(500, 310);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);
        _medicine.DataSource = medicines.ToList();
        _medicine.DisplayMember = nameof(MedicineListItem.Name);
        _medicine.ValueMember = nameof(MedicineListItem.Id);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(18) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        AddRow(layout, 0, "Medicine", _medicine);
        AddRow(layout, 1, "Batch number", _batch);
        AddRow(layout, 2, "Expiry date", _expiry);
        AddRow(layout, 3, "Purchase price", _purchase);
        AddRow(layout, 4, "Sale price", _sale);
        AddRow(layout, 5, "Opening quantity", _quantity);
        var save = DataPage.ActionButton("Receive stock", true);
        save.Click += (_, _) =>
        {
            if (_medicine.SelectedValue is not int medicineId) { MessageBox.Show("Add a medicine before receiving stock."); return; }
            Result = new BatchInput(medicineId, _batch.Text, _expiry.Checked ? DateOnly.FromDateTime(_expiry.Value) : null, _purchase.Value, _sale.Value, (int)_quantity.Value, null);
            DialogResult = DialogResult.OK;
            Close();
        };
        layout.Controls.Add(save, 1, 6);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }
}

internal sealed class AdjustmentDialog : Form
{
    private readonly NumericUpDown _change = new() { Minimum = -10000000, Maximum = 10000000, DecimalPlaces = 0, ThousandsSeparator = true };
    private readonly TextBox _reason = new() { Dock = DockStyle.Fill };
    public int QuantityChange => (int)_change.Value;
    public string Reason => _reason.Text;

    public AdjustmentDialog(StockBatchListItem batch)
    {
        Text = "Adjust batch stock";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(480, 210);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(18) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = $"{batch.MedicineName} · {batch.BatchNumber} · on hand {batch.Quantity}", AutoSize = true }, 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 2);
        layout.Controls.Add(new Label { Text = "Change (+ / −)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_change, 1, 1);
        layout.Controls.Add(new Label { Text = "Reason", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_reason, 1, 2);
        var save = DataPage.ActionButton("Save adjustment", true);
        save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        layout.Controls.Add(save, 1, 3);
    }
}

public sealed class PointOfSalePage : DataPage
{
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly ISalesService _sales;
    private readonly IBackOfficeService _settings;
    private readonly BindingList<CartRow> _cart = new();
    private IReadOnlyList<StockBatchListItem> _batches = [];
    private string _currencyCode = "PKR";
    private readonly Label _total = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold), ForeColor = PharmacyColors.Navy, Margin = new Padding(12, 7, 12, 4) };

    public PointOfSalePage(ICatalogService catalog, IInventoryService inventory, ISalesService sales, IBackOfficeService settings) : base("Sales / POS", "Scan a barcode or search by medicine name, then complete the bill.")
    {
        _catalog = catalog;
        _inventory = inventory;
        _sales = sales;
        _settings = settings;
        SearchBox.PlaceholderText = "Scan barcode or search medicine…";
        var add = ActionButton("Add to cart", true);
        var recent = ActionButton("Recent sales");
        var checkout = ActionButton("Checkout");
        Toolbar.Controls.Add(SearchBox);
        Toolbar.Controls.Add(add);
        Toolbar.Controls.Add(_total);
        Toolbar.Controls.Add(recent);
        Toolbar.Controls.Add(checkout);
        add.Click += async (_, _) => await AddFromSearchAsync();
        SearchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AddFromSearchAsync(); } };
        recent.Click += async (_, _) => await ShowRecentSalesAsync();
        checkout.Click += async (_, _) => await CheckoutAsync();
        Grid.AutoGenerateColumns = false;
        Grid.AllowUserToDeleteRows = true;
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Name), DataPropertyName = nameof(CartRow.Name), HeaderText = "Medicine", ReadOnly = true, FillWeight = 55 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Quantity), DataPropertyName = nameof(CartRow.Quantity), HeaderText = "Quantity", ReadOnly = false, FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Available), DataPropertyName = nameof(CartRow.Available), HeaderText = "Available", ReadOnly = true, FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Amount), DataPropertyName = nameof(CartRow.Amount), HeaderText = "Amount", ReadOnly = true, FillWeight = 25, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        Grid.DataSource = _cart;
        Grid.CellEndEdit += (_, e) => { if (e.RowIndex >= 0) UpdateCartRow(e.RowIndex); };
        Grid.UserDeletedRow += (_, _) => UpdateTotal();
        Load += async (_, _) => { try { _currencyCode = (await _settings.GetSettingsAsync()).CurrencyCode; Grid.Columns[nameof(CartRow.Amount)]!.HeaderText = $"Amount ({_currencyCode})"; UpdateTotal(); } catch (Exception ex) { ShowError(ex); } await ReloadBatchesAsync(); };
        UpdateTotal();
    }

    private async Task ReloadBatchesAsync()
    {
        try { _batches = await _inventory.GetBatchesAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task AddFromSearchAsync()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        try
        {
            var matches = await _catalog.SearchMedicinesAsync(query);
            if (matches.Count == 0) { MessageBox.Show("No active medicine matched that name or barcode."); return; }
            MedicineListItem? medicine;
            var exactBarcode = matches.FirstOrDefault(x => string.Equals(x.Barcode, query, StringComparison.OrdinalIgnoreCase));
            if (exactBarcode is not null) medicine = exactBarcode;
            else if (matches.Count == 1) medicine = matches[0];
            else medicine = ChooseMedicine(matches);
            if (medicine is null) return;
            await ReloadBatchesAsync();
            var available = EligibleBatches(medicine.Id).Sum(x => x.Quantity);
            if (available == 0) { MessageBox.Show("This medicine has no unexpired stock available."); return; }
            var existing = _cart.FirstOrDefault(x => x.MedicineId == medicine.Id);
            if (existing is null)
            {
                var row = new CartRow(medicine.Id, medicine.Name, 1, available);
                row.Amount = Estimate(row);
                _cart.Add(row);
            }
            else
            {
                if (existing.Quantity + 1 > available) { MessageBox.Show($"Only {available} unexpired units are available."); return; }
                existing.Quantity++;
                existing.Amount = Estimate(existing);
                var index = _cart.IndexOf(existing);
                _cart.ResetItem(index);
            }
            SearchBox.Clear();
            UpdateTotal();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private MedicineListItem? ChooseMedicine(IReadOnlyList<MedicineListItem> medicines)
    {
        using var form = new Form { Text = "Choose medicine", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(680, 360) };
        var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = medicines.ToList(), ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
        var choose = ActionButton("Add selected", true);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        choose.Click += (_, _) => { if (grid.CurrentRow?.DataBoundItem is MedicineListItem) form.DialogResult = DialogResult.OK; };
        footer.Controls.Add(choose);
        form.Controls.Add(grid);
        form.Controls.Add(footer);
        return form.ShowDialog(FindForm()) == DialogResult.OK ? grid.CurrentRow?.DataBoundItem as MedicineListItem : null;
    }

    private IEnumerable<StockBatchListItem> EligibleBatches(int medicineId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return _batches.Where(x => x.MedicineId == medicineId && x.Quantity > 0 && (x.ExpiryDate is null || x.ExpiryDate >= today))
            .OrderBy(x => x.ExpiryDate is null).ThenBy(x => x.ExpiryDate);
    }

    private decimal Estimate(CartRow row)
    {
        var remaining = row.Quantity;
        decimal amount = 0;
        foreach (var batch in EligibleBatches(row.MedicineId))
        {
            var take = Math.Min(remaining, batch.Quantity);
            amount += take * batch.SalePrice;
            remaining -= take;
            if (remaining <= 0) break;
        }
        return remaining > 0 ? 0 : amount;
    }

    private void UpdateCartRow(int rowIndex)
    {
        if (rowIndex >= _cart.Count) return;
        var row = _cart[rowIndex];
        var cellValue = Grid.Rows[rowIndex].Cells[nameof(CartRow.Quantity)].Value;
        if (!int.TryParse(Convert.ToString(cellValue), out var quantity) || quantity <= 0 || quantity > row.Available)
        {
            MessageBox.Show($"Quantity must be between 1 and {row.Available}.");
            Grid.Rows[rowIndex].Cells[nameof(CartRow.Quantity)].Value = row.Quantity;
            return;
        }
        row.Quantity = quantity;
        row.Amount = Estimate(row);
        _cart.ResetItem(rowIndex);
        UpdateTotal();
    }

    private decimal Subtotal() => _cart.Sum(Estimate);

    private void UpdateTotal() => _total.Text = $"Subtotal  {_currencyCode} {Subtotal():N2}";

    private async Task CheckoutAsync()
    {
        if (_cart.Count == 0) { MessageBox.Show("Cart is empty."); return; }
        var subtotal = Subtotal();
        if (subtotal <= 0) { MessageBox.Show("The cart has no valid stock available."); return; }
        using var payment = new PaymentDialog(subtotal, _currencyCode);
        if (payment.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            var result = await _sales.CheckoutAsync(new CheckoutRequest(_cart.Select(x => new CartLineRequest(x.MedicineId, x.Quantity)).ToList(), payment.Discount, payment.Paid, payment.Method));
            MessageBox.Show($"Sale completed successfully.\n\nInvoice: {result.InvoiceNumber}\nSubtotal: {_currencyCode} {result.Subtotal:N2}\nDiscount: {_currencyCode} {result.Discount:N2}\nTotal: {_currencyCode} {result.Total:N2}\nPaid: {_currencyCode} {result.Paid:N2}\nChange: {_currencyCode} {result.Change:N2}", "Sale complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _cart.Clear();
            await ReloadBatchesAsync();
            UpdateTotal();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ShowRecentSalesAsync()
    {
        try
        {
            var rows = await _sales.GetRecentSalesAsync();
            using var form = new Form { Text = "Recent sales", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(760, 430) };
            var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = rows, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
            form.Controls.Add(grid);
            form.ShowDialog(FindForm());
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private sealed class CartRow(int medicineId, string name, int quantity, int available)
    {
        public int MedicineId { get; } = medicineId;
        public string Name { get; } = name;
        public int Available { get; } = available;
        public int Quantity { get; set; } = quantity;
        public decimal Amount { get; set; }
    }
}

internal sealed class PaymentDialog : Form
{
    private readonly NumericUpDown _discount = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly NumericUpDown _paid = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly decimal _subtotal;
    private readonly string _currencyCode;
    private readonly Label _totalLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), ForeColor = PharmacyColors.Navy };
    public decimal Discount => _discount.Value;
    public decimal Paid => _paid.Value;
    public string Method => Convert.ToString(_method.SelectedItem) ?? "Cash";

    public PaymentDialog(decimal subtotal, string currencyCode)
    {
        _subtotal = subtotal;
        _currencyCode = currencyCode;
        Text = "Take payment";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(450, 280);
        MaximizeBox = false;
        MinimizeBox = false;
        _discount.Maximum = subtotal;
        _paid.Value = subtotal;
        _method.Items.AddRange(["Cash", "Card", "Bank transfer", "Credit"]);
        _method.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(20) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        AddRow(layout, 0, "Subtotal", new Label { Text = $"{currencyCode} {subtotal:N2}", AutoSize = true });
        AddRow(layout, 1, "Discount", _discount);
        layout.Controls.Add(new Label { Text = "Total", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_totalLabel, 1, 2);
        AddRow(layout, 3, "Paid amount", _paid);
        AddRow(layout, 4, "Payment method", _method);
        var confirm = DataPage.ActionButton("Complete sale", true);
        confirm.Anchor = AnchorStyles.Right;
        confirm.Click += (_, _) => { if (_paid.Value > _subtotal - _discount.Value && _method.Text == "Credit") { MessageBox.Show("For credit sales, paid amount cannot exceed the total."); return; } DialogResult = DialogResult.OK; Close(); };
        layout.Controls.Add(confirm, 1, 5);
        _discount.ValueChanged += (_, _) => RefreshTotal();
        _method.SelectedValueChanged += (_, _) => { if (_method.Text == "Credit") _paid.Value = 0; else if (_paid.Value == 0) _paid.Value = Math.Max(0, _subtotal - _discount.Value); };
        RefreshTotal();
    }

    private void RefreshTotal() => _totalLabel.Text = $"{_currencyCode} {_subtotal - _discount.Value:N2}";
    private static void AddRow(TableLayoutPanel layout, int row, string title, Control control)
    {
        layout.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }
}
