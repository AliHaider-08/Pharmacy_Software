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
    private readonly TextBox _logoPath = new();
    private readonly TextBox _currency = new();
    private readonly TextBox _prefix = new();
    private readonly NumericUpDown _expiryDays = new() { Minimum = 0, Maximum = 3650 };
    private readonly CheckBox _showSales = new() { Text = "Today's sales", AutoSize = true, Checked = true };
    private readonly CheckBox _showMedicines = new() { Text = "Medicine count", AutoSize = true, Checked = true };
    private readonly CheckBox _showStock = new() { Text = "Stock on hand", AutoSize = true, Checked = true };
    private readonly CheckBox _showLowStock = new() { Text = "Low stock alerts", AutoSize = true, Checked = true };
    private readonly CheckBox _showExpiry = new() { Text = "Near expiry alerts", AutoSize = true, Checked = true };

    public SettingsPage(IBackOfficeService service)
    {
        _service = service; Dock = DockStyle.Fill; BackColor = PharmacyColors.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = PharmacyColors.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); Controls.Add(layout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "Settings", AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text });
        heading.Controls.Add(new Label { Text = "Manage your pharmacy details and choose what appears on the dashboard.", AutoSize = true, Location = new Point(2, 44), ForeColor = PharmacyColors.Muted });
        layout.Controls.Add(heading, 0, 0);
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 0, 12, 12), BackColor = PharmacyColors.Background };
        layout.Controls.Add(body, 0, 1);
        var card = new Panel { Width = 820, Height = 410, BackColor = Color.White, Padding = new Padding(22), Margin = new Padding(0, 0, 0, 16) };
        body.Controls.Add(card);
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 8 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); card.Controls.Add(fields);
        fields.Controls.Add(new Label { Text = "Pharmacy details", AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = PharmacyColors.Navy, Margin = new Padding(0, 0, 0, 8) }, 0, 0); fields.SetColumnSpan(fields.GetControlFromPosition(0, 0)!, 2);
        Add(fields, 1, "Pharmacy name *", _name); Add(fields, 2, "Address", _address); Add(fields, 3, "Phone", _phone);
        var logoRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _logoPath.Width = 520; _logoPath.ReadOnly = true;
        var chooseLogo = DataPage.ActionButton("Choose logo…");
        chooseLogo.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = "Logo image (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*", CheckFileExists = true }; if (dialog.ShowDialog(FindForm()) == DialogResult.OK) _logoPath.Text = dialog.FileName; };
        logoRow.Controls.Add(_logoPath); logoRow.Controls.Add(chooseLogo);
        Add(fields, 4, "Receipt logo", logoRow); Add(fields, 5, "Currency code", _currency); Add(fields, 6, "Invoice prefix", _prefix); Add(fields, 7, "Expiry alert days", _expiryDays);
        var dashboardCard = new Panel { Width = 820, Height = 220, BackColor = Color.White, Padding = new Padding(22), Margin = new Padding(0, 0, 0, 16) };
        body.Controls.Add(dashboardCard);
        var dashboardLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        dashboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); dashboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); dashboardCard.Controls.Add(dashboardLayout);
        var dashboardHeading = new Label { Text = "Dashboard display", AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = PharmacyColors.Navy, Margin = new Padding(0, 0, 0, 8) };
        dashboardLayout.Controls.Add(dashboardHeading, 0, 0); dashboardLayout.SetColumnSpan(dashboardHeading, 2);
        dashboardLayout.Controls.Add(_showSales, 0, 1); dashboardLayout.Controls.Add(_showMedicines, 1, 1);
        dashboardLayout.Controls.Add(_showStock, 0, 2); dashboardLayout.Controls.Add(_showLowStock, 1, 2);
        dashboardLayout.Controls.Add(_showExpiry, 0, 3);
        var save = DataPage.ActionButton("Save settings", true); save.Click += async (_, _) => await SaveAsync();
        dashboardLayout.Controls.Add(save, 1, 3);
        Load += async (_, _) => await LoadSettingsAsync();
    }
    private async Task LoadSettingsAsync()
    {
        try { var s = await _service.GetSettingsAsync(); _name.Text = s.PharmacyName; _address.Text = s.Address ?? ""; _phone.Text = s.Phone ?? ""; _logoPath.Text = s.LogoPath ?? ""; _currency.Text = s.CurrencyCode; _prefix.Text = s.InvoicePrefix; _expiryDays.Value = Math.Clamp(s.ExpiryAlertDays, (int)_expiryDays.Minimum, (int)_expiryDays.Maximum); _showSales.Checked = s.ShowDashboardSales; _showMedicines.Checked = s.ShowDashboardMedicines; _showStock.Checked = s.ShowDashboardStock; _showLowStock.Checked = s.ShowDashboardLowStock; _showExpiry.Checked = s.ShowDashboardExpiry; }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
    private async Task SaveAsync()
    {
        try
        {
            await _service.SaveSettingsAsync(new PharmacySettings { Id = 1, PharmacyName = _name.Text, Address = _address.Text, Phone = _phone.Text, LogoPath = _logoPath.Text, CurrencyCode = _currency.Text, InvoicePrefix = _prefix.Text, ExpiryAlertDays = (int)_expiryDays.Value, ShowDashboardSales = _showSales.Checked, ShowDashboardMedicines = _showMedicines.Checked, ShowDashboardStock = _showStock.Checked, ShowDashboardLowStock = _showLowStock.Checked, ShowDashboardExpiry = _showExpiry.Checked });
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

public sealed class DatabaseInfoPage : UserControl
{
    private readonly IDatabaseBackupService _database;
    private readonly Label _path = new() { AutoSize = false, Width = 700, Height = 44, ForeColor = PharmacyColors.Text };
    private readonly Label _connection = new() { AutoSize = true, ForeColor = PharmacyColors.Muted, Text = "Checking connection…" };
    private readonly Label _fileInfo = new() { AutoSize = true, ForeColor = PharmacyColors.Muted };

    public DatabaseInfoPage(IDatabaseBackupService database)
    {
        _database = database;
        Dock = DockStyle.Fill;
        BackColor = PharmacyColors.Background;
        var heading = new Label { Text = "Database connection", AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text };
        var description = new Label { Text = "Check where pharmacy records are stored and verify the local database connection.", AutoSize = true, Location = new Point(2, 44), ForeColor = PharmacyColors.Muted };
        var card = new Panel { Location = new Point(0, 86), Size = new Size(780, 260), BackColor = Color.White, Padding = new Padding(22) };
        card.Controls.Add(new Label { Text = "Database type", AutoSize = true, Location = new Point(22, 22), ForeColor = PharmacyColors.Muted });
        card.Controls.Add(new Label { Text = "SQLite · local file · works offline", AutoSize = true, Location = new Point(22, 45), ForeColor = PharmacyColors.Navy, Font = new Font("Segoe UI", 10f, FontStyle.Bold) });
        card.Controls.Add(new Label { Text = "Database file", AutoSize = true, Location = new Point(22, 82), ForeColor = PharmacyColors.Muted });
        _path.Location = new Point(22, 106); card.Controls.Add(_path);
        _connection.Location = new Point(22, 162); card.Controls.Add(_connection);
        _fileInfo.Location = new Point(22, 188); card.Controls.Add(_fileInfo);
        var check = DataPage.ActionButton("Check connection", true); check.Location = new Point(22, 216);
        var folder = DataPage.ActionButton("Open database folder"); folder.Location = new Point(184, 216);
        var copy = DataPage.ActionButton("Copy file path"); copy.Location = new Point(384, 216);
        card.Controls.Add(check); card.Controls.Add(folder); card.Controls.Add(copy);
        Controls.Add(heading); Controls.Add(description); Controls.Add(card);
        check.Click += async (_, _) => await CheckAsync();
        folder.Click += (_, _) => OpenFolder();
        copy.Click += (_, _) => { Clipboard.SetText(_database.DatabasePath); MessageBox.Show("Database file path copied.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information); };
        Load += async (_, _) => { _path.Text = _database.DatabasePath; RefreshFileInfo(); await CheckAsync(); };
    }

    private void RefreshFileInfo()
    {
        var file = new FileInfo(_database.DatabasePath);
        _fileInfo.Text = file.Exists ? $"File exists · {file.Length / 1024d:N1} KB · last updated {file.LastWriteTime:dd MMM yyyy h:mm tt}" : "Database file has not been created yet.";
    }

    private async Task CheckAsync()
    {
        _connection.Text = "Checking database connection…";
        try
        {
            await _database.CheckConnectionAsync();
            _connection.Text = "● Connected · database query succeeded";
            _connection.ForeColor = Color.FromArgb(35, 134, 91);
        }
        catch (Exception ex)
        {
            _connection.Text = $"● Connection failed · {ex.Message}";
            _connection.ForeColor = Color.FromArgb(190, 55, 55);
        }
        RefreshFileInfo();
    }

    private void OpenFolder()
    {
        var directory = Path.GetDirectoryName(_database.DatabasePath);
        if (directory is null || !Directory.Exists(directory)) { MessageBox.Show("The database folder does not exist yet."); return; }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }
}
