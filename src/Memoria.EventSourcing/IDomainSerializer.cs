namespace Memoria.EventSourcing;

/// <summary>
/// Serializes event, aggregate and projection payloads for a store provider.
/// </summary>
/// <remarks>
/// <para>
/// Every store provider writes the payload as a JSON string in its own record — <c>Data</c> on the
/// Entity Framework Core entities and on the Cosmos documents — so this covers what the store reads
/// back and folds into state. It does not cover a provider's own envelope: Cosmos document mapping
/// belongs to the Azure SDK's serializer, not to this.
/// </para>
/// <para>
/// The implementation is process-wide by design rather than per-call. Payloads must round-trip with
/// the same serializer that wrote them, so allowing two implementations against one store would
/// corrupt data rather than offer a choice.
/// </para>
/// </remarks>
public interface IDomainSerializer
{
    /// <summary>
    /// Serializes a payload for storage.
    /// </summary>
    /// <param name="value">The event, aggregate or projection to serialize.</param>
    /// <returns>The JSON written to the store.</returns>
    string Serialize(object value);

    /// <summary>
    /// Rebuilds a payload read back from storage.
    /// </summary>
    /// <param name="json">The stored JSON.</param>
    /// <param name="type">The CLR type resolved from <see cref="Domain.TypeBindings"/>.</param>
    /// <returns>The rebuilt payload.</returns>
    object Deserialize(string json, Type type);
}
