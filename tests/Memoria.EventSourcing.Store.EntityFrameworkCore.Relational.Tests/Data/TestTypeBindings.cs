using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;

/// <summary>
/// Registers the shared test models with <see cref="TypeBindings"/>, which the store reads statically
/// when serializing and rebuilding. Shared so the SQLite and container suites bind identically.
/// </summary>
public static class TestTypeBindings
{
    public static void Configure()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregateCreated:1", typeof(TestAggregateCreatedEvent) },
            { "TestAggregateUpdated:1", typeof(TestAggregateUpdatedEvent) },
            { "SomethingHappened:1", typeof(SomethingHappenedEvent) },
            { "SomethingHappened:2", typeof(SomethingHappenedEvent2) }
        };

        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregate1:1", typeof(TestAggregate1) },
            { "TestAggregate2:1", typeof(TestAggregate2) },
            { "TestAggregateWithNoTypeFilter:1", typeof(TestAggregateWithNoTypeFilter) }
        };

        TypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "TestProjection:1", typeof(TestProjection) }
        };
    }
}
