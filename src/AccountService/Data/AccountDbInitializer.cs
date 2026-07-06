using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Data;

public static class AccountDbInitializer
{
    public static async Task InitializeAsync(AccountDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await PatchLegacySchemaAsync(dbContext, cancellationToken);
    }

    private static async Task PatchLegacySchemaAsync(AccountDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        if (connection is not SqliteConnection)
        {
            return;
        }

        if (!await TableExistsAsync(connection, "Accounts", cancellationToken))
        {
            return;
        }

        if (await ColumnExistsAsync(connection, "Accounts", "Balance", cancellationToken))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Accounts ADD COLUMN Balance TEXT NOT NULL DEFAULT '0';",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE Accounts
            SET Balance = (
                SELECT COALESCE(SUM(
                    CASE
                        WHEN Transactions.Type = 'CREDIT' THEN CAST(Transactions.Amount AS REAL)
                        WHEN Transactions.Type = 'DEBIT' THEN -CAST(Transactions.Amount AS REAL)
                        ELSE 0
                    END), 0)
                FROM Transactions
                WHERE Transactions.AccountId = Accounts.AccountId);
            """,
            cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.Add(new SqliteParameter("$name", tableName));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
