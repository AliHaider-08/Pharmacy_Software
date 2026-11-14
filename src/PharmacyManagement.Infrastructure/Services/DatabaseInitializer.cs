using Microsoft.EntityFrameworkCore;
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
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        logger.LogInformation("Local pharmacy database is ready at {DatabasePath}", db.Database.GetDbConnection().DataSource);
    }
}
