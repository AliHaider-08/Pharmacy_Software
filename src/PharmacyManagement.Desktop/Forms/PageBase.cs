using PharmacyManagement.Application.Abstractions;

namespace PharmacyManagement.Desktop.Forms;

internal static class PharmacyColors
{
    public static readonly Color Navy = Color.FromArgb(24, 49, 83);
    public static readonly Color Teal = Color.FromArgb(21, 154, 156);
    public static readonly Color Background = Color.FromArgb(244, 246, 248);
    public static readonly Color Text = Color.FromArgb(36, 52, 71);
    public static readonly Color Muted = Color.FromArgb(113, 128, 150);
}

public abstract class DataPage : UserControl
{
    protected readonly DataGridView Grid = new();
    protected readonly FlowLayoutPanel Toolbar = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 6, 0, 4) };
    protected readonly TextBox SearchBox = new() { Width = 260, PlaceholderText = "Search records…", Margin = new Padding(0, 8, 10, 4) };

    protected DataPage(string title, string description)
    {
        Dock = DockStyle.Fill;
        BackColor = PharmacyColors.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4, 2, 4, 4), BackColor = PharmacyColors.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = title, AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text });
        heading.Controls.Add(new Label { Text = description, AutoSize = true, Location = new Point(2, 44), Font = new Font("Segoe UI", 9f), ForeColor = PharmacyColors.Muted });
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(Toolbar, 0, 1);
        Grid.Dock = DockStyle.Fill;
        Grid.BackgroundColor = Color.White;
        Grid.BorderStyle = BorderStyle.None;
        Grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        Grid.GridColor = Color.FromArgb(232, 237, 242);
        Grid.EnableHeadersVisualStyles = false;
        Grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 250);
        Grid.ColumnHeadersDefaultCellStyle.ForeColor = PharmacyColors.Text;
        Grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Grid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        Grid.DefaultCellStyle.ForeColor = PharmacyColors.Text;
        Grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 240, 241);
        Grid.DefaultCellStyle.SelectionForeColor = PharmacyColors.Text;
        Grid.RowHeadersVisible = false;
        Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid.MultiSelect = false;
        Grid.AllowUserToAddRows = false;
        Grid.AllowUserToDeleteRows = false;
        Grid.ReadOnly = true;
        layout.Controls.Add(Grid, 0, 2);
    }

    public static Button ActionButton(string text, bool primary = false) => new()
    {
        Text = text, AutoSize = true, Height = 36, FlatStyle = FlatStyle.Flat,
        BackColor = primary ? PharmacyColors.Navy : Color.White,
        ForeColor = primary ? Color.White : PharmacyColors.Navy,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Padding = new Padding(12, 0, 12, 0), Margin = new Padding(0, 4, 8, 4), Cursor = Cursors.Hand
    };

    public static void ShowError(Exception ex) => MessageBox.Show(ex.Message, "Unable to complete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    public static bool Confirm(string message) => MessageBox.Show(message, "Confirm action", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}

public sealed class DashboardPage : UserControl
{
    private readonly IDashboardService _dashboard;
    private readonly IBackOfficeService _settings;
    private string _currencyCode = "PKR";
    private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, Padding = new Padding(0, 10, 8, 8), BackColor = PharmacyColors.Background };

    public DashboardPage(IDashboardService dashboard, IBackOfficeService settings)
    {
        _dashboard = dashboard;
        _settings = settings;
        Dock = DockStyle.Fill;
        BackColor = PharmacyColors.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = PharmacyColors.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "Dashboard", AutoSize = true, Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold), ForeColor = PharmacyColors.Text, Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "Live snapshot from your local pharmacy records", AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = PharmacyColors.Muted, Location = new Point(2, 44) });
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(_cards, 0, 1);
        Load += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            _currencyCode = (await _settings.GetSettingsAsync()).CurrencyCode;
            var s = await _dashboard.GetSummaryAsync();
            _cards.Controls.Clear();
            Add("Medicines", s.MedicineCount.ToString("N0"), "Active catalog", PharmacyColors.Navy);
            Add("Stock on hand", s.StockUnits.ToString("N0"), "Units across batches", PharmacyColors.Teal);
            Add("Sales today", $"{_currencyCode} {s.SalesToday:N2}", "Completed invoices", Color.FromArgb(35, 134, 91));
            Add("Purchases today", $"{_currencyCode} {s.PurchasesToday:N2}", "Supplier invoices", Color.FromArgb(36, 91, 143));
            Add("Gross profit", $"{_currencyCode} {s.ProfitToday:N2}", "Actual batch cost", Color.FromArgb(35, 134, 91));
            Add("Low stock", s.LowStockMedicineCount.ToString("N0"), "At or below minimum", Color.FromArgb(230, 162, 60));
            Add("Near expiry", s.ExpiringBatchCount.ToString("N0"), "Within 90 days", Color.FromArgb(217, 83, 79));
            Add("Suppliers", s.SupplierCount.ToString("N0"), "Registered suppliers", PharmacyColors.Navy);
            Add("Customers", s.CustomerCount.ToString("N0"), "Registered customers", PharmacyColors.Navy);
        }
        catch (Exception ex) { _cards.Controls.Clear(); Add("Dashboard error", "!", ex.Message, Color.FromArgb(217, 83, 79)); }
    }

    private void Add(string title, string value, string hint, Color accent)
    {
        var card = new Panel { Width = 276, Height = 158, BackColor = Color.White, Margin = new Padding(0, 0, 16, 16) };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, 4, card.Height);
        };
        card.Controls.Add(new Label { Text = title.ToUpperInvariant(), AutoSize = true, Location = new Point(20, 18), ForeColor = PharmacyColors.Muted, Font = new Font("Segoe UI", 8f, FontStyle.Bold) });
        card.Controls.Add(new Label { Text = value, AutoSize = true, Location = new Point(18, 48), ForeColor = PharmacyColors.Text, Font = new Font("Segoe UI Semibold", 25f, FontStyle.Bold) });
        card.Controls.Add(new Label { Text = hint, AutoSize = false, Width = 225, Height = 34, Location = new Point(20, 105), ForeColor = PharmacyColors.Muted, Font = new Font("Segoe UI", 8.5f) });
        _cards.Controls.Add(card);
    }
}
