using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// A count that has run out is handed to the reader as it is and read again behind them, so that
/// nobody waits for a scan of a whole table twice. That read outlives the one the reader is waiting
/// on — which is the whole point of it — so it must not be made on the store the reader is still
/// using.
/// </summary>
/// <remarks>
/// A page asks a model for every one of its sections, and one scope answers all of them: the reads
/// and contexts a scope resolves are one instance each. So a count left running behind the reader is
/// a second operation on the context the next section is about to ask its own question of, and
/// Entity Framework Core refuses that outright — the reader is shown "A second operation was started
/// on this context instance before a previous operation completed" where the figures should be.
/// <para>
/// The same read also outlives the scope it was started in, which closes as soon as the reader has
/// their answer. A read made on that scope's store is cut off when it closes, so the count it went
/// to fetch never arrives and the figure it was replacing is asked for again by the next reader,
/// for ever.
/// </para>
/// </remarks>
public class CountedBehindTheReaderTests
{
    private sealed class SetClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Nothing is kept for any time at all here, so the visit that finds the counts run out is the
    /// one after the first rather than the one five minutes later.
    /// </summary>
    private static MemoriaWeb CountingEveryVisit(OneQuestionAtATime.Opened stores)
    {
        var web = MemoriaWeb.Open().WithStreamedTypesOnly().WithClock(new SetClock())
            .WithReadsPerScope(stores.Open);

        web.Services.GetRequiredService<CachingSettingsStore>()
            .Save(countsKeptForMinutes: 0, recentKeptForSeconds: 0);

        return web;
    }

    private static Service Samples(MemoriaWeb web) =>
        web.Services.GetRequiredService<DomainTypeRegistry>().Current.ServiceAt(MemoriaWeb.ServiceName)
        ?? throw new InvalidOperationException($"No service is at /{MemoriaWeb.ServiceName}.");

    [Fact]
    public async Task Reads_every_section_of_a_model_while_a_count_that_ran_out_is_read_again()
    {
        var stores = new OneQuestionAtATime.Opened();
        using var web = CountingEveryVisit(stores);
        var activity = ActivatorUtilities.CreateInstance<ServiceActivity>(web.Services);

        await activity.Streamed(Samples(web));
        var again = await activity.Streamed(Samples(web));

        again.Problem.Should().BeNull();
        stores.All.Should().NotBeEmpty("the store has to have been asked something");
        stores.All.Where(store => store.Asked > 0).Should()
            .AllSatisfy(store => store.WasAskedTwoAtOnce.Should().BeFalse(
                "one store answers one question at a time, as the context behind it does"));
    }

    /// <summary>Home reads a service over its logs the same way, and the same applies.</summary>
    [Fact]
    public async Task Reads_a_service_s_log_while_a_count_that_ran_out_is_read_again()
    {
        var stores = new OneQuestionAtATime.Opened();
        using var web = CountingEveryVisit(stores);
        var activity = ActivatorUtilities.CreateInstance<ServiceActivity>(web.Services);

        await activity.Of(Samples(web));
        var again = await activity.Of(Samples(web));

        again.Problem.Should().BeNull();
        stores.All.Where(store => store.Asked > 0).Should()
            .AllSatisfy(store => store.WasAskedTwoAtOnce.Should().BeFalse());
    }

    /// <summary>
    /// A guard on what the arrangement is for rather than a pin on what was wrong with it: the
    /// count read behind a reader has to reach the reader after them, or nobody is spared anything
    /// and every visit starts a read that replaces nothing.
    /// </summary>
    [Fact]
    public async Task Hands_the_next_reader_the_count_read_behind_the_last_one()
    {
        var stores = new OneQuestionAtATime.Opened();
        using var web = CountingEveryVisit(stores);
        var activity = ActivatorUtilities.CreateInstance<ServiceActivity>(web.Services);

        var first = await Stored(activity, web);
        await activity.Streamed(Samples(web), ModelSection.Events);

        var later = await Until(activity, web, above: first);

        later.Should().BeGreaterThan(first, "a count read behind a reader replaced the one they were handed");
    }

    private static async Task<int> Stored(ServiceActivity activity, MemoriaWeb web) =>
        (await activity.Streamed(Samples(web), ModelSection.Events)).Events?.Stored.Value ?? 0;

    /// <summary>What a section says is stored, once it says more than the number given.</summary>
    private static async Task<int> Until(ServiceActivity activity, MemoriaWeb web, int above)
    {
        var stored = above;

        for (var waited = 0; waited < 50; waited++)
        {
            stored = await Stored(activity, web);

            if (stored > above)
            {
                return stored;
            }

            await Task.Delay(20);
        }

        return stored;
    }
}
