using System.ComponentModel;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Desktop.Forms;

public sealed class SuppliersPage : DataPage
{
    private readonly IBackOfficeService _service;
    public SuppliersPage(IBackOfficeService service) : base("Suppliers", "Maintain supplier contacts and opening balances.")
    {
        _service = service;
        var add = ActionButton("+  Add supplier", true);
        var edit = ActionButton("Edit");
        var delete = ActionButton("Delete unused");
        Toolbar.Controls.Add(SearchBox); Toolbar.Controls.Add(add); Toolbar.Controls.Add(edit); Toolbar.Controls.Add(delete);
        add.Click += async (_, _) => await EditAsync(null);
        edit.Click += async (_, _) => await EditAsync(Grid.CurrentRow?.DataBoundItem as SupplierListItem);
        delete.Click += async (_, _) => await DeleteAsync();
        SearchBox.TextChanged += async (_, _) => await RefreshAsync();
        Load += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try { Grid.DataSource = await _service.GetSuppliersAsync(SearchBox.Text); HideId(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private void HideId() { if (Grid.Columns.Count == 0) return; Grid.Columns[nameof(SupplierListItem.Id)]!.Visible = false; Grid.Columns[nameof(SupplierListItem.Name)]!.HeaderText = "Supplier"; Grid.Columns[nameof(SupplierListItem.CompanyName)]!.HeaderText = "Company"; Grid.Columns[nameof(SupplierListItem.Phone)]!.HeaderText = "Phone"; Grid.Columns[nameof(SupplierListItem.Email)]!.HeaderText = "Email"; Grid.Columns[nameof(SupplierListItem.Address)]!.HeaderText = "Address"; Grid.Columns[nameof(SupplierListItem.OpeningBalance)]!.HeaderText = "Opening balance"; }

    private async Task EditAsync(SupplierListItem? current)
    {
        using var dialog = new SimpleRecordDialog(current is null ? "Add supplier" : "Edit supplier");
        dialog.AddText("name", "Supplier name *", current?.Name);
        dialog.AddText("company", "Company", current?.CompanyName);
        dialog.AddText("phone", "Phone", current?.Phone);
        dialog.AddText("email", "Email", current?.Email);
        dialog.AddText("address", "Address", current?.Address);
        dialog.AddMoney("balance", "Opening balance", current?.OpeningBalance ?? 0m);
        dialog.SetSave("Save supplier", async () => await _service.SaveSupplierAsync(new SupplierListItem(current?.Id ?? 0, dialog.TextValue("name"), dialog.TextValue("company"), dialog.TextValue("phone"), dialog.TextValue("email"), dialog.TextValue("address"), dialog.MoneyValue("balance"))));
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await RefreshAsync();
    }

    private async Task DeleteAsync()
    {
        if (Grid.CurrentRow?.DataBoundItem is not SupplierListItem supplier) { MessageBox.Show("Select a supplier first."); return; }
        if (!Confirm($"Delete supplier {supplier.Name}?")) return;
        try { await _service.DeleteSupplierAsync(supplier.Id); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

public sealed class CustomersPage : DataPage
{
    private readonly IBackOfficeService _service;
    public CustomersPage(IBackOfficeService service) : base("Customers", "Keep customer contacts for credit sales and purchase history.")
    {
        _service = service;
        var add = ActionButton("+  Add customer", true); var edit = ActionButton("Edit"); var delete = ActionButton("Delete unused");
        Toolbar.Controls.Add(SearchBox); Toolbar.Controls.Add(add); Toolbar.Controls.Add(edit); Toolbar.Controls.Add(delete);
        add.Click += async (_, _) => await EditAsync(null);
        edit.Click += async (_, _) => await EditAsync(Grid.CurrentRow?.DataBoundItem as CustomerListItem);
        delete.Click += async (_, _) => await DeleteAsync();
        SearchBox.TextChanged += async (_, _) => await RefreshAsync();
        Load += async (_, _) => await RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        try
        {
            Grid.DataSource = await _service.GetCustomersAsync(SearchBox.Text);
            if (Grid.Columns.Count == 0) return;
            Grid.Columns[nameof(CustomerListItem.Id)]!.Visible = false;
            foreach (var name in new[] { nameof(CustomerListItem.Name), nameof(CustomerListItem.Phone), nameof(CustomerListItem.Email), nameof(CustomerListItem.Address), nameof(CustomerListItem.CustomerType) })
                Grid.Columns[name]!.HeaderText = name == nameof(CustomerListItem.CustomerType) ? "Type" : name;
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task EditAsync(CustomerListItem? current)
    {
        using var dialog = new SimpleRecordDialog(current is null ? "Add customer" : "Edit customer");
        dialog.AddText("name", "Customer name *", current?.Name);
        dialog.AddText("phone", "Phone", current?.Phone);
        dialog.AddText("email", "Email", current?.Email);
        dialog.AddText("address", "Address", current?.Address);
        dialog.AddChoice("type", "Customer type", ["Retail", "Wholesale", "Credit"], current?.CustomerType ?? "Retail");
        dialog.SetSave("Save customer", async () => await _service.SaveCustomerAsync(new CustomerListItem(current?.Id ?? 0, dialog.TextValue("name"), dialog.TextValue("phone"), dialog.TextValue("email"), dialog.TextValue("address"), dialog.TextValue("type"))));
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await RefreshAsync();
    }
    private async Task DeleteAsync()
    {
        if (Grid.CurrentRow?.DataBoundItem is not CustomerListItem customer) { MessageBox.Show("Select a customer first."); return; }
        if (!Confirm($"Delete customer {customer.Name}?")) return;
        try { await _service.DeleteCustomerAsync(customer.Id); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

internal sealed class SimpleRecordDialog : Form
{
    private readonly TableLayoutPanel _table = new() { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18), AutoScroll = true };
    private readonly Dictionary<string, Control> _fields = new();
    private int _row;

    public SimpleRecordDialog(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(500, 380);
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9f);
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(_table);
    }

    public void AddText(string key, string label, string? initial = null, bool multiline = false)
    {
        var input = new TextBox { Text = initial ?? "", Multiline = multiline, Height = multiline ? 60 : 28 };
        Add(key, label, input, multiline ? 68 : 42);
    }
    public void AddMoney(string key, string label, decimal initial = 0)
    {
        var input = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true, Value = Math.Clamp(initial, 0, 999999999) };
        Add(key, label, input, 42);
    }
    public void AddChoice(string key, string label, string[] values, string initial)
    {
        var input = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        input.Items.AddRange(values);
        input.SelectedItem = initial;
        Add(key, label, input, 42);
    }
    public string TextValue(string key) => _fields[key] switch { TextBox text => text.Text.Trim(), ComboBox choice => Convert.ToString(choice.SelectedItem) ?? "", _ => "" };
    public decimal MoneyValue(string key) => ((NumericUpDown)_fields[key]).Value;
    public void SetSave(string text, Func<Task> saveAction)
    {
        var button = DataPage.ActionButton(text, true);
        button.Anchor = AnchorStyles.Right;
        button.Click += async (_, _) =>
        {
            try { await saveAction(); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) { DataPage.ShowError(ex); }
        };
        Add("__save", "", button, 48, false);
    }
    private void Add(string key, string label, Control control, int height, bool store = true)
    {
        _table.RowCount = Math.Max(_table.RowCount, _row + 1);
        _table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        if (!string.IsNullOrEmpty(label)) _table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, _row);
        control.Dock = DockStyle.Fill;
        _table.Controls.Add(control, 1, _row);
        if (store) _fields.Add(key, control);
        _row++;
    }
}

public sealed class PurchasesPage : DataPage
{
    private readonly IBackOfficeService _service;
    private readonly ICatalogService _catalog;
    public PurchasesPage(IBackOfficeService service, ICatalogService catalog) : base("Purchases", "Create supplier invoices and receive their medicine batches into stock.")
    {
        _service = service; _catalog = catalog;
        var create = ActionButton("+  New purchase", true); Toolbar.Controls.Add(create); create.Click += async (_, _) => await NewPurchaseAsync();
        Load += async (_, _) => await RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        try
        {
            Grid.DataSource = await _service.GetPurchasesAsync();
            if (Grid.Columns.Count == 0) return;
            foreach (var column in new[] { nameof(PurchaseListItem.InvoiceNumber), nameof(PurchaseListItem.PurchasedAtLocal), nameof(PurchaseListItem.Supplier), nameof(PurchaseListItem.Total), nameof(PurchaseListItem.Paid), nameof(PurchaseListItem.Status) }) Grid.Columns[column]!.HeaderText = column switch { nameof(PurchaseListItem.PurchasedAtLocal) => "Date", nameof(PurchaseListItem.Total) => "Total", _ => column };
            Grid.Columns[nameof(PurchaseListItem.Total)]!.DefaultCellStyle.Format = "N2";
            Grid.Columns[nameof(PurchaseListItem.Paid)]!.DefaultCellStyle.Format = "N2";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task NewPurchaseAsync()
    {
        try
        {
            var suppliers = await _service.GetSuppliersAsync();
            var medicines = await _catalog.SearchMedicinesAsync();
            if (suppliers.Count == 0 || medicines.Count == 0) { MessageBox.Show("Add at least one supplier and one active medicine before recording a purchase."); return; }
            var currency = (await _service.GetSettingsAsync()).CurrencyCode;
            using var dialog = new PurchaseEntryDialog(suppliers, medicines, _service, currency);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await RefreshAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }
}

internal sealed class PurchaseEntryDialog : Form
{
    private readonly IBackOfficeService _service;
    private readonly ComboBox _supplier = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly TextBox _invoice = new() { Width = 240 };
    private readonly BindingList<PurchaseDisplayLine> _lines = new();
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = true, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly NumericUpDown _discount = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly NumericUpDown _paid = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly Label _total = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), ForeColor = PharmacyColors.Navy };
    private readonly IReadOnlyList<MedicineListItem> _medicines;
    private readonly string _currencyCode;
    public PurchaseEntryDialog(IReadOnlyList<SupplierListItem> suppliers, IReadOnlyList<MedicineListItem> medicines, IBackOfficeService service, string currencyCode)
    {
        _service = service; _medicines = medicines; _currencyCode = currencyCode;
        Text = "New purchase invoice"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(920, 620); MinimumSize = new Size(800, 520); Font = new Font("Segoe UI", 9f);
        _supplier.DataSource = suppliers.ToList(); _supplier.DisplayMember = nameof(SupplierListItem.Name); _supplier.ValueMember = nameof(SupplierListItem.Id);
        _grid.DataSource = _lines;
        _grid.DataBindingComplete += (_, _) =>
        {
            if (_grid.Columns[nameof(PurchaseDisplayLine.MedicineId)] is { } id) id.Visible = false;
            if (_grid.Columns[nameof(PurchaseDisplayLine.Input)] is { } input) input.Visible = false;
            if (_grid.Columns[nameof(PurchaseDisplayLine.LineTotal)] is { } total) { total.HeaderText = "Line total"; total.DefaultCellStyle.Format = "N2"; }
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); Controls.Add(layout);
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        header.Controls.Add(new Label { Text = "Supplier", AutoSize = true, Margin = new Padding(0, 8, 8, 0) }); header.Controls.Add(_supplier);
        header.Controls.Add(new Label { Text = "Invoice # (optional)", AutoSize = true, Margin = new Padding(20, 8, 8, 0) }); header.Controls.Add(_invoice);
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(_grid, 0, 1);
        var itemButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var addItem = DataPage.ActionButton("+ Add item", true); var remove = DataPage.ActionButton("Remove selected");
        itemButtons.Controls.Add(addItem); itemButtons.Controls.Add(remove); itemButtons.Controls.Add(new Label { Text = "Discount", AutoSize = true, Margin = new Padding(10, 10, 4, 0) }); itemButtons.Controls.Add(_discount); itemButtons.Controls.Add(new Label { Text = "Paid", AutoSize = true, Margin = new Padding(10, 10, 4, 0) }); itemButtons.Controls.Add(_paid); itemButtons.Controls.Add(_total);
        layout.Controls.Add(itemButtons, 0, 2);
        var save = DataPage.ActionButton("Save purchase and receive stock", true); save.Anchor = AnchorStyles.Right; layout.Controls.Add(save, 0, 3);
        addItem.Click += (_, _) => AddItem(); remove.Click += (_, _) => { if (_grid.CurrentRow?.DataBoundItem is PurchaseDisplayLine row) _lines.Remove(row); UpdateTotals(); };
        _discount.ValueChanged += (_, _) => UpdateTotals(); _paid.Value = 0; save.Click += async (_, _) => await SaveAsync();
    }
    private void AddItem()
    {
        using var dialog = new PurchaseLineDialog(_medicines);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Item is null) return;
        if (_lines.Any(x => x.MedicineId == dialog.Item.MedicineId && string.Equals(x.BatchNumber, dialog.Item.BatchNumber, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("That batch is already in this purchase."); return; }
        _lines.Add(new PurchaseDisplayLine(dialog.Item));
        UpdateTotals();
    }
    private void UpdateTotals()
    {
        var subtotal = _lines.Sum(x => x.LineTotal);
        _discount.Maximum = subtotal;
        _total.Text = $"Total {_currencyCode} {subtotal - _discount.Value:N2}";
        _paid.Maximum = subtotal - _discount.Value;
    }
    private async Task SaveAsync()
    {
        try
        {
            if (_supplier.SelectedValue is not int supplierId) throw new InvalidOperationException("Select a supplier.");
            if (_lines.Count == 0) throw new InvalidOperationException("Add at least one item.");
            await _service.CreatePurchaseAsync(new PurchaseRequest(supplierId, _invoice.Text, _discount.Value, _paid.Value, _lines.Select(x => x.Input).ToList()));
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
    private sealed class PurchaseDisplayLine(PurchaseItemInput input)
    {
        public int MedicineId => input.MedicineId;
        public string Medicine => input.MedicineName;
        public string BatchNumber => input.BatchNumber;
        public DateOnly? Expiry => input.ExpiryDate;
        public int Quantity => input.Quantity;
        public decimal Cost => input.PurchasePrice;
        public decimal SalePrice => input.SalePrice;
        public decimal LineTotal => input.Quantity * input.PurchasePrice;
        public PurchaseItemInput Input => input;
    }
}

internal sealed class PurchaseLineDialog : Form
{
    private readonly ComboBox _medicine = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _batch = new();
    private readonly DateTimePicker _expiry = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly NumericUpDown _quantity = new() { Minimum = 1, Maximum = 10000000, Value = 1, ThousandsSeparator = true };
    private readonly NumericUpDown _cost = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    private readonly NumericUpDown _sale = new() { DecimalPlaces = 2, Minimum = 0, Maximum = 999999999, ThousandsSeparator = true };
    public PurchaseItemInput? Item { get; private set; }
    public PurchaseLineDialog(IReadOnlyList<MedicineListItem> medicines)
    {
        Text = "Add purchase item"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; ClientSize = new Size(500, 320); MaximizeBox = false; MinimizeBox = false;
        _medicine.DataSource = medicines.ToList(); _medicine.DisplayMember = nameof(MedicineListItem.Name); _medicine.ValueMember = nameof(MedicineListItem.Id);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(18) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        Add(layout, 0, "Medicine", _medicine); Add(layout, 1, "Batch number", _batch); Add(layout, 2, "Expiry", _expiry); Add(layout, 3, "Quantity", _quantity); Add(layout, 4, "Purchase price", _cost); Add(layout, 5, "Sale price", _sale);
        var save = DataPage.ActionButton("Add line", true); save.Click += (_, _) =>
        {
            if (_medicine.SelectedItem is not MedicineListItem med) { MessageBox.Show("Select a medicine."); return; }
            if (string.IsNullOrWhiteSpace(_batch.Text)) { MessageBox.Show("Batch number is required."); return; }
            Item = new PurchaseItemInput(med.Id, _batch.Text.Trim(), _expiry.Checked ? DateOnly.FromDateTime(_expiry.Value) : null, (int)_quantity.Value, _cost.Value, _sale.Value, med.Name);
            DialogResult = DialogResult.OK; Close();
        };
        layout.Controls.Add(save, 1, 6);
    }
    private static void Add(TableLayoutPanel layout, int row, string label, Control input) { layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); input.Dock = DockStyle.Fill; layout.Controls.Add(input, 1, row); }
}

public sealed class ExpensesPage : DataPage
{
    private readonly IBackOfficeService _service;
    public ExpensesPage(IBackOfficeService service) : base("Expenses", "Record business expenses and review the dated expense history.")
    {
        _service = service;
        var add = ActionButton("+  Add expense", true); Toolbar.Controls.Add(add); add.Click += async (_, _) => await AddAsync();
        Load += async (_, _) => await RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        try
        {
            Grid.DataSource = await _service.GetExpensesAsync(DateTime.Today.AddMonths(-1), DateTime.Today);
            if (Grid.Columns.Count == 0) return;
            Grid.Columns[nameof(ExpenseListItem.Id)]!.Visible = false;
            foreach (var column in new[] { nameof(ExpenseListItem.DateLocal), nameof(ExpenseListItem.Category), nameof(ExpenseListItem.Amount), nameof(ExpenseListItem.PaymentMethod), nameof(ExpenseListItem.Description) }) Grid.Columns[column]!.HeaderText = column == nameof(ExpenseListItem.DateLocal) ? "Date" : column;
            Grid.Columns[nameof(ExpenseListItem.Amount)]!.DefaultCellStyle.Format = "N2";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task AddAsync()
    {
        using var dialog = new ExpenseDialog();
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try { await _service.AddExpenseAsync(dialog.Input); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
}

internal sealed class ExpenseDialog : Form
{
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDown, Text = "Utilities" };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly NumericUpDown _amount = new() { DecimalPlaces = 2, Minimum = 0.01m, Maximum = 999999999, Value = 1, ThousandsSeparator = true };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _description = new() { Multiline = true };
    public ExpenseInput Input => new(Convert.ToString(_category.Text) ?? "", _date.Value, _amount.Value, Convert.ToString(_method.SelectedItem) ?? "Cash", _description.Text);
    public ExpenseDialog()
    {
        Text = "Add expense"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; ClientSize = new Size(460, 270); MaximizeBox = false; MinimizeBox = false;
        _category.Items.AddRange(["Rent", "Utilities", "Salaries", "Transport", "Supplies", "Maintenance", "Other"]);
        _method.Items.AddRange(["Cash", "Card", "Bank transfer"]); _method.SelectedIndex = 0;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(18) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        Add(layout, 0, "Category", _category); Add(layout, 1, "Date", _date); Add(layout, 2, "Amount", _amount); Add(layout, 3, "Method", _method); Add(layout, 4, "Description", _description);
        var save = DataPage.ActionButton("Save expense", true); save.Anchor = AnchorStyles.Right; layout.Controls.Add(save, 1, 5); save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
    }
    private static void Add(TableLayoutPanel table, int row, string label, Control field) { table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); field.Dock = DockStyle.Fill; table.Controls.Add(field, 1, row); }
}
