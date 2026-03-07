using Microsoft.EntityFrameworkCore;

namespace KuechenRezepte.Data;

internal static class MigrationBootstrapper
{
    private const string ProductVersion = "9.0.0";

    public static async Task EnsureLegacyHistoryAsync(AppDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            var hasHistoryTable = await ExecuteScalarIntAsync(connection,
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';") > 0;
            if (hasHistoryTable)
            {
                return;
            }

            var hasUserTables = await ExecuteScalarIntAsync(connection,
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';") > 0;
            if (!hasUserTables)
            {
                return;
            }

            var latestMigration = context.Database.GetMigrations().LastOrDefault();
            if (string.IsNullOrWhiteSpace(latestMigration))
            {
                return;
            }

            await context.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);");

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") SELECT {latestMigration}, {ProductVersion} WHERE NOT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = {latestMigration});");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<int> ExecuteScalarIntAsync(System.Data.Common.DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null ? 0 : Convert.ToInt32(value);
    }
}
