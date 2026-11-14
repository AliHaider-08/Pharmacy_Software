using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Desktop.Forms;
using PharmacyManagement.Infrastructure.Data;
using PharmacyManagement.Infrastructure.Services;

namespace PharmacyManagement.Desktop;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        ApplicationConfiguration.Initialize();
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PharmacyManagement", "Database", "pharmacy.db");
                services.AddDbContext<PharmacyDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
                services.AddScoped<DatabaseInitializer>();
                services.AddScoped<IDashboardService, DashboardService>();
                services.AddScoped<ICatalogService, CatalogService>();
                services.AddScoped<IInventoryService, InventoryService>();
                services.AddScoped<ISalesService, SalesService>();
                services.AddScoped<IBackOfficeService, BackOfficeService>();
                services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
                services.AddScoped<IReportService, ReportService>();
                services.AddScoped<IUserService, UserService>();
                services.AddSingleton<IUserSession, UserSession>();
                services.AddTransient<MainForm>();
            }).Build();

        try
        {
            await using (var scope = host.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

            await using (var authScope = host.Services.CreateAsyncScope())
            {
                var users = authScope.ServiceProvider.GetRequiredService<IUserService>();
                var session = authScope.ServiceProvider.GetRequiredService<IUserSession>();
                if (!await users.HasUsersAsync())
                {
                    using var setup = new FirstRunSetupDialog();
                    if (setup.ShowDialog() != DialogResult.OK) return;
                    await users.CreateInitialAdministratorAsync(setup.Username, setup.Password);
                    session.Current = await users.AuthenticateAsync(setup.Username, setup.Password);
                }
                else
                {
                    using var login = new LoginDialog(users);
                    if (login.ShowDialog() != DialogResult.OK || login.User is null) return;
                    session.Current = login.User;
                }
            }
            System.Windows.Forms.Application.Run(host.Services.GetRequiredService<MainForm>());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Pharmacy Management could not start.\n\n{ex.Message}", "Startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

