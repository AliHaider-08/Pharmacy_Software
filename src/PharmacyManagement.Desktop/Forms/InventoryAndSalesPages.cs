using System.ComponentModel;
using System.Drawing.Printing;
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
    private readonly ListBox _products = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly Dictionary<int, string> _productLabels = new();
    private readonly SplitContainer _workspace = new() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1, BackColor = Color.White };
    private string _currencyCode = "PKR";
    private readonly Label _total = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold), ForeColor = PharmacyColors.Navy, Margin = new Padding(12, 7, 12, 4) };

    public PointOfSalePage(ICatalogService catalog, IInventoryService inventory, ISalesService sales, IBackOfficeService settings) : base("Sales / POS", "Select an in-stock medicine or scan a barcode, then complete the bill.")
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
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(CartRow.Amount), DataPropertyName = nameof(CartRow.Amount), HeaderText = "Line total", ReadOnly = true, FillWeight = 25, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
        Grid.DataSource = _cart;
        Grid.CellEndEdit += (_, e) => { if (e.RowIndex >= 0) UpdateCartRow(e.RowIndex); };
        Grid.DataError += (_, e) => { e.ThrowException = false; if (e.RowIndex >= 0) UpdateCartRow(e.RowIndex); };
        Grid.UserDeletedRow += (_, _) => UpdateTotal();
        _products.Format += (_, e) => { if (e.ListItem is MedicineListItem medicine && _productLabels.TryGetValue(medicine.Id, out var label)) e.Value = label; };
        _products.DoubleClick += async (_, _) => { if (_products.SelectedItem is MedicineListItem item) await AddMedicineToCartAsync(item); };
        var productPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
        productPanel.Controls.Add(_products);
        productPanel.Controls.Add(new Label { Text = "IN STOCK", Dock = DockStyle.Top, Height = 30, ForeColor = PharmacyColors.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        _workspace.Panel1.Controls.Add(productPanel);
        _workspace.Panel2.Controls.Add(Grid);
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
        _workspace.SplitterDistance = 310;
        Load += async (_, _) => { try { _currencyCode = (await _settings.GetSettingsAsync()).CurrencyCode; Grid.Columns[nameof(CartRow.Amount)]!.HeaderText = $"Line total ({_currencyCode})"; UpdateTotal(); } catch (Exception ex) { ShowError(ex); } await ReloadBatchesAsync(); await RefreshProductsAsync(); };
        UpdateTotal();
    }

    private async Task ReloadBatchesAsync()
    {
        try { _batches = await _inventory.GetBatchesAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task RefreshProductsAsync()
    {
        try
        {
            var medicines = await _catalog.SearchMedicinesAsync();
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
            _products.DataSource = null;
            _products.DisplayMember = nameof(MedicineListItem.Name);
            _products.DataSource = available;
        }
        catch (Exception ex) { ShowError(ex); }
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
            var matches = await _catalog.SearchMedicinesAsync(query);
            if (matches.Count == 0) { MessageBox.Show("No active medicine matched that name or barcode."); return; }
            MedicineListItem? medicine;
            var exactBarcode = matches.FirstOrDefault(x => string.Equals(x.Barcode, query, StringComparison.OrdinalIgnoreCase));
            if (exactBarcode is not null) medicine = exactBarcode;
            else if (matches.Count == 1) medicine = matches[0];
            else medicine = ChooseMedicine(matches);
            if (medicine is null) return;
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

    private void UpdateCartRow(int rowIndex)
    {
        if (rowIndex >= _cart.Count) return;
        var row = _cart[rowIndex];
        row.Available = EligibleBatches(row.MedicineId).Sum(x => x.Quantity);
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

    private decimal Subtotal() => _cart.Sum(x => x.Amount);

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
            Grid.EndEdit();
            foreach (var cartRow in _cart)
                if (cartRow.Quantity < 1 || cartRow.Quantity > cartRow.Available)
                {
                    MessageBox.Show($"Quantity for {cartRow.Name} must be between 1 and {cartRow.Available}.");
                    return;
                }
            var result = await _sales.CheckoutAsync(new CheckoutRequest(_cart.Select(x => new CartLineRequest(x.MedicineId, x.Quantity)).ToList(), payment.Discount, payment.Paid, payment.Method));
            _cart.Clear();
            await ReloadBatchesAsync();
            await RefreshProductsAsync();
            UpdateTotal();
            if (payment.PrintReceipt)
            {
                try
                {
                    var settings = await _settings.GetSettingsAsync();
                    using var receipt = new ReceiptDialog(result, settings);
                    receipt.ShowDialog(FindForm());
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Sale {result.InvoiceNumber} was saved, but the receipt could not be printed. You can reprint it from Recent sales.\n\n{ex.Message}", "Sale saved · print issue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
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
            var reprint = ActionButton("Print selected receipt", true);
            reprint.Dock = DockStyle.Bottom;
            reprint.Click += async (_, _) =>
            {
                if (grid.CurrentRow?.DataBoundItem is not SaleListItem selected) { MessageBox.Show("Select a sale first."); return; }
                try
                {
                    var sale = await _sales.GetReceiptAsync(selected.InvoiceNumber);
                    var settings = await _settings.GetSettingsAsync();
                    using var receipt = new ReceiptDialog(sale, settings);
                    receipt.ShowDialog(form);
                }
                catch (Exception ex) { ShowError(ex); }
            };
            form.Controls.Add(grid);
            form.Controls.Add(reprint);
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
        var close = DataPage.ActionButton("Close");
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(close); footer.Controls.Add(print);
        Controls.Add(preview); Controls.Add(footer);
        _document.DocumentName = $"Receipt {_sale.InvoiceNumber}";
        _document.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
        _document.PrintController = new StandardPrintController();
        _firstPrintPage = true;
        _document.PrintPage += PrintPage;
        _document.BeginPrint += (_, _) => { _firstPrintPage = true; _nextPrintLine = 0; _printLines = []; };
        print.Click += async (_, _) => await PrintReceiptAsync(print);
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
        using var dialog = new PrintDialog { Document = _document, UseEXDialog = true, AllowSomePages = false, AllowSelection = false };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        printButton.Enabled = false;
        printButton.Text = "Printing…";
        try
        {
            await Task.Run(() => _document.Print());
            MessageBox.Show(this, "Receipt sent to the selected printer.", "Print submitted", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, $"The sale is saved, but the printer did not accept the receipt. You can retry from this window or Recent sales.\n\n{ex.Message}", "Print failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { if (!IsDisposed) { printButton.Enabled = true; printButton.Text = "Print receipt"; } }
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
    private readonly CheckBox _printReceipt = new() { Text = "Print receipt after sale", AutoSize = true, Checked = true };
    public decimal Discount => _discount.Value;
    public decimal Paid => _paid.Value;
    public bool PrintReceipt => _printReceipt.Checked;
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
