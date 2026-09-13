using System.Reflection;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Folds one model's state out of its stream up to a sequence, without writing it anywhere.
/// </summary>
/// <remarks>
/// Through the store's own domain service, as <see cref="ModelRefresher"/> refreshes: folding events
/// into a model is the store's operation, and the in-memory read is the one that does it without
/// leaving a snapshot behind — which is what a page comparing two versions of a row wants, since
/// neither version is the one the row should be left at.
/// <para>
/// Reflection, because the model type is not known until someone uploads it and the read is generic
/// and constrained (<c>IAggregateRoot, new()</c>). The overload is picked by its parameters rather
/// than by name: the whole stream, up to a sequence, and up to a date all share the name, and only
/// the sequence one is this.
/// </para>
/// </remarks>
public static class ModelFolder
{
    private static readonly MethodInfo FoldAggregate = UpToSequence(nameof(IDomainService.GetInMemoryAggregate));

    private static readonly MethodInfo FoldProjection = UpToSequence(nameof(IDomainService.GetInMemoryProjection));

    private static MethodInfo UpToSequence(string name) =>
        typeof(IDomainService).GetMethods()
            .SingleOrDefault(method =>
                method.Name == name &&
                method.GetParameters() is [_, _, { ParameterType.Name: nameof(Int32) }, _])
        ?? throw new InvalidOperationException($"IDomainService.{name} up to a sequence is missing.");

    private static readonly MethodInfo FoldDcbAggregate = UpToPosition(nameof(IDcbDomainService.GetInMemoryAggregate));

    private static readonly MethodInfo FoldDcbProjection = UpToPosition(nameof(IDcbDomainService.GetInMemoryProjection));

    /// <summary>
    /// The DCB store's read up to a position, picked by its parameters for the same reason: the
    /// whole boundary, up to a position, and up to a date share the name.
    /// </summary>
    private static MethodInfo UpToPosition(string name) =>
        typeof(IDcbDomainService).GetMethods()
            .SingleOrDefault(method =>
                method.Name == name &&
                method.GetParameters() is [_, { ParameterType.Name: nameof(Int64) }, _])
        ?? throw new InvalidOperationException($"IDcbDomainService.{name} up to a position is missing.");

    /// <summary>
    /// Folds a DCB model's state up to a position in the log.
    /// </summary>
    /// <param name="service">The DCB domain service.</param>
    /// <param name="model">The aggregate or projection type to fold.</param>
    /// <param name="identifier">An identifier instance naming it, whose boundary selects its events.</param>
    /// <param name="upToPosition">The last position to fold, inclusive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The folded model, or what went wrong. A boundary with nothing to fold up to that position
    /// comes back as the model in its opening state, which is an answer rather than a fault.
    /// </returns>
    /// <remarks>
    /// The identifier alone, where a streamed fold takes a stream as well: a DCB identifier carries
    /// its own boundary. Which of the store's two reads is called follows from the identifier, as
    /// with the streamed fold and with a refresh.
    /// </remarks>
    public static async Task<FoldedModel> Fold(
        IDcbDomainService service,
        Type model,
        object identifier,
        long upToPosition,
        CancellationToken cancellationToken = default)
    {
        var read = identifier switch
        {
            IDcbAggregateId => FoldDcbAggregate,
            IDcbProjectionId => FoldDcbProjection,
            _ => null
        };

        if (read is null)
        {
            return new FoldedModel(null,
                $"{identifier.GetType().Name} is not a DCB identifier, so nothing names what to fold.");
        }

        var answer = await StoreCall.Invoke(read, model, service, [identifier, upToPosition, cancellationToken]);

        return new FoldedModel(answer.Value, answer.Error);
    }

    /// <summary>
    /// Folds a streamed model's state up to a sequence.
    /// </summary>
    /// <param name="service">The streamed domain service.</param>
    /// <param name="model">The aggregate or projection type to fold.</param>
    /// <param name="streamId">The stream it is folded from.</param>
    /// <param name="identifier">An identifier instance naming it inside that stream.</param>
    /// <param name="upToSequence">The last sequence to fold, inclusive.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The folded model, or what went wrong. A stream with nothing to fold up to that sequence
    /// comes back as the model in its opening state, which is an answer rather than a fault.
    /// </returns>
    /// <remarks>
    /// Which of the store's two reads is called follows from the identifier rather than being
    /// asked for, as with a refresh: an identifier names one model and only one kind of model.
    /// </remarks>
    public static async Task<FoldedModel> Fold(
        IDomainService service,
        Type model,
        object streamId,
        object identifier,
        int upToSequence,
        CancellationToken cancellationToken = default)
    {
        if (streamId is not IStreamId)
        {
            return new FoldedModel(null,
                $"{streamId.GetType().Name} is not a stream, so there is nowhere to fold from.");
        }

        var read = identifier switch
        {
            IAggregateId => FoldAggregate,
            IProjectionId => FoldProjection,
            _ => null
        };

        if (read is null)
        {
            return new FoldedModel(null,
                $"{identifier.GetType().Name} is not a streamed identifier, so nothing names what to fold.");
        }

        var answer = await StoreCall.Invoke(
            read, model, service, [streamId, identifier, upToSequence, cancellationToken]);

        return new FoldedModel(answer.Value, answer.Error);
    }
}

/// <summary>The outcome of a fold.</summary>
/// <param name="Model">The folded model, or null when there is none to show.</param>
/// <param name="Error">Why there is none, or null when nothing went wrong.</param>
public sealed record FoldedModel(object? Model, string? Error);
