using PharmacyManagement.Application.Abstractions;

namespace PharmacyManagement.Desktop.Forms;

public sealed class FirstRunSetupDialog : Form
{
    private readonly TextBox _username = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly TextBox _confirm = new() { UseSystemPasswordChar = true };
    public string Username => _username.Text.Trim();
    public string Password => _password.Text;

    public FirstRunSetupDialog()
    {
        Text = "Set up Pharmacy Management"; StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; ClientSize = new Size(500, 360); MaximizeBox = false; MinimizeBox = false; Font = new Font("Segoe UI", 9.5f);
        BackColor = PharmacyColors.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(28) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); Controls.Add(layout);
        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label { Text = "Create your administrator", AutoSize = true, ForeColor = PharmacyColors.Text, Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold), Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "This account controls access to your local pharmacy data.", AutoSize = true, ForeColor = PharmacyColors.Muted, Location = new Point(1, 39) });
        layout.Controls.Add(heading, 0, 0);
        AddRow(layout, 1, "Username", _username); AddRow(layout, 2, "Password", _password); AddRow(layout, 3, "Confirm password", _confirm);
        var setup = DataPage.ActionButton("Create account", true); setup.Dock = DockStyle.Bottom; setup.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Username) || Username.Length < 3) { MessageBox.Show("Username must contain at least 3 characters."); return; }
            if (Password.Length < 10) { MessageBox.Show("Use a password with at least 10 characters."); return; }
            if (Password != _confirm.Text) { MessageBox.Show("The passwords do not match."); return; }
            DialogResult = DialogResult.OK; Close();
        };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 54 }; footer.Controls.Add(setup); Controls.Add(footer);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control field)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); field.Dock = DockStyle.Fill; panel.Controls.Add(field, 1, 0); layout.Controls.Add(panel, 0, row);
    }
}

public sealed class LoginDialog : Form
{
    private readonly IUserService _users;
    private readonly TextBox _username = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    public AuthenticatedUser? User { get; private set; }

    public LoginDialog(IUserService users)
    {
        _users = users;
        Text = "Sign in · Pharmacy Management"; StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.FixedDialog; ClientSize = new Size(450, 280); MaximizeBox = false; MinimizeBox = false; Font = new Font("Segoe UI", 9.5f); BackColor = PharmacyColors.Background;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(28) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        var title = new Label { Text = "Welcome back", AutoSize = true, ForeColor = PharmacyColors.Text, Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold) }; layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
        Add(layout, 1, "Username", _username); Add(layout, 2, "Password", _password);
        var login = DataPage.ActionButton("Sign in", true); login.Anchor = AnchorStyles.Right; layout.Controls.Add(login, 1, 3); login.Click += async (_, _) => await AuthenticateAsync();
        AcceptButton = login;
    }

    private async Task AuthenticateAsync()
    {
        try
        {
            User = await _users.AuthenticateAsync(_username.Text, _password.Text);
            if (User is null) { MessageBox.Show("Username or password is incorrect, or this account is inactive.", "Sign-in failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); _password.Clear(); _password.Focus(); return; }
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { DataPage.ShowError(ex); }
    }

    internal static void Add(TableLayoutPanel table, int row, string label, Control field) { table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); field.Dock = DockStyle.Fill; table.Controls.Add(field, 1, row); }
}

public sealed class UserManagementPage : DataPage
{
    private readonly IUserService _users;
    private readonly IUserSession _session;
    public UserManagementPage(IUserService users, IUserSession session) : base("User management", "Create staff accounts, assign roles and deactivate access.")
    {
        _users = users; _session = session;
        var add = ActionButton("+  Add user", true); var toggle = ActionButton("Activate / deactivate"); var password = ActionButton("Change my password");
        Toolbar.Controls.Add(add); Toolbar.Controls.Add(toggle); Toolbar.Controls.Add(password);
        add.Click += async (_, _) => await AddUserAsync(); toggle.Click += async (_, _) => await ToggleAsync(); password.Click += async (_, _) => await ChangePasswordAsync();
        Load += async (_, _) => await RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        try
        {
            Grid.DataSource = await _users.GetUsersAsync();
            if (Grid.Columns.Count == 0) return;
            Grid.Columns[nameof(UserListItem.Id)]!.Visible = false;
            Grid.Columns[nameof(UserListItem.Username)]!.HeaderText = "Username"; Grid.Columns[nameof(UserListItem.Role)]!.HeaderText = "Role"; Grid.Columns[nameof(UserListItem.IsActive)]!.HeaderText = "Active"; Grid.Columns[nameof(UserListItem.CreatedAtLocal)]!.HeaderText = "Created";
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task AddUserAsync()
    {
        using var dialog = new AccountDialog(); if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try { await _users.CreateUserAsync(dialog.Username, dialog.Password, dialog.Role); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ToggleAsync()
    {
        if (Grid.CurrentRow?.DataBoundItem is not UserListItem user) { MessageBox.Show("Select a user first."); return; }
        if (user.Id == _session.Current?.Id) { MessageBox.Show("You cannot deactivate your own active session."); return; }
        if (!Confirm($"{(user.IsActive ? "Deactivate" : "Activate")} {user.Username}?")) return;
        try { await _users.SetUserActiveAsync(user.Id, !user.IsActive); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ChangePasswordAsync()
    {
        if (_session.Current is null) return;
        using var dialog = new ChangePasswordDialog(); if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try { await _users.ChangePasswordAsync(_session.Current.Id, dialog.CurrentPassword, dialog.NewPassword); MessageBox.Show("Password changed successfully."); }
        catch (Exception ex) { ShowError(ex); }
    }
}

internal sealed class AccountDialog : Form
{
    private readonly TextBox _username = new(); private readonly TextBox _password = new() { UseSystemPasswordChar = true }; private readonly ComboBox _role = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    public string Username => _username.Text.Trim(); public string Password => _password.Text; public string Role => Convert.ToString(_role.SelectedItem) ?? "Cashier";
    public AccountDialog()
    {
        Text = "Create staff account"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; ClientSize = new Size(440, 240); MaximizeBox = false; MinimizeBox = false;
        _role.Items.AddRange(["Administrator", "Manager", "Cashier"]); _role.SelectedIndex = 2;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(18) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        LoginDialog.Add(layout, 0, "Username", _username); LoginDialog.Add(layout, 1, "Password", _password); LoginDialog.Add(layout, 2, "Role", _role);
        var save = DataPage.ActionButton("Create user", true); save.Anchor = AnchorStyles.Right; layout.Controls.Add(save, 1, 3); save.Click += (_, _) => { if (Username.Length < 3 || Password.Length < 10) { MessageBox.Show("Username needs 3 characters and password needs 10."); return; } DialogResult = DialogResult.OK; Close(); };
    }
}

internal sealed class ChangePasswordDialog : Form
{
    private readonly TextBox _current = new() { UseSystemPasswordChar = true }; private readonly TextBox _new = new() { UseSystemPasswordChar = true }; private readonly TextBox _confirm = new() { UseSystemPasswordChar = true };
    public string CurrentPassword => _current.Text; public string NewPassword => _new.Text;
    public ChangePasswordDialog()
    {
        Text = "Change password"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; ClientSize = new Size(460, 260); MaximizeBox = false; MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(18) }; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(layout);
        LoginDialog.Add(layout, 0, "Current", _current); LoginDialog.Add(layout, 1, "New", _new); LoginDialog.Add(layout, 2, "Confirm new", _confirm);
        var save = DataPage.ActionButton("Change password", true); save.Anchor = AnchorStyles.Right; layout.Controls.Add(save, 1, 3); save.Click += (_, _) => { if (NewPassword.Length < 10 || NewPassword != _confirm.Text) { MessageBox.Show("New passwords must match and contain at least 10 characters."); return; } DialogResult = DialogResult.OK; Close(); };
    }
}
