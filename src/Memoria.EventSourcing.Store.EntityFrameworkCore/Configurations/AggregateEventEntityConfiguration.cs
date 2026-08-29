using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Configurations;

public class AggregateEventEntityConfiguration : IEntityTypeConfiguration<AggregateEventEntity>
{
    public void Configure(EntityTypeBuilder<AggregateEventEntity> builder)
    {
        builder
            .ToTable(name: "DomainAggregateEvents")
            .HasKey(aggregateEventEntity => new { aggregateEventEntity.AggregateId, aggregateEventEntity.EventId });

        builder
            .Property(aggregateEventEntity => aggregateEventEntity.AppliedDate)
            .IsRequired();

        builder
            .HasOne(aggregateEventEntity => aggregateEventEntity.Aggregate)
            .WithMany()
            .HasForeignKey(aggregateEventEntity => aggregateEventEntity.AggregateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(aggregateEventEntity => aggregateEventEntity.Event)
            .WithMany()
            .HasForeignKey(aggregateEventEntity => aggregateEventEntity.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // No index on AggregateId: it is the leading column of the composite primary key, so a
        // separate index duplicates it and costs maintenance on every link row written.
    }
}
