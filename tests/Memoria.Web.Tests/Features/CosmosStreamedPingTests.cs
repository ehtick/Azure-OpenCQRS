using System;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.Web.Extensibility;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What a service's sheet asks of a Cosmos streamed store: whether it answers at all, over a
/// container set up with the store's own recommended indexing policy.
/// </summary>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedPingTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";

    private CosmosClient _client = null!;
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

        _reads = new CosmosStreamedReads(_client, _databaseName, ContainerName, TypeBindingSet.Default);
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
