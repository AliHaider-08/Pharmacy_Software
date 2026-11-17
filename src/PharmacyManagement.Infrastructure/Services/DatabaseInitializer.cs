using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class DatabaseInitializer(PharmacyDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        // Phase 1 creates the initial schema. Once migration history is introduced,
        // this switches to MigrateAsync so upgrades preserve existing pharmacy data.
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await UpgradeSettingsSchemaAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        logger.LogInformation("Local pharmacy database is ready at {DatabasePath}", db.Database.GetDbConnection().DataSource);
    }

    private async Task UpgradeSettingsSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info('Settings');";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) existingColumns.Add(reader.GetString(1));
            }

            var additions = new (string Name, string Sql)[]
            {
                ("LogoPath", "ALTER TABLE Settings ADD COLUMN LogoPath TEXT NULL;"),
                ("ShowDashboardSales", "ALTER TABLE Settings ADD COLUMN ShowDashboardSales INTEGER NOT NULL DEFAULT 1;"),
                ("ShowDashboardMedicines", "ALTER TABLE Settings ADD COLUMN ShowDashboardMedicines INTEGER NOT NULL DEFAULT 1;"),
                ("ShowDashboardStock", "ALTER TABLE Settings ADD COLUMN ShowDashboardStock INTEGER NOT NULL DEFAULT 1;"),
                ("ShowDashboardLowStock", "ALTER TABLE Settings ADD COLUMN ShowDashboardLowStock INTEGER NOT NULL DEFAULT 1;"),
                ("ShowDashboardExpiry", "ALTER TABLE Settings ADD COLUMN ShowDashboardExpiry INTEGER NOT NULL DEFAULT 1;")
            };

            foreach (var (name, sql) in additions)
            {
                if (existingColumns.Contains(name)) continue;
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Added missing Settings.{Column} column", name);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
