/*
    Memoria 1.5.0 — event store index changes (SQL Server)

    Assumes the default table names and the dbo schema. Adjust the identifiers below if your
    DbContext maps them elsewhere.

    Safe to run more than once. Requires SQL Server 2016 or later for DROP INDEX ... IF EXISTS.

    Deliberately contains no GO separators, so it runs as a single batch under any client —
    sqlcmd, SSMS, Azure Data Studio, or a plain SqlCommand.

    If you manage this database with EF Core migrations, prefer the migrationBuilder snippet in
    docs/guides/upgrade-1.5.0-indexes.md instead — applying raw DDL out of band leaves your
    __EFMigrationsHistory out of step with the schema.

    None of these statements rewrite a table, but building an index takes a schema lock for its
    duration. On a large events table, apply during a quiet period, or add WITH (ONLINE = ON) to the
    CREATE INDEX statements if your edition supports it.
*/

SET XACT_ABORT ON;

/* ---------------------------------------------------------------------------
   Pre-check: the unique index below cannot be created if duplicates exist.
   EventEntity.Id is derived as "{StreamId}:{Sequence}", so the primary key
   should already have prevented them. Rows written outside the store might not.
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM [dbo].[events] GROUP BY [StreamId], [Sequence] HAVING COUNT(*) > 1)
BEGIN
    THROW 50000, 'Duplicate (StreamId, Sequence) rows exist in [events]. Resolve them before applying this migration.', 1;
END;

/* ---------------------------------------------------------------------------
   1. Drop redundant indexes.
      IX_Events_StreamId is a prefix of IX_Events_StreamId_Sequence.
      IX_AggregateEvents_AggregateId duplicates the leading column of the
      composite primary key. Both cost maintenance on every write.
   --------------------------------------------------------------------------- */
DROP INDEX IF EXISTS [IX_Events_StreamId] ON [dbo].[events];

DROP INDEX IF EXISTS [IX_AggregateEvents_AggregateId] ON [dbo].[DomainAggregateEvents];

/* ---------------------------------------------------------------------------
   2. Make (StreamId, Sequence) unique, so the database enforces the invariant
      the store's read-then-write sequence check assumes.
   --------------------------------------------------------------------------- */
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Events_StreamId_Sequence'
      AND object_id = OBJECT_ID(N'[dbo].[events]')
      AND is_unique = 0)
BEGIN
    DROP INDEX [IX_Events_StreamId_Sequence] ON [dbo].[events];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Events_StreamId_Sequence'
      AND object_id = OBJECT_ID(N'[dbo].[events]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Events_StreamId_Sequence]
        ON [dbo].[events] ([StreamId], [Sequence]);
END;

/* ---------------------------------------------------------------------------
   3. Add (StreamId, CreatedDate) to serve the from/up-to/between-date reads,
      which previously had to scan a whole stream.
   --------------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Events_StreamId_CreatedDate'
      AND object_id = OBJECT_ID(N'[dbo].[events]'))
BEGIN
    CREATE INDEX [IX_Events_StreamId_CreatedDate]
        ON [dbo].[events] ([StreamId], [CreatedDate]);
END;
