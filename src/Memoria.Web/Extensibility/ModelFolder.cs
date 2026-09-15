using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Folds two versions of one model out of its history, without writing either anywhere.
/// </summary>
/// <remarks>
/// The events are read from the store once, up to the later version, through the store's own
/// filtered read — the model's event types and the identifier's event properties, which is what
/// the store's own fold reads with — and both versions are folded from that one read: the earlier
/// from the first however-many of the events, the later from all of them. A version is the model's
/// count of its own events, so the earlier version's events are exactly the first that many of the
/// later's; folding each version through the store instead read the earlier version's events twice.
/// <para>
/// The fold itself is the model's own <see cref="EventSourcedModel.Apply(IEnumerable{IEvent})"/>,
/// on a fresh instance: what a page comparing two versions of a row wants is each version's state,
/// and neither version is the one the row should be left at. No reflection over generics is needed,
/// since the filtered reads are not generic and the model is built from its runtime type.
/// </para>
/// </remarks>
public static class ModelFolder
{
    /// <summary>
    /// Folds two versions of a DCB model out of its boundary.
    /// </summary>
    /// <param name="service">The DCB domain service.</param>
    /// <param name="model">The aggregate or projection type to fold.</param>
    /// <param name="identifier">An identifier instance naming it, whose boundary selects its events.</param>
    /// <param name="beforeVersion">The earlier version: how many of the events to fold into it.</param>
    /// <param name="upToPosition">The position the later version was folded up to, inclusive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The two folded models, or what went wrong. A boundary with nothing to fold up to that
    /// position comes back as the model in its opening state twice, which is an answer rather than
    /// a fault.
    /// </returns>
    /// <remarks>
    /// The identifier alone, where a streamed fold takes a stream as well: a DCB identifier carries
    /// its own boundary. The boundary's tags go onto both versions, as the store's own fold puts
    /// them on before applying anything.
    /// </remarks>
    public static async Task<FoldedPair> Fold(
        IDcbDomainService service,
        Type model,
        object identifier,
        int beforeVersion,
        long upToPosition,
        CancellationToken cancellationToken = default)
    {
        if (DcbModels.BoundaryOf(identifier) is not { } boundary)
        {
            return FoldedPair.Refused(
                $"{identifier.GetType().Name} is not a DCB identifier, so nothing names what to fold.");
        }

        return await Fold(model, beforeVersion,
            applies => service.GetEventsUpToPosition(boundary, upToPosition, applies, cancellationToken),
            fresh =>
            {
                if (fresh is IDcbModel tagged)
                {
                    tagged.Tags = boundary.Tags;
                }
            });
    }

    /// <summary>
    /// Folds two versions of a streamed model out of its stream.
    /// </summary>
    /// <param name="service">The streamed domain service.</param>
    /// <param name="model">The aggregate or projection type to fold.</param>
    /// <param name="streamId">The stream it is folded from.</param>
    /// <param name="identifier">An identifier instance naming it inside that stream.</param>
    /// <param name="beforeVersion">The earlier version: how many of the events to fold into it.</param>
    /// <param name="upToSequence">The sequence the later version was folded up to, inclusive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The two folded models, or what went wrong. A stream with nothing to fold up to that sequence
    /// comes back as the model in its opening state twice, which is an answer rather than a fault.
    /// </returns>
    public static async Task<FoldedPair> Fold(
        IDomainService service,
        Type model,
        object streamId,
        object identifier,
        int beforeVersion,
        int upToSequence,
        CancellationToken cancellationToken = default)
    {
        if (streamId is not IStreamId stream)
        {
            return FoldedPair.Refused(
                $"{streamId.GetType().Name} is not a stream, so there is nowhere to fold from.");
        }

        IDictionary<string, string>? properties;

        switch (identifier)
        {
            case IAggregateId aggregateId:
                properties = aggregateId.EventPropertyFilter;
                break;
            case IProjectionId projectionId:
                properties = projectionId.EventPropertyFilter;
                break;
            default:
                return FoldedPair.Refused(
                    $"{identifier.GetType().Name} is not a streamed identifier, so nothing names what to fold.");
        }

        return await Fold(model, beforeVersion,
            applies => service.GetEventsUpToSequence(stream, upToSequence, applies, properties, cancellationToken),
            prepare: null);
    }

    /// <summary>
    /// One read, two folds: the events the model applies up to the later version, folded into two
    /// fresh instances — the first <paramref name="beforeVersion"/> of them into one, all into the
    /// other.
    /// </summary>
    private static async Task<FoldedPair> Fold(
        Type model,
        int beforeVersion,
        Func<Type[]?, Task<Result<List<IEvent>>>> read,
        Action<EventSourcedModel>? prepare)
    {
        try
        {
            if (Fresh(model, prepare) is not { } before || Fresh(model, prepare) is not { } after)
            {
                return FoldedPair.Refused($"{model.Name} is not an event-sourced model, so there is nothing to fold.");
            }

            var result = await read(after.EventTypeFilter);

            if (result.IsNotSuccess || result.Value is not { } events)
            {
                return FoldedPair.Refused(Describe(result.Failure));
            }

            before.Apply(events.Take(beforeVersion));
            after.Apply(events);

            return new FoldedPair(before, after, Error: null);
        }
        catch (Exception exception)
        {
            return FoldedPair.Refused(exception.Message);
        }
    }

    private static EventSourcedModel? Fresh(Type model, Action<EventSourcedModel>? prepare)
    {
        if (InstanceFactory.CreateInstance(model) is not EventSourcedModel fresh)
        {
            return null;
        }

        prepare?.Invoke(fresh);

        return fresh;
    }

    private static string Describe(Failure? failure) =>
        failure is null
            ? "The store reported a failure with no detail."
            : string.Join(" — ", new[] { failure.Title, failure.Description }.Where(part => !string.IsNullOrWhiteSpace(part)))
                is { Length: > 0 } message
                ? message
                : "The store reported a failure.";
}

/// <summary>The outcome of folding two versions.</summary>
/// <param name="Before">The earlier version, or null when there is none to show.</param>
/// <param name="After">The later version, or null when there is none to show.</param>
/// <param name="Error">Why there is none, or null when nothing went wrong.</param>
public sealed record FoldedPair(object? Before, object? After, string? Error)
{
    /// <summary>Neither version, and why.</summary>
    public static FoldedPair Refused(string error) => new(null, null, error);
}
