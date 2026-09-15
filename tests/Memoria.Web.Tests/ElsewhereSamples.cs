using Memoria.EventSourcing.Domain;

// A sample declared under a second namespace, so a list folded by namespace has two groups to fold
// into. Applied by the streamed sample aggregate, which declares no filter and so applies every
// event, and by the DCB sample projection for the same reason.
namespace Memoria.Web.Tests.Elsewhere;

[EventType("ElsewhereHappened", 1)]
public record ElsewhereHappenedEvent(string Id) : IEvent;
