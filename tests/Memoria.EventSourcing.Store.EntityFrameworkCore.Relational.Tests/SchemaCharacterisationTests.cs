using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Pins the schema the store currently configures. These assertions describe what *is*, not what
/// ought to be — several of them describe the very problems the open schema items exist to fix, and
/// are expected to be flipped by those items rather than to hold forever.
/// </summary>
public class SchemaCharacterisationTests : RelationalTestBase
{
    private static string Describe(IReadOnlyList<string> properties) => string.Join(", ", properties);

    private IReadOnlyList<string> IndexesOf<TEntity>() =>
        DbContext.Model.FindEntityType(typeof(TEntity))!
            .GetIndexes()
            .Select(index => $"[{Describe(index.Properties.Select(p => p.Name).ToList())}]{(index.IsUnique ? " unique" : string.Empty)}")
            .OrderBy(description => description)
            .ToList();

    private int? MaxLengthOf<TEntity>(string propertyName) =>
        DbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength();

    [Fact]
    public void EventEntityKeyHasNoMaxLength()
    {
        // Item 1. Every other key in the store is capped at 255; this one is not, so a relational
        // provider falls back to its own default width for string keys.
        MaxLengthOf<EventEntity>(nameof(EventEntity.Id)).Should().BeNull();
    }

    [Fact]
    public void KeyAndDiscriminatorMaxLengthsAreAsConfigured()
    {
        using (new AssertionScope())
        {
            MaxLengthOf<AggregateEntity>(nameof(AggregateEntity.Id)).Should().Be(255);
            MaxLengthOf<ProjectionEntity>(nameof(ProjectionEntity.Id)).Should().Be(255);

            MaxLengthOf<EventEntity>(nameof(EventEntity.StreamId)).Should().Be(255);
            MaxLengthOf<EventEntity>(nameof(EventEntity.EventType)).Should().Be(255);

            // Serialized payloads are deliberately unbounded.
            MaxLengthOf<EventEntity>(nameof(EventEntity.Data)).Should().BeNull();
            MaxLengthOf<AggregateEntity>(nameof(AggregateEntity.Data)).Should().BeNull();
        }
    }

    [Fact]
    public void AggregateEventCompositeKeyColumnsAreBothUnbounded()
    {
        // Item 1. These two make up the composite primary key, so their combined width is what a
        // provider has to fit into an index key. Note that AggregateId is unbounded even though its
        // principal (AggregateEntity.Id) is capped at 255 — EF does not propagate the facet — so a
        // provider defaults *both* columns. On SQL Server that is nvarchar(450), i.e. 900 bytes
        // each and 1800 for the pair, against a 900-byte limit for a clustered index key.
        using (new AssertionScope())
        {
            MaxLengthOf<AggregateEventEntity>(nameof(AggregateEventEntity.AggregateId)).Should().BeNull();
            MaxLengthOf<AggregateEventEntity>(nameof(AggregateEventEntity.EventId)).Should().BeNull();
        }
    }

    [Fact]
    public void EventIndexesAreAsConfigured()
    {
        // Items 2, 3 and 5: [StreamId] is a prefix of [StreamId, Sequence] and so redundant;
        // [StreamId, Sequence] is not unique; and nothing indexes CreatedDate.
        IndexesOf<EventEntity>().Should().Equal(
            "[EventType]",
            "[StreamId, Sequence]",
            "[StreamId]");
    }

    [Fact]
    public void AggregateEventIndexesAreAsConfigured()
    {
        // Item 2: [AggregateId] duplicates the leading column of the composite primary key.
        IndexesOf<AggregateEventEntity>().Should().Equal(
            "[AggregateId]",
            "[EventId]");
    }

    [Fact]
    public void SnapshotTablesHaveNoSecondaryIndexes()
    {
        using (new AssertionScope())
        {
            IndexesOf<AggregateEntity>().Should().BeEmpty();
            IndexesOf<ProjectionEntity>().Should().BeEmpty();
        }
    }

    [Fact]
    public void SchemaCanBeCreatedOnARelationalProvider()
    {
        var script = DbContext.Database.GenerateCreateScript();

        using (new AssertionScope())
        {
            script.Should().Contain("CREATE TABLE \"events\"");
            script.Should().Contain("CREATE TABLE \"DomainAggregates\"");
            script.Should().Contain("CREATE TABLE \"DomainAggregateEvents\"");
            script.Should().Contain("CREATE TABLE \"DomainProjections\"");
        }
    }
}
