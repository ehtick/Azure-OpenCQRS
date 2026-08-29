using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// Loads and runs the shipped migration scripts. They are read from the repository rather than
/// copied, so a test can only ever pass against the file consumers are actually given.
/// </summary>
public static class MigrationScript
{
    public static string Read(string fileName) => File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "migrations", fileName));

    public static async Task ExecuteAsync(DbContext dbContext, string sql)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Walks up from the test binaries until the directory holding the solution is found.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Memoria.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }
}
