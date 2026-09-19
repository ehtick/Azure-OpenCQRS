using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The aggregates and projections data tables mark each row whose stored snapshot is behind its
/// history — the rule the detail page's info tab warns by — with the clock that warning wears,
/// beside the version it is about. Each mark is a read of its own row's stream or boundary: drawn
/// after the table, so a slow read never holds the rows up; a few at a time; and kept for as long
/// as recent figures are.
/// </summary>
public class BehindHistoryTests
{
    private const string Aggregates = "/samples/streamed/aggregates/data";

    [Fact]
    public async Task Marks_a_streamed_row_behind_its_history_and_leaves_one_that_is_not()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);
        await AppendStreamed(web, "sample:1", 5);
        await AppendStreamed(web, "sample:2", 1);
        await SaveAggregate(web, "sample:1", "sample-1:1", version: 2);
        await SaveAggregate(web, "sample:2", "sample-2:1", version: 1);

        var page = await web.Client.GetStringAsync(Aggregates);

        using var scope = new AssertionScope();

        Version(page, "sample-1:1").Should().Contain("Behind its history by 3 events");
        Version(page, "sample-2:1").Should().NotContain("behind-mark");
    }

    [Fact]
    public async Task Marks_a_dcb_row_behind_its_boundary()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();
        await CreateTheStore(web);
        await AppendDcb(web, 1);
        await AppendDcb(web, 2);
        await AppendDcb(web, 3);
        await SaveDcbSnapshot(web, version: 1);

        var page = await web.Client.GetStringAsync("/samples/dcb/aggregates/data");

        Version(page, "carrying:abc").Should().Contain("Behind its history by 2 events");
    }

    /// <summary>
    /// The rows are sent while their checks are still being read, and each mark follows in the
    /// patch that answers it — a slow stream holds up its own mark and nothing else.
    /// </summary>
    [Fact]
    public async Task Sends_the_rows_before_their_checks_have_answered()
    {
        var gate = new TaskCompletionSource<EventCount>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = Holding(rows: 1, count: _ => gate.Task);
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);

        // Raced against a clock rather than awaited: a page that holds everything back for its
        // checks sends nothing — not even its headers — until they answer, and the test host gives
        // up neither a request nor a read it was asked for. When the clock wins the store is let go,
        // and what comes then is the whole page, marks and all, which the first assertion refuses.
        var reading = FirstPart();
        if (await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(10))) != reading)
        {
            gate.TrySetResult(new EventCount(5, null));
        }

        var (response, reader, first) = await reading;
        gate.TrySetResult(new EventCount(5, null));
        var rest = await reader.ReadToEndAsync();
        reader.Dispose();
        response.Dispose();

        async Task<(HttpResponseMessage, StreamReader, string)> FirstPart()
        {
            var sent = await web.Client.GetAsync(Aggregates, HttpCompletionOption.ResponseHeadersRead);
            var body = new StreamReader(await sent.Content.ReadAsStreamAsync());

            return (sent, body, await ReadUntil(body, "</html>"));
        }

        using var scope = new AssertionScope();

        Markup.Plain(first).Should().Contain(">sample-1:1<", "the rows are drawn before their checks answer")
            .And.NotContain("Behind its history");
        rest.Should().Contain("Behind its history by 3 events");
    }

    /// <summary>A row's check is kept for as long as recent figures are, so a reload asks nothing again.</summary>
    [Fact]
    public async Task Keeps_a_row_s_check_for_as_long_as_recent_figures_are_kept()
    {
        var reads = Holding(rows: 1, count: _ => Task.FromResult(new EventCount(5, null)));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);

        await web.Client.GetStringAsync(Aggregates);
        var again = await web.Client.GetStringAsync(Aggregates);

        using var scope = new AssertionScope();

        again.Should().Contain("Behind its history by 3 events");
        await reads.Received(1).Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checks_every_row_again_when_recent_figures_are_kept_for_no_time()
    {
        var reads = Holding(rows: 1, count: _ => Task.FromResult(new EventCount(5, null)));
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);
        web.Services.GetRequiredService<CachingSettingsStore>().Save(5, recentKeptForSeconds: 0);

        await web.Client.GetStringAsync(Aggregates);
        await web.Client.GetStringAsync(Aggregates);

        await reads.Received(2).Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A page of a hundred rows asks a few at a time rather than a hundred at once.</summary>
    [Fact]
    public async Task Checks_no_more_than_four_rows_at_once()
    {
        var gate = new TaskCompletionSource<EventCount>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asking = 0;
        var most = 0;
        var reads = Holding(rows: 10, count: async _ =>
        {
            var now = Interlocked.Increment(ref asking);
            InterlockedMax(ref most, now);
            try
            {
                return await gate.Task;
            }
            finally
            {
                Interlocked.Decrement(ref asking);
            }
        });
        using var web = MemoriaWeb.Open().WithSampleTypes().WithReads(reads);

        var loading = web.Client.GetStringAsync(Aggregates + "?size=10");
        for (var waited = 0; Volatile.Read(ref asking) < 4 && waited < 100; waited++)
        {
            await Task.Delay(20);
        }

        await Task.Delay(200);
        var atOnce = Volatile.Read(ref most);
        gate.SetResult(new EventCount(5, null));
        await loading;

        atOnce.Should().Be(4);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }

    /// <summary>
    /// Reads standing in for the store: a page of snapshot rows of the sample aggregate, each version
    /// 2 in a stream of its own, and a count answered as the test says.
    /// </summary>
    private static IStreamedReads Holding(int rows, Func<StreamedEventFilter, Task<EventCount>> count)
    {
        var reads = Substitute.For<IStreamedReads>();
        var written = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var snapshots = Enumerable.Range(1, rows)
            .Select(index => new StoredStreamSnapshot($"sample:{index}", $"sample-{index}:1", "SampleAggregate:1", 2, 1, written, written))
            .ToList();

        reads.Snapshots(Arg.Any<StreamedSnapshotFilter>(), Arg.Any<CancellationToken>())
            .Returns(new StoredStreamSnapshots(snapshots, rows, 1, 1, null));
        reads.Count(Arg.Any<StreamedEventFilter>(), Arg.Any<CancellationToken>())
            .Returns(call => count(call.Arg<StreamedEventFilter>()));

        return reads;
    }

    /// <summary>The version cell of the row whose id or boundary is given, as the page ends up drawing it.</summary>
    private static string Version(string page, string rowText)
    {
        var rows = Regex.Matches(Markup.Plain(page), "<tr[^>]*>.*?</tr>", RegexOptions.Singleline)
            .Select(row => row.Value)
            .Where(row => row.Contains(rowText, StringComparison.Ordinal))
            .ToList();

        return rows.Count == 0
            ? string.Empty
            : Regex.Match(rows[^1], "<td class=\"numeric\">.*?</td>", RegexOptions.Singleline).Value;
    }

    /// <summary>What the response has sent up to the marker, or all of it when the marker never comes.</summary>
    private static async Task<string> ReadUntil(StreamReader reader, string marker)
    {
        var read = new System.Text.StringBuilder();
        var buffer = new char[1024];

        while (!read.ToString().Contains(marker, StringComparison.Ordinal))
        {
            var count = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (count == 0)
            {
                break;
            }

            read.Append(buffer, 0, count);
        }

        return read.ToString();
    }

    private static async Task CreateTheStore(MemoriaWeb web)
    {
        using var scope = web.Scope();
        await scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    private static async Task AppendStreamed(MemoriaWeb web, string stream, int events)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();

        for (var sequence = 0; sequence < events; sequence++)
        {
            store.Events.Add(new EventEntity
            {
                Id = $"{stream}:{sequence}",
                StreamId = stream,
                EventType = "SampleHappened:1",
                Sequence = sequence,
                Data = """{"Id":"sample-1"}"""
            });
        }

        await store.SaveChangesAsync();
    }

    private static async Task SaveAggregate(MemoriaWeb web, string stream, string id, int version)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<StreamedStoreDbContext>();
        store.Aggregates.Add(new AggregateEntity
        {
            Id = id,
            StreamId = stream,
            AggregateType = "SampleAggregate:1",
            Version = version,
            LatestEventSequence = version - 1,
            Data = "{}"
        });
        await store.SaveChangesAsync();
    }

    private static async Task AppendDcb(MemoriaWeb web, long position)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        store.DcbEvents.Add(new DcbEventEntity
        {
            Position = position,
            EventType = "SampleCarried:1",
            Data = $$"""{"Id":"carried-{{position}}"}""",
            Tags = { new DcbEventTagEntity { Tag = "carrying:abc" } }
        });
        await store.SaveChangesAsync();
    }

    private static async Task SaveDcbSnapshot(MemoriaWeb web, int version)
    {
        using var scope = Seeding(web);
        var store = scope.ServiceProvider.GetRequiredService<DcbStoreDbContext>();
        store.DcbSnapshots.Add(new DcbSnapshotEntity
        {
            Id = $"{DcbSnapshotEntity.AggregateKind}:carrying:abc",
            SnapshotKind = DcbSnapshotEntity.AggregateKind,
            StoreId = "abc",
            TagQuery = "carrying:abc",
            ModelType = "SampleCarryingAggregate:1",
            Version = version,
            LatestPosition = version,
            Data = "{}"
        });
        await store.SaveChangesAsync();
    }

    private static IServiceScope Seeding(MemoriaWeb web)
    {
        var scope = web.Scope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "seeder")], "test"))
        };

        return scope;
    }
}
