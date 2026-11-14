using System.Text;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Domain.Entities;

namespace PharmacyManagement.Desktop.Forms;

public sealed class ReportsPage : DataPage
{
    private readonly IReportService _reports;
    private readonly ComboBox _reportType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Margin = new Padding(0, 8, 8, 4) };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today.AddDays(-30), Margin = new Padding(0, 8, 8, 4) };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today, Margin = new Padding(0, 8, 8, 4) };
    private IReadOnlyList<object> _currentRows = [];

    public ReportsPage(IReportService reports) : base("Reports", "Review and export sales, purchasing, expenses and batch-level stock.")
    {
        _reports = reports;
        _reportType.Items.AddRange(["Sales", "Purchases", "Expenses", "Stock", "Low stock"]);
        _reportType.SelectedIndex = 0;
        var run = ActionButton("Run report", true);
        var export = ActionButton("Export CSV");
        Toolbar.Controls.Add(new Label { Text = "Report", AutoSize = true, Margin = new Padding(0, 13, 6, 0) }); Toolbar.Controls.Add(_reportType);
        Toolbar.Controls.Add(new Label { Text = "From", AutoSize = true, Margin = new Padding(8, 13, 5, 0) }); Toolbar.Controls.Add(_from);
        Toolbar.Controls.Add(new Label { Text = "To", AutoSize = true, Margin = new Padding(8, 13, 5, 0) }); Toolbar.Controls.Add(_to);
        Toolbar.Controls.Add(run); Toolbar.Controls.Add(export);
        run.Click += async (_, _) => await RunAsync(); export.Click += (_, _) => Export();
        _reportType.SelectedIndexChanged += (_, _) => { var stock = _reportType.Text is "Stock" or "Low stock"; _from.Enabled = !stock; _to.Enabled = !stock; };
        Load += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        if (_to.Value.Date < _from.Value.Date) { MessageBox.Show("The end date must be on or after the start date."); return; }
        try
        {
            switch (_reportType.Text)
            {
                case "Sales":
                    var sales = await _reports.GetSalesReportAsync(_from.Value.Date, _to.Value.Date);
                    _currentRows = sales.Cast<object>().ToList(); Grid.DataSource = sales; FormatReportGrid(); break;
                case "Purchases":
                    var purchases = await _reports.GetPurchaseReportAsync(_from.Value.Date, _to.Value.Date);
                    _currentRows = purchases.Cast<object>().ToList(); Grid.DataSource = purchases; FormatReportGrid(); break;
                case "Expenses":
                    var expenses = await _reports.GetExpenseReportAsync(_from.Value.Date, _to.Value.Date);
                    _currentRows = expenses.Cast<object>().ToList(); Grid.DataSource = expenses; FormatReportGrid(); break;
                case "Stock":
                case "Low stock":
                    var stock = await _reports.GetStockReportAsync(_reportType.Text == "Low stock");
                    _currentRows = stock.Cast<object>().ToList(); Grid.DataSource = stock; FormatStockGrid(); break;
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void FormatReportGrid()
    {
        foreach (DataGridViewColumn c in Grid.Columns) c.HeaderText = c.Name switch { nameof(ReportLine.Date) => "Date", nameof(ReportLine.Reference) => "Reference", nameof(ReportLine.Description) => "Details", nameof(ReportLine.Amount) => "Amount (PKR)", nameof(ReportLine.Paid) => "Paid (PKR)", nameof(ReportLine.Balance) => "Balance (PKR)", _ => c.Name };
        foreach (var name in new[] { nameof(ReportLine.Amount), nameof(ReportLine.Paid), nameof(ReportLine.Balance) }) if (Grid.Columns[name] is { } column) column.DefaultCellStyle.Format = "N2";
    }

    private void FormatStockGrid()
    {
        foreach (DataGridViewColumn c in Grid.Columns) c.HeaderText = c.Name switch { nameof(StockReportLine.Medicine) => "Medicine", nameof(StockReportLine.Category) => "Category", nameof(StockReportLine.Batch) => "Batch", nameof(StockReportLine.Expiry) => "Expiry", nameof(StockReportLine.Quantity) => "Quantity", nameof(StockReportLine.Cost) => "Cost (PKR)", nameof(StockReportLine.Price) => "Sale price (PKR)", _ => c.Name };
        foreach (var name in new[] { nameof(StockReportLine.Cost), nameof(StockReportLine.Price) }) if (Grid.Columns[name] is { } column) column.DefaultCellStyle.Format = "N2";
    }

    private void Export()
    {
        if (_currentRows.Count == 0) { MessageBox.Show("Run a report with records before exporting."); return; }
        using var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"{_reportType.Text.ToLowerInvariant()}-{DateTime.Now:yyyyMMdd}.csv" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            var builder = new StringBuilder();
            var columns = Grid.Columns.Cast<DataGridViewColumn>().Where(x => x.Visible).ToList();
            builder.AppendLine(string.Join(",", columns.Select(x => Csv(x.HeaderText))));
            foreach (DataGridViewRow row in Grid.Rows)
                builder.AppendLine(string.Join(",", columns.Select(c => Csv(Convert.ToString(row.Cells[c.Index].Value) ?? ""))));
            File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Report exported.", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed class SettingsPage : UserControl
{
    private readonly IBackOfficeService _service;
    private readonly TextBox _name = new();
    private readonly TextBox _address = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _currency = new();
    private readonly TextBox _prefix = new();
    private readonly NumericUpDown _expiryDays = new() { Minimum = 0, Maximum = 3650 };

    public SettingsPage(IBackOfficeService service)
    {
        _service = service; Dock = DockStyle.Fill; BackColor = PharmacyColors.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = PharmacyColors.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(layout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "Pharmacy settings", AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text });
        heading.Controls.Add(new Label { Text = "Business identity and defaults used on local invoices and alerts.", AutoSize = true, Location = new Point(2, 44), ForeColor = PharmacyColors.Muted });
        layout.Controls.Add(heading, 0, 0);
        var card = new Panel { Dock = DockStyle.Top, Height = 360, BackColor = Color.White, Padding = new Padding(22) };
        layout.Controls.Add(card, 0, 1);
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 7 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); card.Controls.Add(fields);
        Add(fields, 0, "Pharmacy name *", _name); Add(fields, 1, "Address", _address); Add(fields, 2, "Phone", _phone); Add(fields, 3, "Currency code", _currency); Add(fields, 4, "Invoice prefix", _prefix); Add(fields, 5, "Expiry alert days", _expiryDays);
        var save = DataPage.ActionButton("Save settings", true); save.Click += async (_, _) => await SaveAsync(); fields.Controls.Add(save, 1, 6);
        Load += async (_, _) => await LoadSettingsAsync();
    }
    private async Task LoadSettingsAsync()
    {
        try { var s = await _service.GetSettingsAsync(); _name.Text = s.PharmacyName; _address.Text = s.Address ?? ""; _phone.Text = s.Phone ?? ""; _currency.Text = s.CurrencyCode; _prefix.Text = s.InvoicePrefix; _expiryDays.Value = Math.Clamp(s.ExpiryAlertDays, (int)_expiryDays.Minimum, (int)_expiryDays.Maximum); }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
    private async Task SaveAsync()
    {
        try
        {
            await _service.SaveSettingsAsync(new PharmacySettings { Id = 1, PharmacyName = _name.Text, Address = _address.Text, Phone = _phone.Text, CurrencyCode = _currency.Text, InvoicePrefix = _prefix.Text, ExpiryAlertDays = (int)_expiryDays.Value });
            MessageBox.Show("Settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
    private static void Add(TableLayoutPanel table, int row, string label, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = PharmacyColors.Text }, 0, row); input.Dock = DockStyle.Fill; table.Controls.Add(input, 1, row);
    }
}

public sealed class BackupPage : UserControl
{
    private readonly IDatabaseBackupService _backup;
    private readonly Label _status = new() { AutoSize = true, ForeColor = PharmacyColors.Muted };
    public BackupPage(IDatabaseBackupService backup)
    {
        _backup = backup; Dock = DockStyle.Fill; BackColor = PharmacyColors.Background;
        var heading = new Label { Text = "Backup & restore", AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text };
        var description = new Label { Text = "Create a consistent SQLite backup or restore a validated backup file.", AutoSize = true, Location = new Point(2, 44), ForeColor = PharmacyColors.Muted };
        var create = DataPage.ActionButton("Create backup…", true); create.Location = new Point(0, 100);
        var restore = DataPage.ActionButton("Restore backup…"); restore.Location = new Point(170, 100);
        _status.Location = new Point(0, 158);
        Controls.Add(heading); Controls.Add(description); Controls.Add(create); Controls.Add(restore); Controls.Add(_status);
        create.Click += async (_, _) => await CreateAsync(); restore.Click += async (_, _) => await RestoreAsync();
    }
    private async Task CreateAsync()
    {
        using var dialog = new SaveFileDialog { Filter = "SQLite backup (*.db)|*.db|All files (*.*)|*.*", FileName = $"pharmacy-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try { await _backup.CreateBackupAsync(dialog.FileName); _status.Text = $"Backup created and checked: {dialog.FileName}"; }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
    private async Task RestoreAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "SQLite backup (*.db;*.bak)|*.db;*.bak|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (!DataPage.Confirm("Restore this backup? The current database will first be saved as a safety backup.")) return;
        try { await _backup.RestoreBackupAsync(dialog.FileName); _status.Text = "Database restored. Restart the application to refresh all open views."; }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
}
