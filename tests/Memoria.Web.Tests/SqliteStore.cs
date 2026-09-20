using Microsoft.Data.Sqlite;

namespace Memoria.Web.Tests;

/// <summary>
/// Lets go of the connections pooled for one test's store, so the file behind it can be deleted.
/// </summary>
/// <remarks>
/// Every teardown in this assembly used to call <c>SqliteConnection.ClearAllPools</c>, which is
/// what it sounds like: it empties the pool of every SQLite store in the process. The test classes
/// here run beside one another, so a test tearing its own store down was closing connections that
/// other tests were in the middle of reading through. They failed with
/// <c>Cannot access a disposed object. Object name: 'SQLitePCL.sqlite3'</c> — rendered into a page
/// as a store that could not be read — somewhere unrelated to anything they were exercising, and
/// passed when run on their own.
/// <para>
/// A store's own pool is the one a test may empty. Keyed by connection string, so this builds the
/// same one the tests open their stores with.
/// </para>
/// </remarks>
internal static class SqliteStore
{
    /// <summary>The connection string a store file is opened with, which is also its pool's key.</summary>
    public static string ConnectionStringFor(string file) => $"Data Source={file}";

    /// <summary>Empties the pool held for each of those store files, and no others.</summary>
    public static void LetGo(params string[] files)
    {
        foreach (var file in files)
        {
            using var pooled = new SqliteConnection(ConnectionStringFor(file));

            SqliteConnection.ClearPool(pooled);
        }
    }
}
