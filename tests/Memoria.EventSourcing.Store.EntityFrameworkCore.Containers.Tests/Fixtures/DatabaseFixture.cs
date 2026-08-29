using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

/// <summary>
/// Starts a database engine in a container for the tests that need a real provider. SQLite cannot
/// answer questions about column widths, index key limits or engine-specific DDL, so those claims
/// have to be executed against the engines the store actually targets.
/// </summary>
/// <remarks>
/// A missing or unhealthy Docker daemon is reported as a skip rather than a failure, so the suite
/// stays runnable on machines without Docker. It does mean a green run is not proof these tests
/// executed — check for skips when the answer matters.
/// </remarks>
public abstract class DatabaseFixture : IAsyncLifetime
{
    /// <summary>Connection string for the container's default database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Null when the engine started; otherwise why the tests cannot run.</summary>
    public string? UnavailableReason { get; private set; }

    public bool IsAvailable => UnavailableReason is null;

    protected abstract string EngineName { get; }

    protected abstract Task<string> StartContainerAsync();

    protected abstract Task StopContainerAsync();

    /// <summary>
    /// A connection string pointing at a uniquely named database, so each test can create the store's
    /// schema from scratch without colliding with another test.
    /// </summary>
    public abstract string ConnectionStringForFreshDatabase();

    protected static string FreshDatabaseName() => $"memoria_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        try
        {
            ConnectionString = await StartContainerAsync();
        }
        catch (Exception exception)
        {
            UnavailableReason =
                $"{EngineName} container could not be started ({exception.GetType().Name}: {exception.Message}). Is Docker running?";
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await StopContainerAsync();
        }
        catch
        {
            // Nothing useful to do while tearing down, and throwing here would mask test results.
        }
    }
}
