  # Upgrade to 1.5.0: event store index changes

Memoria 1.5.0 changes three indexes on the Entity Framework Core event store. Nothing about your
events, aggregates or projections changes — no table is rewritten and no data is migrated — but the
schema your database holds no longer matches the one the package declares until you apply the change.

| Change | Index | Why |
|---|---|---|
| Dropped | `IX_Events_StreamId` | A prefix of `IX_Events_StreamId_Sequence`, so it served no query that index could not, while costing maintenance on every append. |
| Dropped | `IX_AggregateEvents_AggregateId` | Duplicated the leading column of the `DomainAggregateEvents` composite primary key. |
| Now unique | `IX_Events_StreamId_Sequence` | `EventEntity.Id` is derived as `{StreamId}:{Sequence}`, so this pair was already unique. Declaring it lets the database enforce the invariant the store's sequence check assumes. |
| Added | `IX_Events_StreamId_CreatedDate` | `GetEventsFromDate`, `GetEventsUpToDate` and `GetEventsBetweenDates` filter on `CreatedDate`, which nothing indexed. They had to scan a whole stream. |

## If you use EF Core migrations

This is the recommended path, and it needs no SQL from us.

Memoria ships no migrations of its own — your `DbContext` derives from `DomainDbContext` and you own
the migration history. Upgrading the package changes the model, so EF generates the change for you:

```bash
dotnet ef migrations add MemoriaIndexes150
dotnet ef database update
```

The generated migration should contain exactly these operations. If it contains anything else, stop
and check whether your `DbContext` overrides part of the store's model:

```csharp
migrationBuilder.DropIndex(name: "IX_Events_StreamId", table: "events");
migrationBuilder.DropIndex(name: "IX_AggregateEvents_AggregateId", table: "DomainAggregateEvents");

migrationBuilder.DropIndex(name: "IX_Events_StreamId_Sequence", table: "events");
migrationBuilder.CreateIndex(
    name: "IX_Events_StreamId_Sequence",
    table: "events",
    columns: ["StreamId", "Sequence"],
    unique: true);

migrationBuilder.CreateIndex(
    name: "IX_Events_StreamId_CreatedDate",
    table: "events",
    columns: ["StreamId", "CreatedDate"]);
```

## If you do not use EF Core migrations

For databases managed with `EnsureCreated`, DbUp, Flyway, or by hand, apply the script for your
engine:

- [`scripts/migrations/1.5.0-indexes-sqlserver.sql`](../../scripts/migrations/1.5.0-indexes-sqlserver.sql)
- [`scripts/migrations/1.5.0-indexes-postgresql.sql`](../../scripts/migrations/1.5.0-indexes-postgresql.sql)

Both are safe to run more than once, and both begin with a check that fails loudly if duplicate
`(StreamId, Sequence)` rows exist — the unique index cannot be created while they do. Both assume the
default table names and the default schema (`dbo` on SQL Server, `public` on PostgreSQL); adjust the
identifiers if your `DbContext` maps them elsewhere.

> **Do not apply the script if you use EF Core migrations.** Raw DDL applied out of band leaves
> `__EFMigrationsHistory` out of step with the schema, and your next `Add-Migration` will try to make
> the change again.

## Applying it to a large events table

None of these statements rewrite a table, but building an index takes a lock for its duration.

- **SQL Server** — apply during a quiet period, or add `WITH (ONLINE = ON)` to the `CREATE INDEX`
  statements if your edition supports it.
- **PostgreSQL** — `CREATE INDEX CONCURRENTLY` builds without blocking writes, but cannot run inside
  a transaction block. The script's closing comment gives the standalone statements to use instead.
  A `CONCURRENTLY` build that fails leaves an invalid index behind; drop it before retrying.

## Verifying

After applying, `events` should hold `IX_Events_EventType`, `IX_Events_StreamId_CreatedDate` and a
**unique** `IX_Events_StreamId_Sequence`, and `DomainAggregateEvents` should no longer hold
`IX_AggregateEvents_AggregateId`.

Both scripts are exercised against real SQL Server and PostgreSQL containers on every CI run,
starting from a database in the pre-1.5.0 shape, and are checked to produce exactly the index set the
model declares and to be safe to run twice.
