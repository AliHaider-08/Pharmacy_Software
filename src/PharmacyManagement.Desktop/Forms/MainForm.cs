using PharmacyManagement.Application.Abstractions;

namespace PharmacyManagement.Desktop.Forms;

public sealed class MainForm : Form
{
    private static readonly Color SidebarColor = Color.FromArgb(20, 40, 63);
    private static readonly Color Workspace = Color.FromArgb(244, 246, 248);
    private readonly IDashboardService _dashboard;
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly ISalesService _sales;
    private readonly IBackOfficeService _backOffice;
    private readonly IDatabaseBackupService _backup;
    private readonly IReportService _reports;
    private readonly IUserService _userService;
    private readonly IUserSession _session;
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Workspace };
    private readonly Label _pageTitle = new() { AutoSize = true, Location = new Point(0, 2), ForeColor = PharmacyColors.Text, Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold) };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(35, 134, 91), Text = "●  Local database ready", Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Dictionary<string, Button> _navigation = new();

    public MainForm(IDashboardService dashboard, ICatalogService catalog, IInventoryService inventory, ISalesService sales, IBackOfficeService backOffice, IDatabaseBackupService backup, IReportService reports, IUserService userService, IUserSession session)
    {
        _dashboard = dashboard; _catalog = catalog; _inventory = inventory; _sales = sales; _backOffice = backOffice; _backup = backup; _reports = reports; _userService = userService; _session = session;
        Text = "Pharmacy Management"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(1060, 700); Size = new Size(1440, 900); BackColor = Workspace; Font = new Font("Segoe UI", 9.5f);
        BuildLayout(); Navigate("Dashboard");
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Workspace };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(shell);
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = SidebarColor, Padding = new Padding(18, 24, 18, 18) }; shell.Controls.Add(sidebar, 0, 0);
        sidebar.Controls.Add(new Label { Text = "✚  PHARMACY\n     MANAGEMENT", Dock = DockStyle.Top, Height = 76, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        sidebar.Controls.Add(new Label { Text = "WORKSPACE", Dock = DockStyle.Top, Height = 44, ForeColor = Color.FromArgb(151, 171, 193), Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(12, 0, 0, 8) });
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 570, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = SidebarColor }; sidebar.Controls.Add(nav);
        AddNav(nav, "Dashboard", "▦"); AddNav(nav, "Medicines", "▤"); AddNav(nav, "Sales / POS", "▣");
        if (_session.Current?.Role is "Administrator" or "Manager")
        {
            AddNav(nav, "Categories", "▧"); AddNav(nav, "Inventory", "▥"); AddNav(nav, "Purchases", "▥"); AddNav(nav, "Suppliers", "♙"); AddNav(nav, "Customers", "♧"); AddNav(nav, "Expenses", "◷"); AddNav(nav, "Reports", "▧");
        }
        if (_session.Current?.Role == "Administrator") { AddNav(nav, "Backup & restore", "↻"); AddNav(nav, "Settings", "⚙"); AddNav(nav, "User management", "♙"); }
        sidebar.Controls.Add(new Label { Text = "LOCAL · OFFLINE", Dock = DockStyle.Bottom, Height = 34, ForeColor = Color.FromArgb(151, 171, 193), Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(30, 20, 30, 24), BackColor = Workspace };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.Controls.Add(content, 1, 0);
        var header = new Panel { Dock = DockStyle.Fill }; content.Controls.Add(header, 0, 0); header.Controls.Add(_pageTitle);
        header.Controls.Add(new Label { Text = DateTime.Now.ToString("dddd, dd MMMM yyyy · h:mm tt"), AutoSize = true, Location = new Point(2, 43), ForeColor = PharmacyColors.Muted, Font = new Font("Segoe UI", 9f) });
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right; _status.Location = new Point(720, 25); header.Controls.Add(_status); content.Controls.Add(_pageHost, 0, 1);
        var account = new Label { Text = $"{_session.Current?.Username} · {_session.Current?.Role}", AutoSize = true, Location = new Point(500, 26), ForeColor = PharmacyColors.Navy, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        header.Controls.Add(account);
        var changePassword = new LinkLabel { Text = "Change password", AutoSize = true, Location = new Point(500, 48), LinkColor = PharmacyColors.Teal };
        changePassword.Click += async (_, _) => await ChangePasswordAsync(); header.Controls.Add(changePassword);
        var logout = new LinkLabel { Text = "Log out", AutoSize = true, Location = new Point(920, 25), LinkColor = Color.FromArgb(217, 83, 79), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        logout.Click += (_, _) => { if (MessageBox.Show("Log out of Pharmacy Management?", "Confirm logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Close(); };
        header.Controls.Add(logout);
    }

    private void AddNav(FlowLayoutPanel panel, string page, string icon)
    {
        var button = new Button { Text = $"{icon}   {page}", Width = 208, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = SidebarColor, ForeColor = Color.FromArgb(202, 214, 228), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.5f), Padding = new Padding(12, 0, 0, 0), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => Navigate(page); _navigation[page] = button; panel.Controls.Add(button);
    }

    private static void AddPlannedNav(FlowLayoutPanel panel, string page)
    {
        var button = new Button { Text = $"·   {page}  ·  next", Width = 208, Height = 39, FlatStyle = FlatStyle.Flat, BackColor = SidebarColor, ForeColor = Color.FromArgb(116, 137, 158), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(12, 0, 0, 0), Enabled = false };
        button.FlatAppearance.BorderSize = 0; panel.Controls.Add(button);
    }

    private void Navigate(string page)
    {
        foreach (var item in _navigation)
        {
            var selected = item.Key == page; item.Value.BackColor = selected ? Color.FromArgb(36, 91, 143) : SidebarColor; item.Value.ForeColor = selected ? Color.White : Color.FromArgb(202, 214, 228); item.Value.Font = new Font("Segoe UI", 9.5f, selected ? FontStyle.Bold : FontStyle.Regular);
        }
        _pageTitle.Text = page;
        foreach (Control oldPage in _pageHost.Controls.Cast<Control>().ToList()) { _pageHost.Controls.Remove(oldPage); oldPage.Dispose(); }
        UserControl control = page switch
        {
            "Medicines" => new MedicinesPage(_catalog),
            "Categories" => new CategoriesPage(_catalog),
            "Inventory" => new InventoryPage(_catalog, _inventory),
            "Sales / POS" => new PointOfSalePage(_catalog, _inventory, _sales, _backOffice),
            "Purchases" => new PurchasesPage(_backOffice, _catalog),
            "Suppliers" => new SuppliersPage(_backOffice),
            "Customers" => new CustomersPage(_backOffice),
            "Expenses" => new ExpensesPage(_backOffice),
            "Reports" => new ReportsPage(_reports),
            "Backup & restore" => new BackupPage(_backup),
            "Settings" => new SettingsPage(_backOffice),
            "User management" when _session.Current?.Role == "Administrator" => new UserManagementPage(_userService, _session),
            _ => new DashboardPage(_dashboard, _backOffice)
        };
        _pageHost.Controls.Add(control); _status.Text = "●  Live data · SQLite";
    }

    private async Task ChangePasswordAsync()
    {
        if (_session.Current is null) return;
        using var dialog = new ChangePasswordDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { await _userService.ChangePasswordAsync(_session.Current.Id, dialog.CurrentPassword, dialog.NewPassword); MessageBox.Show("Password changed."); }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }
}
