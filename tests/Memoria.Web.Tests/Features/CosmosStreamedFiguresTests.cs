using System;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.Web.Extensibility;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What the overview pages say of a Cosmos streamed store beside each section, over a container set
/// up with the store's own recommended indexing policy — the one that leaves the date a document was
/// last written out of the index, so a read that sorted on it would be refused here.
/// </summary>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedFiguresTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private CosmosClient _client = null!;
    private Container _container = null!;
    private CosmosStreamedReads _reads = null!;

    public async Task InitializeAsync()
    {
        _client = new CosmosClient(Endpoint, Key, new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            }),
            ConnectionMode = ConnectionMode.Gateway
        });

        var database = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        await database.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerName, "/streamId")
        {
            IndexingPolicy = CosmosIndexingPolicy.CreateRecommended()
        });

        _container = database.Database.GetContainer(ContainerName);
        _reads = new CosmosStreamedReads(_client, _databaseName, ContainerName, TypeBindingSet.Default);
    }

    [Fact]
    public async Task Counts_the_snapshots_of_each_kind_and_the_streams_the_events_are_held_in()
    {
        await Append(("customer:c-1", 0), ("customer:c-1", 1), ("customer:c-2", 0), ("order:o-1", 0));
        await SaveAggregate("customer:c-1", Start);
        await SaveAggregate("customer:c-2", Start);
        await SaveAggregate("customer:c-3", Start);
        await SaveProjection("customer:c-1", Start);

        using var scope = new AssertionScope();

        (await _reads.CountSnapshots(StreamedModelKind.Aggregate)).Should().Be(3);
        (await _reads.CountSnapshots(StreamedModelKind.Projection)).Should().Be(1);
        (await _reads.CountStreams()).Should().Be(3, "four events are held in three streams");
    }

    [Fact]
    public async Task Says_when_the_newest_snapshot_of_a_kind_was_last_written_though_the_date_is_not_indexed()
    {
        await SaveAggregate("customer:c-1", Start + TimeSpan.FromHours(3));
        await SaveAggregate("customer:c-2", Start + TimeSpan.FromHours(1));

        using var scope = new AssertionScope();

        (await _reads.LastWritten(StreamedModelKind.Aggregate)).Should().Be(Start + TimeSpan.FromHours(3));
        (await _reads.LastWritten(StreamedModelKind.Projection)).Should().BeNull();
    }

    [Fact]
    public async Task Tallies_the_events_and_the_snapshots_of_one_type()
    {
        await Append(("customer:c-1", 0), ("customer:c-2", 0));
        await _container.UpsertItemAsync(new EventDocument
        {
            Id = "order:o-1:0",
            StreamId = "order:o-1",
            EventType = "OrderShippedEvent:1",
            Sequence = 0,
            Data = "{}",
            CreatedDate = Start + TimeSpan.FromHours(2)
        }, new PartitionKey("order:o-1"));
        await SaveAggregate("customer:c-1", Start + TimeSpan.FromHours(3));
        await SaveAggregate("customer:c-2", Start + TimeSpan.FromHours(1));

        using var scope = new AssertionScope();

        (await _reads.TallyEvents("OrderPlacedEvent:1")).Should().Be(new TypeTally(2, Start));
        (await _reads.TallyEvents("OrderShippedEvent:1")).Should().Be(new TypeTally(1, Start + TimeSpan.FromHours(2)));
        (await _reads.TallyEvents("OrderCancelledEvent:1")).Should().BeNull();
        (await _reads.TallySnapshots(StreamedModelKind.Aggregate, "CustomerAccount:1"))
            .Should().Be(new TypeTally(2, Start + TimeSpan.FromHours(3)));
        (await _reads.TallySnapshots(StreamedModelKind.Projection, "CustomerOrderHistory:1")).Should().BeNull();
    }

    [Fact]
    public async Task Counts_the_streams_whose_ids_a_pattern_matches()
    {
        await Append(("customer:c-1", 0), ("customer:c-1", 1), ("customer:c-2", 0), ("order:o-1", 0));

        using var scope = new AssertionScope();

        (await _reads.CountStreams("customer:%")).Should().Be(2);
        (await _reads.CountStreams("invoice:%")).Should().Be(0);
    }

    /// <summary>
    /// The sheet's round trip: the container's own record, read and nothing of the domain; a
    /// container that is not there fails the way every other read over it would.
    /// </summary>
    [Fact]
    public async Task Answers_a_ping_and_fails_one_over_a_container_that_is_not_there()
    {
        var missing = new CosmosStreamedReads(_client, _databaseName, "Missing", TypeBindingSet.Default);

        var pinging = () => _reads.Ping();
        var failing = () => missing.Ping();

        await pinging.Should().NotThrowAsync();
        await failing.Should().ThrowAsync<CosmosException>();
    }

    [Fact]
    public async Task Counts_nothing_in_an_empty_container()
    {
        using var scope = new AssertionScope();

        (await _reads.CountSnapshots(StreamedModelKind.Aggregate)).Should().Be(0);
        (await _reads.CountStreams()).Should().Be(0);
    }

    private async Task Append(params (string Stream, int Sequence)[] events)
    {
        foreach (var (stream, sequence) in events)
        {
            await _container.UpsertItemAsync(new EventDocument
            {
                Id = $"{stream}:{sequence}",
                StreamId = stream,
                EventType = "OrderPlacedEvent:1",
                Sequence = sequence,
                Data = "{}",
                CreatedDate = Start
            }, new PartitionKey(stream));
        }
    }

    private Task SaveAggregate(string stream, DateTimeOffset updated) =>
        _container.UpsertItemAsync(new AggregateDocument
        {
            Id = $"{stream}:1",
            StreamId = stream,
            AggregateType = "CustomerAccount:1",
            Version = 1,
            Data = "{}",
            CreatedDate = Start,
            UpdatedDate = updated
        }, new PartitionKey(stream));

    private Task SaveProjection(string stream, DateTimeOffset updated) =>
        _container.UpsertItemAsync(new ProjectionDocument
        {
            Id = $"history-{stream}:1",
            StreamId = stream,
            ProjectionType = "CustomerOrderHistory:1",
            Version = 1,
            Data = "{}",
            CreatedDate = Start,
            UpdatedDate = updated
        }, new PartitionKey(stream));

    public async Task DisposeAsync()
    {
        try
        {
            await _client.GetDatabase(_databaseName).DeleteAsync();
        }
        catch (CosmosException)
        {
            // A run that never created it has nothing to clean up.
        }

        _client.Dispose();
    }
}
