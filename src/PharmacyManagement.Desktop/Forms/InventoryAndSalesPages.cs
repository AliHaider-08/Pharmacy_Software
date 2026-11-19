using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Printing;
using System.Text;
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
    private IReadOnlyList<MedicineListItem> _availableMedicines = [];
    private readonly ListBox _products = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, Font = new Font("Segoe UI", 10f), ItemHeight = 42 };
    private readonly Dictionary<int, string> _productLabels = new();
    private readonly Panel _cartPanel = new() { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };
    private readonly Label _cartHeading = new() { Text = "Current bill", Dock = DockStyle.Top, Height = 38, Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), ForeColor = PharmacyColors.Text };
    private readonly Label _cartHint = new() { Text = "Choose a medicine to start a sale", Dock = DockStyle.Bottom, Height = 30, Font = new Font("Segoe UI", 9f), ForeColor = PharmacyColors.Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly SplitContainer _workspace = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = Color.White, Panel1MinSize = 0, Panel2MinSize = 0 };
    private string _currencyCode = "PKR";
    private readonly Label _total = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold), ForeColor = PharmacyColors.Navy, Margin = new Padding(12, 7, 12, 4) };

    public PointOfSalePage(ICatalogService catalog, IInventoryService inventory, ISalesService sales, IBackOfficeService settings) : base("Sales / POS", "Select an in-stock medicine or scan a barcode, then complete the bill.")
    {
        _catalog = catalog;
        _inventory = inventory;
        _sales = sales;
        _settings = settings;
        SearchBox.PlaceholderText = "Scan barcode or search medicine…";
        var recent = ActionButton("Recent sales");
        var checkout = ActionButton("Generate bill  →", true);
        checkout.Height = 40;
        SearchBox.Width = 370;
        SearchBox.Margin = new Padding(0, 6, 10, 4);
        Toolbar.Controls.Add(SearchBox);
        Toolbar.Controls.Add(_total);
        Toolbar.Controls.Add(recent);
        Toolbar.Controls.Add(checkout);
        SearchBox.TextChanged += (_, _) => FilterProducts();
        SearchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await AddFromSearchAsync(); } };
        recent.Click += async (_, _) => await ShowRecentSalesAsync();
        checkout.Click += async (_, _) => await CheckoutAsync();
        Grid.AutoGenerateColumns = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.ReadOnly = true;
        Grid.RowTemplate.Height = 42;
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Name), DataPropertyName = nameof(CartRow.Name), HeaderText = "Medicine", ReadOnly = true, FillWeight = 55 });
        Grid.Columns.Add(new DataGridViewButtonColumn { Name = "Decrease", HeaderText = "", Text = "−", UseColumnTextForButtonValue = true, FillWeight = 9, FlatStyle = FlatStyle.Flat });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Quantity), DataPropertyName = nameof(CartRow.Quantity), HeaderText = "Qty", ReadOnly = true, FillWeight = 9 });
        Grid.Columns.Add(new DataGridViewButtonColumn { Name = "Increase", HeaderText = "", Text = "+", UseColumnTextForButtonValue = true, FillWeight = 9, FlatStyle = FlatStyle.Flat });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Available), DataPropertyName = nameof(CartRow.Available), HeaderText = "Available", ReadOnly = true, FillWeight = 15 });
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Amount), DataPropertyName = nameof(CartRow.Amount), HeaderText = "Line total", ReadOnly = true, FillWeight = 25, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        Grid.DataSource = _cart;
        Grid.CellContentClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0) AdjustQuantity(e.RowIndex, Grid.Columns[e.ColumnIndex].Name == "Increase" ? 1 : -1); };
        _products.Format += (_, e) => { if (e.ListItem is MedicineListItem medicine && _productLabels.TryGetValue(medicine.Id, out var label)) e.Value = label; };
        _products.Click += async (_, _) => { if (_products.SelectedItem is MedicineListItem item) await AddMedicineToCartAsync(item); };
        _products.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && _products.SelectedItem is MedicineListItem item) { e.SuppressKeyPress = true; await AddMedicineToCartAsync(item); } };
        var productPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
        productPanel.Controls.Add(_products);
        productPanel.Controls.Add(new Label { Text = "IN STOCK · SELECT TO ADD", Dock = DockStyle.Top, Height = 34, ForeColor = PharmacyColors.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        _cartPanel.Controls.Add(Grid);
        _cartPanel.Controls.Add(_cartHint);
        _cartPanel.Controls.Add(_cartHeading);
        _workspace.Panel1.Controls.Add(_cartPanel);
        _workspace.Panel2.Controls.Add(productPanel);
        var posLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4, 2, 4, 4), BackColor = PharmacyColors.Background };
        posLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        posLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        posLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Clear();
        Controls.Add(posLayout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "Sales / POS", AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text });
        heading.Controls.Add(new Label { Text = "Select an in-stock medicine or scan a barcode, then complete the bill.", AutoSize = true, Location = new Point(2, 44), Font = new Font("Segoe UI", 9f), ForeColor = PharmacyColors.Muted });
        posLayout.Controls.Add(heading, 0, 0);
        posLayout.Controls.Add(Toolbar, 0, 1);
        posLayout.Controls.Add(_workspace, 0, 2);
        _workspace.SizeChanged += (_, _) => SetWorkspaceSplit();
        Load += async (_, _) => { try { _currencyCode = (await Task.Run(() => _settings.GetSettingsAsync())).CurrencyCode; Grid.Columns[nameof(CartRow.Amount)]!.HeaderText = $"Line total ({_currencyCode})"; UpdateTotal(); } catch (Exception ex) { ShowError(ex); } await ReloadBatchesAsync(); await RefreshProductsAsync(); };
        UpdateTotal();
    }

    private void SetWorkspaceSplit()
    {
        if (_workspace.Width <= 0 || _workspace.IsDisposed) return;
        var usableWidth = Math.Max(1, _workspace.Width - _workspace.SplitterWidth);
        var desired = Math.Clamp((int)(usableWidth * .58), 280, Math.Max(280, usableWidth - 240));
        desired = Math.Min(desired, usableWidth);
        try { _workspace.SplitterDistance = desired; }
        catch (InvalidOperationException) { /* WinForms may resize the parent between layout events; SizeChanged retries with the final width. */ }
    }

    private async Task ReloadBatchesAsync()
    {
        try { _batches = await Task.Run(() => _inventory.GetBatchesAsync()); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task RefreshProductsAsync()
    {
        try
        {
            var medicines = await Task.Run(() => _catalog.SearchMedicinesAsync());
            var stockedIds = _batches.Where(x => x.Quantity > 0 && (x.ExpiryDate is null || x.ExpiryDate >= DateOnly.FromDateTime(DateTime.Today))).Select(x => x.MedicineId).ToHashSet();
            var available = medicines.Where(x => stockedIds.Contains(x.Id)).ToList();
            _productLabels.Clear();
            foreach (var medicine in available)
            {
                var batches = EligibleBatches(medicine.Id).ToList();
                var quantity = batches.Sum(x => x.Quantity);
                var price = batches.FirstOrDefault()?.SalePrice ?? medicine.SalePrice;
                _productLabels[medicine.Id] = $"{medicine.Name}  ·  {quantity} in stock  ·  {_currencyCode} {price:N2}";
            }
            _availableMedicines = available;
            FilterProducts();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void FilterProducts()
    {
        var query = SearchBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(query) ? _availableMedicines : _availableMedicines.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || (x.Barcode?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        var prior = _products.SelectedItem as MedicineListItem;
        _products.DataSource = null;
        _products.DisplayMember = nameof(MedicineListItem.Name);
        _products.DataSource = filtered;
        if (prior is not null) _products.SelectedItem = filtered.FirstOrDefault(x => x.Id == prior.Id);
        if (filtered.Count == 1 && !string.IsNullOrWhiteSpace(query)) _products.SelectedIndex = 0;
    }

    private async Task AddFromSearchAsync()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            if (_products.SelectedItem is MedicineListItem selected)
            {
                await AddMedicineToCartAsync(selected);
                return;
            }
            MessageBox.Show("Select a medicine from the IN STOCK list or scan/search its barcode or name.", "Choose a medicine", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _products.Focus();
            return;
        }
        try
        {
            var matches = await Task.Run(() => _catalog.SearchMedicinesAsync(query));
            if (matches.Count == 0) { MessageBox.Show("No active medicine matched that name or barcode."); return; }
            MedicineListItem? medicine;
            var exactBarcode = matches.FirstOrDefault(x => string.Equals(x.Barcode, query, StringComparison.OrdinalIgnoreCase));
            if (exactBarcode is not null) medicine = exactBarcode;
            else if (matches.Count == 1) medicine = matches[0];
            else medicine = ChooseMedicine(matches);
            if (medicine is null) return;
            if (!_batches.Any(x => x.MedicineId == medicine.Id && x.Quantity > 0 && (x.ExpiryDate is null || x.ExpiryDate >= DateOnly.FromDateTime(DateTime.Today))))
            { MessageBox.Show($"{medicine.Name} has no unexpired stock."); return; }
            await ReloadBatchesAsync();
            await AddMedicineToCartAsync(medicine);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task AddMedicineToCartAsync(MedicineListItem medicine)
    {
        try
        {
            if (_batches.Count == 0) await ReloadBatchesAsync();
            var available = EligibleBatches(medicine.Id).Sum(x => x.Quantity);
            if (available == 0) { MessageBox.Show($"{medicine.Name} has no unexpired stock. Receive a batch in Inventory before selling it."); return; }
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
            _cartHeading.Text = $"Current bill · {_cart.Count} item{(_cart.Count == 1 ? "" : "s")}";
            _cartHint.Text = _cart.Count == 0 ? "Choose a medicine to start a sale" : "Adjust quantity, then generate the bill";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private MedicineListItem? ChooseMedicine(IReadOnlyList<MedicineListItem> medicines)
    {
        using var form = new Form { Text = "Choose medicine", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(680, 360) };
        var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = medicines.ToList(), ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
        if (grid.Columns[nameof(MedicineListItem.Id)] is { } id) id.Visible = false;
        if (grid.Columns[nameof(MedicineListItem.Name)] is { } name) name.HeaderText = "Medicine";
        if (grid.Columns[nameof(MedicineListItem.Barcode)] is { } barcode) barcode.HeaderText = "Barcode";
        if (grid.Columns[nameof(MedicineListItem.SalePrice)] is { } price) { price.HeaderText = "Catalog price"; price.DefaultCellStyle.Format = "N2"; }
        if (grid.Columns[nameof(MedicineListItem.Stock)] is { } stock) stock.HeaderText = "On hand";
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
        return remaining > 0 ? 0 : decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private void AdjustQuantity(int rowIndex, int change)
    {
        if (rowIndex < 0 || rowIndex >= _cart.Count) return;
        var row = _cart[rowIndex];
        row.Available = EligibleBatches(row.MedicineId).Sum(x => x.Quantity);
        var quantity = row.Quantity + change;
        if (quantity <= 0) { _cart.RemoveAt(rowIndex); }
        else if (quantity > row.Available) { MessageBox.Show($"Only {row.Available} unexpired units are available."); return; }
        else { row.Quantity = quantity; row.Amount = Estimate(row); _cart.ResetItem(rowIndex); }
        UpdateTotal();
        _cartHeading.Text = $"Current bill · {_cart.Count} item{(_cart.Count == 1 ? "" : "s")}";
        _cartHint.Text = _cart.Count == 0 ? "Choose a medicine to start a sale" : "Adjust quantity, then generate the bill";
    }

    private decimal Subtotal() => _cart.Sum(x => x.Amount);

    private void UpdateTotal() => _total.Text = $"Subtotal  {_currencyCode} {Subtotal():N2}";

    private async Task CheckoutAsync()
    {
        if (_cart.Count == 0) { MessageBox.Show("Cart is empty."); return; }
        var subtotal = Subtotal();
        if (subtotal <= 0) { MessageBox.Show("The cart has no valid stock available."); return; }
        using var payment = new PaymentDialog(subtotal, _currencyCode);
        if (payment.ShowDialog(FindForm()) != DialogResult.OK) return;
        Enabled = false;
        try
        {
            Grid.EndEdit();
            foreach (var cartRow in _cart)
                if (cartRow.Quantity < 1 || cartRow.Quantity > cartRow.Available)
                {
                    MessageBox.Show($"Quantity for {cartRow.Name} must be between 1 and {cartRow.Available}.");
                    return;
                }
            var request = new CheckoutRequest(_cart.Select(x => new CartLineRequest(x.MedicineId, x.Quantity)).ToList(), payment.Discount, payment.Paid, payment.Method);
            var result = await Task.Run(() => _sales.CheckoutAsync(request));
            _cart.Clear();
            _cartHeading.Text = "Current bill";
            _cartHint.Text = "Choose a medicine to start a sale";
            await ReloadBatchesAsync();
            await RefreshProductsAsync();
            UpdateTotal();
            ReceiptDialog? receipt = null;
            try
            {
                var settings = await Task.Run(() => _settings.GetSettingsAsync());
                receipt = new ReceiptDialog(result, settings);
                await receipt.SavePdfToStoreAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Sale {result.InvoiceNumber} is saved in the database, but its PDF could not be stored. You can retry with Show PDF in Recent sales.\n\n{ex.Message}", "Sale saved · PDF storage issue", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            if (payment.ShowReceipt && receipt is not null) { Enabled = true; receipt.Show(FindForm()); }
        }
        catch (Exception ex) { ShowError(ex); }
        finally { if (!IsDisposed) Enabled = true; }
    }

    private async Task ShowRecentSalesAsync()
    {
        try
        {
            var rows = await Task.Run(() => _sales.GetRecentSalesAsync());
            using var form = new Form { Text = "Recent sales", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(760, 430) };
            var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = rows, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var reprint = ActionButton("Print selected receipt");
            var showPdf = ActionButton("Show PDF", true);
            actions.Controls.Add(reprint);
            actions.Controls.Add(showPdf);
            reprint.Click += async (_, _) =>
            {
                if (grid.CurrentRow?.DataBoundItem is not SaleListItem selected) { MessageBox.Show("Select a sale first."); return; }
                try
                {
                    var sale = await Task.Run(() => _sales.GetReceiptAsync(selected.InvoiceNumber));
                    var settings = await Task.Run(() => _settings.GetSettingsAsync());
                    using var receipt = new ReceiptDialog(sale, settings);
                    receipt.ShowDialog(form);
                }
                catch (Exception ex) { ShowError(ex); }
            };
            showPdf.Click += async (_, _) =>
            {
                if (grid.CurrentRow?.DataBoundItem is not SaleListItem selected) { MessageBox.Show("Select a sale first."); return; }
                try
                {
                    var sale = await Task.Run(() => _sales.GetReceiptAsync(selected.InvoiceNumber));
                    var settings = await Task.Run(() => _settings.GetSettingsAsync());
                    using var receipt = new ReceiptDialog(sale, settings);
                    await receipt.ShowStoredPdfAsync();
                }
                catch (Exception ex) { ShowError(ex); }
            };
            form.Controls.Add(grid);
            form.Controls.Add(actions);
            form.ShowDialog(FindForm());
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private sealed class CartRow(int medicineId, string name, int quantity, int available)
    {
        public int MedicineId { get; } = medicineId;
        public string Name { get; } = name;
        public int Available { get; set; } = available;
        public int Quantity { get; set; } = quantity;
        public decimal Amount { get; set; }
    }
}

internal sealed class ReceiptDialog : Form
{
    private readonly CheckoutResult _sale;
    private readonly PharmacySettings _settings;
    private readonly PrintDocument _document = new();
    private readonly Image? _logo;
    private readonly string _currency;
    private float _printY;
    private string[] _printLines = [];
    private int _nextPrintLine;
    private bool _firstPrintPage;

    public ReceiptDialog(CheckoutResult sale, PharmacySettings settings)
    {
        _sale = sale;
        _settings = settings;
        _currency = settings.CurrencyCode;
        if (!string.IsNullOrWhiteSpace(settings.LogoPath) && File.Exists(settings.LogoPath))
            try { using var source = Image.FromFile(settings.LogoPath); _logo = new Bitmap(source); } catch { _logo = null; }
        Text = "Sale receipt";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 680);
        MinimizeBox = false;
        MaximizeBox = false;
        var preview = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 10f), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Text = BuildText() };
        var print = DataPage.ActionButton("Print receipt", true);
        var savePdf = DataPage.ActionButton("Save as PDF");
        var showPdf = DataPage.ActionButton("Show PDF");
        var close = DataPage.ActionButton("Close");
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(close); footer.Controls.Add(savePdf); footer.Controls.Add(showPdf); footer.Controls.Add(print);
        Controls.Add(preview); Controls.Add(footer);
        _document.DocumentName = $"Receipt {_sale.InvoiceNumber}";
        _document.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
        _document.PrintController = new StandardPrintController();
        _firstPrintPage = true;
        _document.PrintPage += PrintPage;
        _document.BeginPrint += (_, _) => { _firstPrintPage = true; _nextPrintLine = 0; _printLines = []; };
        print.Click += async (_, _) => await PrintReceiptAsync(print);
        savePdf.Click += async (_, _) => await SavePdfAsync(savePdf);
        showPdf.Click += async (_, _) => await ShowStoredPdfAsync();
        close.Click += (_, _) => Close();
        FormClosed += (_, _) => { _document.Dispose(); _logo?.Dispose(); };
    }

    private string BuildText()
    {
        var width = 40;
        var lines = new List<string> { Center(_settings.PharmacyName, width) };
        if (!string.IsNullOrWhiteSpace(_settings.Address)) lines.Add(Center(_settings.Address, width));
        if (!string.IsNullOrWhiteSpace(_settings.Phone)) lines.Add(Center(_settings.Phone, width));
        lines.Add(new string('-', width));
        lines.Add(Center("SALES RECEIPT", width));
        lines.Add($"Invoice: {_sale.InvoiceNumber}");
        lines.Add($"Date:    {_sale.SoldAtLocal:dd MMM yyyy  h:mm:ss tt}");
        lines.Add(new string('-', width));
        lines.Add("ITEM                 QTY     PRICE      TOTAL");
        foreach (var line in _sale.Lines)
        {
            lines.Add(Clip(line.MedicineName, width));
            lines.Add($"                     {line.Quantity,3} {line.UnitPrice,9:N2} {line.LineTotal,10:N2}");
        }
        lines.Add(new string('-', width));
        lines.Add($"Subtotal:{_currency,8} {_sale.Subtotal,14:N2}");
        if (_sale.Discount > 0) lines.Add($"Discount:{_currency,8} {_sale.Discount,14:N2}");
        lines.Add($"TOTAL:{_currency,11} {_sale.Total,14:N2}");
        lines.Add($"Paid:{_currency,13} {_sale.Paid,14:N2}");
        if (_sale.Change > 0) lines.Add($"Change:{_currency,11} {_sale.Change,14:N2}");
        lines.Add($"Payment: {_sale.PaymentMethod}");
        lines.Add(new string('-', width));
        lines.Add(Center("Thank you for your purchase", width));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task PrintReceiptAsync(Button printButton)
    {
        printButton.Enabled = false;
        printButton.Text = "Printing…";
        try
        {
            await PrintOnStaThreadAsync();
            MessageBox.Show(this, "Receipt sent to the default printer.", "Print submitted", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, $"The sale is saved, but the default printer could not accept the receipt. You can retry from this window or Recent sales.\n\n{ex.Message}", "Print failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { if (!IsDisposed) { printButton.Enabled = true; printButton.Text = "Print receipt"; } }
    }

    private Task PrintOnStaThreadAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { _document.Print(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true, Name = "Pharmacy receipt printer" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private async Task SavePdfAsync(Button saveButton)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save sale receipt as PDF",
            Filter = "PDF document (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            FileName = $"Receipt-{_sale.InvoiceNumber}.pdf",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        saveButton.Enabled = false;
        saveButton.Text = "Saving…";
        try
        {
            var storedPath = await SavePdfToStoreAsync();
            await Task.Run(() => File.Copy(storedPath, dialog.FileName, true));
            MessageBox.Show(this, $"Receipt PDF saved to:\n{dialog.FileName}", "PDF saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save the receipt PDF.\n\n{ex.Message}", "PDF save failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if (!IsDisposed) { saveButton.Enabled = true; saveButton.Text = "Save as PDF"; } }
    }

    public Task<string> SavePdfToStoreAsync()
    {
        var path = StoredPdfPath(_sale.InvoiceNumber);
        var text = BuildText();
        return Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            try
            {
                WritePdf(temporaryPath, text);
                File.Move(temporaryPath, path, true);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            return path;
        });
    }

    public async Task ShowStoredPdfAsync()
    {
        var path = StoredPdfPath(_sale.InvoiceNumber);
        if (!File.Exists(path)) path = await SavePdfToStoreAsync();
        await Task.Run(() => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }));
    }

    private static string StoredPdfPath(string invoiceNumber)
    {
        var safeInvoice = string.Concat(invoiceNumber.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PharmacyManagement", "Receipts");
        return Path.Combine(directory, $"Receipt-{safeInvoice}.pdf");
    }

    private static void WritePdf(string path, string receiptText)
    {
        const int linesPerPage = 52;
        var lines = receiptText.Split([Environment.NewLine], StringSplitOptions.None);
        var pages = lines.Chunk(linesPerPage).ToList();
        var objects = new List<byte[]>();
        static byte[] Bytes(string value) => System.Text.Encoding.Latin1.GetBytes(value);
        void Add(string value) => objects.Add(Bytes(value));
        Add("<< /Type /Catalog /Pages 2 0 R >>");
        var pageObjectIds = Enumerable.Range(0, pages.Count).Select(i => 5 + i * 2).ToList();
        Add($"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>");
        Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var contentObjectId = 4 + pageIndex * 2;
            var pageObjectId = contentObjectId + 1;
            var content = new StringBuilder("BT\n/F1 10 Tf\n48 790 Td\n14 TL\n");
            foreach (var line in pages[pageIndex])
            {
                content.Append('<').Append(Convert.ToHexString(ToWinAnsi(line))).Append("> Tj\nT*\n");
            }
            content.Append("ET");
            var contentBytes = Bytes(content.ToString());
            Add($"<< /Length {contentBytes.Length} >>\nstream\n{System.Text.Encoding.Latin1.GetString(contentBytes)}\nendstream");
            Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >>");
        }

        using var output = new MemoryStream();
        output.Write(Bytes("%PDF-1.4\n%âãÏÓ\n"));
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(Bytes($"{i + 1} 0 obj\n"));
            output.Write(objects[i]);
            output.Write(Bytes("\nendobj\n"));
        }
        var xrefPosition = output.Position;
        output.Write(Bytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1)) output.Write(Bytes($"{offset:0000000000} 00000 n \n"));
        output.Write(Bytes($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF"));
        File.WriteAllBytes(path, output.ToArray());
    }

    private static byte[] ToWinAnsi(string value)
    {
        // Keep the receipt portable without requiring CodePagesEncodingProvider at runtime.
        // Courier with WinAnsiEncoding renders these ASCII bytes consistently; unsupported
        // medicine-name characters are replaced instead of failing during PDF export.
        return System.Text.Encoding.ASCII.GetBytes(value);
    }

    private void PrintPage(object? sender, PrintPageEventArgs e)
    {
        var graphics = e.Graphics;
        if (graphics is null) return;
        var left = e.MarginBounds.Left;
        var width = e.MarginBounds.Width;
        using var font = new Font("Consolas", 8.5f, FontStyle.Regular);
        using var bold = new Font("Consolas", 10f, FontStyle.Bold);
        using var center = new StringFormat { Alignment = StringAlignment.Center };
        _printY = e.MarginBounds.Top;
        if (_firstPrintPage)
        {
            _printLines = BuildText().Split([Environment.NewLine], StringSplitOptions.None);
            _nextPrintLine = 4;
            if (_logo is not null)
            {
                var maxWidth = Math.Min(width * .62f, 180f);
                var ratio = Math.Min(maxWidth / _logo.Width, 72f / _logo.Height);
                var size = new SizeF(_logo.Width * ratio, _logo.Height * ratio);
                graphics.DrawImage(_logo, left + (width - size.Width) / 2, _printY, size.Width, size.Height);
                _printY += size.Height + 5;
            }
            DrawCentered(graphics, _settings.PharmacyName, bold, center, left, width);
            if (!string.IsNullOrWhiteSpace(_settings.Address)) DrawCentered(graphics, _settings.Address, font, center, left, width);
            if (!string.IsNullOrWhiteSpace(_settings.Phone)) DrawCentered(graphics, _settings.Phone, font, center, left, width);
        }
        while (_nextPrintLine < _printLines.Length)
        {
            if (_printY + font.GetHeight(graphics) > e.MarginBounds.Bottom)
            {
                e.HasMorePages = true;
                _firstPrintPage = false;
                return;
            }
            var line = _printLines[_nextPrintLine++];
            graphics.DrawString(line, font, Brushes.Black, left, _printY);
            _printY += font.GetHeight(graphics) + 1;
        }
        e.HasMorePages = false;
        _firstPrintPage = true;
    }

    private void DrawCentered(Graphics graphics, string text, Font font, StringFormat format, int left, int width)
    {
        graphics.DrawString(text, font, Brushes.Black, new RectangleF(left, _printY, width, font.GetHeight(graphics) + 3), format);
        _printY += font.GetHeight(graphics) + 2;
    }

    private static string Center(string value, int width) => value.Length >= width ? value[..width] : value.PadLeft((width + value.Length) / 2).PadRight(width);
    private static string Clip(string value, int width) => value.Length <= width ? value : value[..(width - 1)] + "…";
}

internal sealed class PaymentDialog : Form
{
    private readonly NumericUpDown _discount = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly NumericUpDown _paid = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly decimal _subtotal;
    private readonly string _currencyCode;
    private readonly Label _totalLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), ForeColor = PharmacyColors.Navy };
    private readonly CheckBox _printReceipt = new() { Text = "Show receipt options after sale", AutoSize = true, Checked = true };
    public decimal Discount => _discount.Value;
    public decimal Paid => _paid.Value;
    public bool ShowReceipt => _printReceipt.Checked;
    public string Method => Convert.ToString(_method.SelectedItem) ?? "Cash";

    public PaymentDialog(decimal subtotal, string currencyCode)
    {
        _subtotal = subtotal;
        _currencyCode = currencyCode;
        Text = "Take payment";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(450, 320);
        MaximizeBox = false;
        MinimizeBox = false;
        _discount.Maximum = subtotal;
        _paid.Value = subtotal;
        _method.Items.AddRange(["Cash", "Card", "Bank transfer", "Credit"]);
        _method.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(20) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        AddRow(layout, 0, "Subtotal", new Label { Text = $"{currencyCode} {subtotal:N2}", AutoSize = true });
        AddRow(layout, 1, "Discount", _discount);
        layout.Controls.Add(new Label { Text = "Total", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_totalLabel, 1, 2);
        AddRow(layout, 3, "Paid amount", _paid);
        AddRow(layout, 4, "Payment method", _method);
        layout.Controls.Add(_printReceipt, 1, 5);
        var confirm = DataPage.ActionButton("Complete sale", true);
        confirm.Anchor = AnchorStyles.Right;
        confirm.Click += (_, _) =>
        {
            var total = _subtotal - _discount.Value;
            if (_method.Text == "Credit" && _paid.Value > total) { MessageBox.Show("For credit sales, paid amount cannot exceed the total."); return; }
            if (_method.Text != "Credit" && _paid.Value < total) { MessageBox.Show("Paid amount must cover the total. Choose Credit to record an unpaid balance."); return; }
            DialogResult = DialogResult.OK;
            Close();
        };
        layout.Controls.Add(confirm, 1, 6);
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
