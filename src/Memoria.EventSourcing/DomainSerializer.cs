namespace Memoria.EventSourcing;

/// <summary>
/// The <see cref="IDomainSerializer"/> every store provider uses to read and write payloads.
/// Defaults to <see cref="NewtonsoftDomainSerializer"/>; set it at startup to use another.
/// </summary>
/// <remarks>
/// <para>
/// Process-wide rather than injected, for the same reason as <see cref="Domain.TypeBindings"/>:
/// serialization happens inside static extension methods deep in each store, and — more importantly
/// — payloads must be read back by the serializer that wrote them. A per-call choice would let two
/// implementations write to one store, which corrupts data rather than offering flexibility.
/// </para>
/// <para>
/// Replacing this is not a decision to take lightly on an existing store: everything already
/// persisted was written by the previous implementation and must stay readable. Serializers differ in
/// ways that fail silently — dropped public fields, ignored <c>[JsonIgnore]</c>, renamed properties —
/// so verify a replacement against real stored payloads before switching, or keep it for new stores.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // At startup, after AddMemoriaEventSourcing:
/// DomainSerializer.Current = new MyDomainSerializer();
/// </code>
/// </example>
public static class DomainSerializer
{
    /// <summary>
    /// Gets or sets the serializer used for event, aggregate and projection payloads.
    /// </summary>
    public static IDomainSerializer Current { get; set; } = new NewtonsoftDomainSerializer();
}
