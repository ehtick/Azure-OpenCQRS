using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Reads the indexes an engine actually holds for a table, so a migration script can be checked
/// against the schema it is supposed to produce rather than against its own SQL.
/// </summary>
public static class IndexMetadata
{
    private const string SqlServerQuery =
        """
        SELECT i.name, i.is_unique
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(@tableName) AND i.name IS NOT NULL
        """;

    private const string PostgreSqlQuery =
        """
        SELECT index_class.relname, index_meta.indisunique
        FROM pg_class index_class
        JOIN pg_index index_meta ON index_meta.indexrelid = index_class.oid
        JOIN pg_class table_class ON table_class.oid = index_meta.indrelid
        WHERE table_class.relname = @tableName
        """;

    private const string SqlServerExcludePrimaryKey = " AND i.is_primary_key = 0";

    private const string PostgreSqlExcludePrimaryKey = " AND NOT index_meta.indisprimary";

    /// <param name="includePrimaryKeys">
    /// Primary keys are indexes too. Exclude them when asserting on secondary indexes alone; include
    /// them when comparing two schemas, where a differing key matters as much as a differing index.
    /// </param>
    public static Task<IReadOnlyList<string>> ReadSqlServerAsync(DbContext dbContext, string tableName,
        bool includePrimaryKeys = false) =>
        ReadAsync(dbContext, tableName,
            includePrimaryKeys ? SqlServerQuery : SqlServerQuery + SqlServerExcludePrimaryKey);

    /// <inheritdoc cref="ReadSqlServerAsync"/>
    public static Task<IReadOnlyList<string>> ReadPostgreSqlAsync(DbContext dbContext, string tableName,
        bool includePrimaryKeys = false) =>
        ReadAsync(dbContext, tableName,
            includePrimaryKeys ? PostgreSqlQuery : PostgreSqlQuery + PostgreSqlExcludePrimaryKey);

    /// <summary>
    /// Index names, each suffixed with " unique" when unique, sorted so comparisons are stable.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ReadAsync(DbContext dbContext, string tableName, string query)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = query;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var indexes = new List<string>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                indexes.Add(reader.GetBoolean(1) ? $"{name} unique" : name);
            }

            indexes.Sort(StringComparer.Ordinal);

            return indexes;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
