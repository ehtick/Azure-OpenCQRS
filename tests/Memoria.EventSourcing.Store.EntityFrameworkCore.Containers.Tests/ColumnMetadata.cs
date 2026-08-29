using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// The column type an engine actually produced for a mapped property.
/// </summary>
/// <param name="DataType">The engine's own type name, e.g. <c>nvarchar</c> or <c>text</c>.</param>
/// <param name="MaximumLength">Declared character length, or null when the type is unbounded.</param>
public sealed record ColumnType(string DataType, int? MaximumLength)
{
    public override string ToString() =>
        MaximumLength is null ? DataType : $"{DataType}({MaximumLength})";
}

/// <summary>
/// Reads back what the engine created, rather than what the EF model asked for. The two differ in
/// exactly the places that matter for key widths and index limits.
/// </summary>
public static class ColumnMetadata
{
    public static async Task<IReadOnlyDictionary<string, ColumnType>> ReadAsync(
        DbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tableName
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var columns = new Dictionary<string, ColumnType>(StringComparer.OrdinalIgnoreCase);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns[reader.GetString(0)] = new ColumnType(
                    reader.GetString(1),
                    await reader.IsDBNullAsync(2) ? null : Convert.ToInt32(reader.GetValue(2)));
            }

            return columns;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public static string Describe(IReadOnlyDictionary<string, ColumnType> columns) =>
        string.Join(", ", columns.OrderBy(column => column.Key)
            .Select(column => $"{column.Key} {column.Value}"));
}
