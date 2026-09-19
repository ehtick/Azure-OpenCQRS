using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.Web.Extensibility;

namespace Memoria.Web.Data;

/// <summary>
/// Whether each row on a snapshots data page is behind its history, by the rule the detail page's
/// info tab warns by: more events of the types the model applies, in the stream its identifier
/// claims or the boundary it was keyed by, than the version it was folded to.
/// </summary>
/// <remarks>
/// Each row is a read of its own — one stream, or one boundary — so a page of them is as many reads
/// as it has rows. They are started together and taken <see cref="AtOnce"/> at a time, each in a
/// scope of its own with a context of its own, and each is kept for as long as recent figures are,
/// so paging back and forth, or refreshing, asks again only once that runs out. A row the rule
/// cannot be applied to — a streamed row whose identifier cannot be worked out, so a shared stream
/// cannot be narrowed to it; a boundary that cannot be read back — is not checked: a mark on a
/// history it was never folded from would be worse than none. A read that fails marks nothing
/// either, and is not kept.
/// </remarks>
public sealed class SnapshotLags(
    IServiceScopeFactory scopes,
    DomainTypeRegistry registry,
    CachingSettingsStore settings,
    TimeProvider clock)
{
    /// <summary>How many of a page's rows are read at once.</summary>
    public const int AtOnce = 4;

    private readonly FigureCache _kept = new(clock, () => settings.RecentKeptFor);

    /// <summary>The key a streamed row's check is found by on the page.</summary>
    public static string KeyOf(StoredStreamSnapshot row) => $"{row.StreamId}\n{row.StoreId}";

    /// <summary>The key a DCB row's check is found by on the page.</summary>
    public static string KeyOf(Instance row) => $"{row.ModelType}\n{row.Boundary}";

    /// <summary>A check for each streamed row, by <see cref="KeyOf(StoredStreamSnapshot)"/>, each answering how far behind the row is, or null.</summary>
    public IReadOnlyDictionary<string, Task<SnapshotLag?>> Streamed(
        Service service, StreamedModelKind kind, IReadOnlyList<StoredStreamSnapshot> rows)
    {
        var catalogue = registry.Current;
        var models = kind is StreamedModelKind.Projection ? catalogue.StreamedProjections : catalogue.StreamedAggregates;
        var turns = new SemaphoreSlim(AtOnce);

        return rows
            .DistinctBy(KeyOf)
            .ToDictionary(KeyOf, row =>
            {
                if (ModelOf(models, row.Type, model => DomainTypeDescriber.BindingOf(model)?.Key) is not { } model ||
                    StreamedIdentity.Of(catalogue, kind, model, row.StreamId, row.StoreId).Claim is not { } claim)
                {
                    return Task.FromResult<SnapshotLag?>(null);
                }

                var applies = BoundaryEvents.AppliedBy(model, loaded: null);
                var filter = new StreamedEventFilter(row.StreamId, EventType: null, Text: null, Descending: false, Page: 1, Size: 1)
                {
                    EventTypes = applies?.Select(type => DomainTypeDescriber.BindingOf(type)?.Key).OfType<string>().ToList(),
                    Properties = claim
                };

                return Check(service, catalogue, turns, row.Version,
                    $"{service.Slug}/behind/streamed/{kind}/{row.StreamId}/{row.StoreId}/{row.Version}",
                    async provider =>
                    {
                        var counted = await provider.GetRequiredService<IStreamedReads>().Count(filter);

                        return counted.Total ?? throw new InvalidOperationException(counted.Error);
                    });
            });
    }

    /// <summary>A check for each DCB row, by <see cref="KeyOf(Instance)"/>, each answering how far behind the row is, or null.</summary>
    public IReadOnlyDictionary<string, Task<SnapshotLag?>> Dcb(
        Service service, DcbModelKind kind, IReadOnlyList<Instance> rows)
    {
        var catalogue = registry.Current;
        var models = catalogue.Models(kind);
        var turns = new SemaphoreSlim(AtOnce);

        return rows
            .DistinctBy(KeyOf)
            .ToDictionary(KeyOf, row =>
            {
                if (ModelOf(models, row.ModelType, model => DomainTypeDescriber.BindingOf(model)?.Key) is not { } model ||
                    BoundaryOf(row.Boundary) is not { } boundary)
                {
                    return Task.FromResult<SnapshotLag?>(null);
                }

                var applies = BoundaryEvents.AppliedBy(model, loaded: null);

                return Check(service, catalogue, turns, row.Version,
                    $"{service.Slug}/behind/dcb/{kind}/{row.ModelType}/{row.Boundary}/{row.Version}",
                    async provider =>
                    {
                        var counted = await BoundaryEvents.Count(provider.GetRequiredService<DcbStoreDbContext>(), boundary, applies);

                        return counted.Total ?? throw new InvalidOperationException(counted.Error);
                    });
            });
    }

    /// <summary>
    /// One row's check: the count kept, or one read now in a scope put inside the service, when a
    /// turn comes; and how far that leaves the version behind. Nothing when the read fails.
    /// </summary>
    private async Task<SnapshotLag?> Check(
        Service service, DomainTypeCatalogue catalogue, SemaphoreSlim turns, int version, string key,
        Func<IServiceProvider, Task<int>> count)
    {
        try
        {
            var kept = await _kept.Keep(key, async _ =>
            {
                await turns.WaitAsync();

                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<CurrentService>().Enter(service, catalogue);

                    return await count(scope.ServiceProvider);
                }
                finally
                {
                    turns.Release();
                }
            });

            return SnapshotLag.Of(version, kept.Value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The registered model a row was written as, by the key the row carries.</summary>
    private static Type? ModelOf(IReadOnlyList<Type> models, string key, Func<Type, string?> keyOf) =>
        models.FirstOrDefault(model => keyOf(model) == key);

    /// <summary>
    /// The boundary a row was keyed by, read back from the text it was stored as — or null when it
    /// cannot be: the store's own form is not meant to be parsed, so what is rebuilt is kept only
    /// when it renders to exactly the text it was read from.
    /// </summary>
    private static TagQuery? BoundaryOf(string stored)
    {
        try
        {
            var read = StoredBoundary.Read(stored);
            var tags = read.Tags.Select(Tag.Parse).ToArray();
            var rebuilt = read.Combination == "(AllOf)" ? TagQuery.AllOf(tags) : TagQuery.AnyOf(tags);

            return rebuilt.ToString() == stored ? rebuilt : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
