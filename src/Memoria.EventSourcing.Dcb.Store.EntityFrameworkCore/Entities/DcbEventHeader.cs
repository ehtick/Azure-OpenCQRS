namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// The header of one stored event: where it sits in the log, what it was stored as, and when it
/// was appended — everything about the event except what it carries.
/// </summary>
/// <param name="Position">The event's global position in the log.</param>
/// <param name="EventType">The binding key the event was stored under.</param>
/// <param name="CreatedDate">When the event was appended.</param>
/// <remarks>
/// A projection rather than an entity: read where a caller needs the shape of a history and not
/// its payloads, which are what make a boundary heavy.
/// </remarks>
public sealed record DcbEventHeader(long Position, string EventType, DateTimeOffset CreatedDate);
