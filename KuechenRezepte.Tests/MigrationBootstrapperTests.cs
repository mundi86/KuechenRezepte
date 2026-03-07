using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KuechenRezepte.Data;

namespace KuechenRezepte.Tests;

public class MigrationBootstrapperTests
{
    [Fact]
    public async Task EnsureLegacyHistoryAsync_WithLegacyTables_CreatesHistoryAndInsertsLatestMigration()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"kuechenrezepte-legacy-{Guid.NewGuid():N}.db");
        try
        {
            await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE Rezepte (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Beschreibung TEXT NULL,
                        Zubereitung TEXT NULL,
                        Portionen INTEGER NOT NULL,
                        Zubereitungszeit INTEGER NULL,
                        Kategorie TEXT NOT NULL,
                        BildPfad TEXT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var context = new AppDbContext(options);
            await MigrationBootstrapper.EnsureLegacyHistoryAsync(context);

            await using var verifyConn = new SqliteConnection($"Data Source={dbPath}");
            await verifyConn.OpenAsync();
            await using var verifyCmd = verifyConn.CreateCommand();
            verifyCmd.CommandText = """
                SELECT COUNT(1)
                FROM __EFMigrationsHistory
                WHERE MigrationId = '20260307192312_InitialCreate';
                """;
            var count = Convert.ToInt32(await verifyCmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                TryDelete(dbPath);
            }
        }
    }

    [Fact]
    public async Task EnsureLegacyHistoryAsync_WithEmptyDatabase_DoesNotCreateHistoryTable()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"kuechenrezepte-empty-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var context = new AppDbContext(options);
            await MigrationBootstrapper.EnsureLegacyHistoryAsync(context);

            await using var verifyConn = new SqliteConnection($"Data Source={dbPath}");
            await verifyConn.OpenAsync();
            await using var verifyCmd = verifyConn.CreateCommand();
            verifyCmd.CommandText = """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type='table' AND name='__EFMigrationsHistory';
                """;
            var count = Convert.ToInt32(await verifyCmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                TryDelete(dbPath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Temp files from failed cleanup are acceptable in test runs.
        }
    }
}
