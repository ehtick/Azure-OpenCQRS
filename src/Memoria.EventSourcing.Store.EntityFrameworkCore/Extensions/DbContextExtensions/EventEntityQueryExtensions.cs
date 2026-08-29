using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

internal static class EventEntityQueryExtensions
{
    private static readonly IEventDataFilter DefaultDataFilter = new SubstringEventDataFilter();

    private sealed record ReverseEventTypeBindings(
        Dictionary<string, Type> Source,
        Dictionary<Type, string> BindingKeysByType);

    private static ReverseEventTypeBindings? _cachedReverseEventTypeBindings;

    public static IQueryable<EventEntity> ApplyFilters(this IQueryable<EventEntity> query,
        Type[]? eventTypeFilter, IDictionary<string, string>? eventPropertyFilter,
        IEventDataFilter? dataFilter = null)
    {
        if (eventTypeFilter is { Length: > 0 })
        {
            var bindingKeysByType = GetBindingKeysByType();

            var eventTypes = eventTypeFilter
                .Select(bindingKeysByType.GetValueOrDefault)
                .ToList();

            query = query.Where(eventEntity => eventTypes.Contains(eventEntity.EventType));
        }

        if (eventPropertyFilter is { Count: > 0 })
        {
            var filter = dataFilter ?? DefaultDataFilter;
            foreach (var property in eventPropertyFilter)
            {
                query = filter.ApplyPropertyFilter(query, property.Key, property.Value);
            }
        }

        return query;
    }

    private static Dictionary<Type, string> GetBindingKeysByType()
    {
        var source = TypeBindings.EventTypeBindings;

        var cached = _cachedReverseEventTypeBindings;
        if (cached is not null && ReferenceEquals(cached.Source, source))
        {
            return cached.BindingKeysByType;
        }

        var bindingKeysByType = new Dictionary<Type, string>();
        foreach (var binding in source)
        {
            bindingKeysByType.TryAdd(binding.Value, binding.Key);
        }

        _cachedReverseEventTypeBindings = new ReverseEventTypeBindings(source, bindingKeysByType);

        return bindingKeysByType;
    }
}
