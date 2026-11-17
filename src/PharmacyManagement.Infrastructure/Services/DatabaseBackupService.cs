using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PharmacyManagement.Application.Abstractions;
using PharmacyManagement.Infrastructure.Data;

namespace PharmacyManagement.Infrastructure.Services;

public sealed class DatabaseBackupService(PharmacyDbContext db, IUserSession session) : IDatabaseBackupService
{
    private readonly string _databasePath = db.Database.GetDbConnection().DataSource;
    public string DatabasePath => Path.GetFullPath(_databasePath);

    public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireAdministrator(session);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table';";
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0) throw new InvalidOperationException("The database is connected, but it has no application tables.");
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireAdministrator(session);
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new InvalidOperationException("Choose a backup file path.");
        var fullPath = Path.GetFullPath(destinationPath);
        if (string.Equals(fullPath, Path.GetFullPath(_databasePath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The backup must use a different path from the active database.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString());
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        await ValidateAsync(destination, cancellationToken);
    }

    public async Task RestoreBackupAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        UserAuthorization.RequireAdministrator(session);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Backup file was not found.", sourcePath);
        var fullPath = Path.GetFullPath(sourcePath);
        if (string.Equals(fullPath, Path.GetFullPath(_databasePath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a backup file, not the active database file.");
        await using (var check = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            await check.OpenAsync(cancellationToken);
            await ValidateAsync(check, cancellationToken);
        }

        var safetyPath = $"{_databasePath}.before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        await CreateBackupAsync(safetyPath, cancellationToken);
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fullPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString());
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        await ValidateAsync(destination, cancellationToken);
    }

    private static async Task ValidateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"SQLite backup integrity check failed: {result}");
    }
}
