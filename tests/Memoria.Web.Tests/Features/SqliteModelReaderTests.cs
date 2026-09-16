using System.Data.Common;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Reading one stored DCB model off a real store. The row is reached by the key the store filed it
/// under — the identifier's store id and its boundary, digested the way the store digests them — so
/// the read is the same single-row lookup the store's own read is, rather than a scan of every
/// snapshot of the kind for one whose boundary matches.
/// </summary>
public class SqliteModelReaderTests : IAsyncLifetime
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"memoria_web_dcb_reader_{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_file}";

    private readonly List<string> _commands = [];

    private sealed class Capture(List<string> commands) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private DcbStoreDbContext Store() =>
        new(new DbContextOptionsBuilder<DcbDbContext>()
                .UseSqlite(ConnectionString)
                .AddInterceptors(new Capture(_commands))
                .Options,
            TimeProvider.System,
            Substitute.For<IHttpContextAccessor>());

    private static readonly SampleDcbAggregateId Kettle = new("kettle");

    public async Task InitializeAsync()
    {
        await using var seed = Store();
        await seed.Database.EnsureCreatedAsync();

        // Filed the way the store files them: under the key it builds from the identifier's store
        // id and its boundary. Two, so a read has something to pass over.
        seed.DcbSnapshots.Add(Snapshot(Kettle, """{"Name":"Kettle"}""", version: 3));
        seed.DcbSnapshots.Add(Snapshot(new SampleDcbAggregateId("toaster"), """{"Name":"Toaster"}""", version: 1));
        await seed.SaveChangesAsync();

        _commands.Clear();
    }

    private static DcbSnapshotEntity Snapshot(SampleDcbAggregateId identifier, string data, int version) => new()
    {
        Id = DcbSnapshotEntity.BuildId(DcbSnapshotEntity.AggregateKind, identifier.ToStoreId(), identifier.Boundary),
        SnapshotKind = DcbSnapshotEntity.AggregateKind,
        StoreId = identifier.ToStoreId(),
        TagQuery = identifier.Boundary.ToString(),
        ModelType = "SampleDcbAggregate:1",
        Version = version,
        LatestPosition = version,
        Data = data
    };

    public Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
            // A file the operating system is still holding is not this test's problem.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Loads_the_snapshot_the_store_filed_under_the_identifier()
    {
        await using var context = Store();

        var loaded = await ModelReader.Load(context, typeof(SampleDcbAggregate), DcbModelKind.Aggregate, Kettle);

        using var scope = new AssertionScope();

        loaded.Error.Should().BeNull();
        loaded.Model.Should().BeOfType<SampleDcbAggregate>().Which.Name.Should().Be("Kettle");
        loaded.Snapshot!.Version.Should().Be(3);
    }

    [Fact]
    public async Task Reaches_the_row_by_the_key_the_store_filed_it_under()
    {
        await using var context = Store();

        await ModelReader.Load(context, typeof(SampleDcbAggregate), DcbModelKind.Aggregate, Kettle);

        _commands.Should().ContainSingle().Which.Should().Contain("\"Id\" = @");
    }

    [Fact]
    public async Task Finds_nothing_under_an_identifier_no_snapshot_was_filed_for()
    {
        await using var context = Store();

        var loaded = await ModelReader.Load(
            context, typeof(SampleDcbAggregate), DcbModelKind.Aggregate, new SampleDcbAggregateId("blender"));

        using var scope = new AssertionScope();

        loaded.Model.Should().BeNull();
        loaded.Snapshot.Should().BeNull();
        loaded.Error.Should().BeNull();
    }

    /// <summary>
    /// What a page hands over if the uploaded types were reloaded under it: an identifier of the
    /// wrong kind. Said, and the store not asked.
    /// </summary>
    [Fact]
    public async Task Refuses_an_identifier_that_is_not_of_the_kind_asked_for()
    {
        await using var context = Store();

        var loaded = await ModelReader.Load(context, typeof(SampleDcbAggregate), DcbModelKind.Projection, Kettle);

        using var scope = new AssertionScope();

        loaded.Error.Should().NotBeNullOrWhiteSpace();
        _commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Says_why_when_the_store_could_not_be_read()
    {
        await using var context = Store();
        await context.Database.ExecuteSqlRawAsync("DROP TABLE DcbSnapshots");

        var loaded = await ModelReader.Load(context, typeof(SampleDcbAggregate), DcbModelKind.Aggregate, Kettle);

        loaded.Error.Should().NotBeNullOrWhiteSpace();
    }
}
