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
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(35, 134, 91), Text = "●  Local database ready", Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Dictionary<string, Button> _navigation = new();
    private FlowLayoutPanel _navigationPanel = null!;
    private string _workspace = "Sales";

    public MainForm(IDashboardService dashboard, ICatalogService catalog, IInventoryService inventory, ISalesService sales, IBackOfficeService backOffice, IDatabaseBackupService backup, IReportService reports, IUserService userService, IUserSession session)
    {
        _dashboard = dashboard; _catalog = catalog; _inventory = inventory; _sales = sales; _backOffice = backOffice; _backup = backup; _reports = reports; _userService = userService; _session = session;
        Text = "Pharmacy Management"; StartPosition = FormStartPosition.CenterScreen; WindowState = FormWindowState.Maximized; MinimumSize = new Size(1060, 700); Size = new Size(1440, 900); BackColor = Workspace; Font = new Font("Segoe UI", 9.5f);
        BuildLayout();
        BuildSalesNavigation();
        Navigate("Sales / POS");
    }

    private void BuildLayout()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Workspace };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244)); shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(shell);
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = SidebarColor, Padding = new Padding(16, 14, 16, 12) }; shell.Controls.Add(sidebar, 0, 0);
        var sidebarLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = SidebarColor };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        sidebar.Controls.Add(sidebarLayout);
        sidebarLayout.Controls.Add(new Label { Text = "✚  SIMPLE PHARMACY", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var workspaceButton = new Button { Text = "Sales workspace  ▾", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 91, 143), ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Cursor = Cursors.Hand };
        workspaceButton.FlatAppearance.BorderSize = 0;
        workspaceButton.Text = _session.Current?.Role == "Cashier" ? "Sales workspace" : "Enter Admin workspace 🔒";
        workspaceButton.Enabled = _session.Current?.Role is "Administrator" or "Manager";
        workspaceButton.Click += async (_, _) => await ToggleWorkspaceAsync(workspaceButton);
        sidebarLayout.Controls.Add(workspaceButton, 0, 1);
        _navigationPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = SidebarColor, Margin = Padding.Empty, Padding = Padding.Empty };
        sidebarLayout.Controls.Add(_navigationPanel, 0, 2);
        sidebarLayout.Controls.Add(new Label { Text = "LOCAL · OFFLINE", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(151, 171, 193), Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 3);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(30, 20, 30, 24), BackColor = Workspace };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.Controls.Add(content, 1, 0);
        var header = new Panel { Dock = DockStyle.Fill }; content.Controls.Add(header, 0, 0);
        header.Controls.Add(new Label { Text = DateTime.Now.ToString("dddd, dd MMMM yyyy · h:mm tt"), AutoSize = true, Location = new Point(2, 26), ForeColor = PharmacyColors.Muted, Font = new Font("Segoe UI", 9f) });
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Right; _status.Location = new Point(720, 25); header.Controls.Add(_status); content.Controls.Add(_pageHost, 0, 1);
        var account = new Label { Text = $"{_session.Current?.Username} · {_session.Current?.Role}", AutoSize = true, Location = new Point(500, 26), ForeColor = PharmacyColors.Navy, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        header.Controls.Add(account);
        var changePassword = new LinkLabel { Text = "Change password", AutoSize = true, Location = new Point(500, 48), LinkColor = PharmacyColors.Teal };
        changePassword.Click += async (_, _) => await ChangePasswordAsync(); header.Controls.Add(changePassword);
        var logout = new LinkLabel { Text = "Log out", AutoSize = true, Location = new Point(920, 25), LinkColor = Color.FromArgb(217, 83, 79), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        logout.Click += (_, _) => { if (MessageBox.Show("Log out of Pharmacy Management?", "Confirm logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Close(); };
        header.Controls.Add(logout);
    }

    private void BuildSalesNavigation()
    {
        _workspace = "Sales";
        _navigation.Clear();
        _navigationPanel.Controls.Clear();
        AddNav(_navigationPanel, "Sales / POS", "▣");
    }

    private void BuildAdminNavigation()
    {
        _workspace = "Admin";
        _navigation.Clear();
        _navigationPanel.Controls.Clear();
        AddNav(_navigationPanel, "Dashboard", "▦");
        AddNav(_navigationPanel, "Medicines", "▤");
        AddNav(_navigationPanel, "Inventory", "▥");
        AddNav(_navigationPanel, "Reports", "▧");
        if (_session.Current?.Role == "Administrator")
        {
            AddNav(_navigationPanel, "Settings", "⚙");
            AddNav(_navigationPanel, "Database", "▣");
            AddNav(_navigationPanel, "User management", "♙");
        }
        AddNav(_navigationPanel, "Sales / POS", "▣");
    }

    private async Task ToggleWorkspaceAsync(Button workspaceButton)
    {
        if (_session.Current?.Role is not ("Administrator" or "Manager")) return;
        if (_workspace == "Sales")
        {
            using var unlock = new AdminWorkspacePasswordDialog(_session.Current.Username);
            if (unlock.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                if (!await _userService.VerifyCurrentPasswordAsync(unlock.Password))
                {
                    MessageBox.Show("The password is incorrect. Admin workspace remains locked.", "Access denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex) { DataPage.ShowError(ex); return; }
            BuildAdminNavigation(); workspaceButton.Text = "Admin workspace · unlocked ▾"; Navigate("Dashboard");
        }
        else { BuildSalesNavigation(); workspaceButton.Text = "Sales workspace  ▾"; Navigate("Sales / POS"); }
    }

    private void AddNav(FlowLayoutPanel panel, string page, string icon)
    {
        var button = new Button { Text = $"{icon}   {page}", Width = 184, Height = 36, Margin = new Padding(0, 2, 0, 2), FlatStyle = FlatStyle.Flat, BackColor = SidebarColor, ForeColor = Color.FromArgb(202, 214, 228), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f), Padding = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; button.Click += (_, _) => Navigate(page); _navigation[page] = button; panel.Controls.Add(button);
    }

    private static void AddPlannedNav(FlowLayoutPanel panel, string page)
    {
        var button = new Button { Text = $"·   {page}  ·  next", Width = 208, Height = 39, FlatStyle = FlatStyle.Flat, BackColor = SidebarColor, ForeColor = Color.FromArgb(116, 137, 158), TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f), Padding = new Padding(12, 0, 0, 0), Enabled = false };
        button.FlatAppearance.BorderSize = 0; panel.Controls.Add(button);
    }

    private void Navigate(string page)
    {
        var role = _session.Current?.Role;
        if (page is "Settings" or "Database" or "User management" && role != "Administrator") page = "Dashboard";
        if (page is "Inventory" or "Reports" && role is not ("Administrator" or "Manager")) page = "Sales / POS";
        if (page is "Dashboard" or "Medicines" && role == "Cashier") page = "Sales / POS";
        if (_workspace == "Sales" && page != "Sales / POS") page = "Sales / POS";
        foreach (var item in _navigation)
        {
            var selected = item.Key == page; item.Value.BackColor = selected ? Color.FromArgb(36, 91, 143) : SidebarColor; item.Value.ForeColor = selected ? Color.White : Color.FromArgb(202, 214, 228); item.Value.Font = new Font("Segoe UI", 9.5f, selected ? FontStyle.Bold : FontStyle.Regular);
        }
        foreach (Control oldPage in _pageHost.Controls.Cast<Control>().ToList()) { _pageHost.Controls.Remove(oldPage); oldPage.Dispose(); }
        UserControl control = page switch
        {
            "Medicines" => new MedicinesPage(_catalog),
            "Inventory" => new InventoryPage(_catalog, _inventory),
            "Sales / POS" => new PointOfSalePage(_catalog, _inventory, _sales, _backOffice),
            "Settings" when _session.Current?.Role == "Administrator" => new SettingsPage(_backOffice),
            "Database" when _session.Current?.Role == "Administrator" => new DatabaseInfoPage(_backup),
            "Reports" when _session.Current?.Role is "Administrator" or "Manager" => new ReportsPage(_reports),
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
