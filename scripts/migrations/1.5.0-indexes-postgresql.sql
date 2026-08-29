/*
    Memoria 1.5.0 — event store index changes (PostgreSQL)

    Assumes the default table names and the public schema. Identifiers are quoted because EF creates
    them case-sensitively; adjust them if your DbContext maps them elsewhere.

    Safe to run more than once.

    If you manage this database with EF Core migrations, prefer the migrationBuilder snippet in
    docs/guides/upgrade-1.5.0-indexes.md instead — applying raw DDL out of band leaves your
    __EFMigrationsHistory out of step with the schema.

    None of these statements rewrite a table, but CREATE INDEX takes a lock that blocks writes for
    its duration. On a large events table, see the CONCURRENTLY note at the end of this file.
*/

/* ---------------------------------------------------------------------------
   Pre-check: the unique index below cannot be created if duplicates exist.
   EventEntity.Id is derived as "{StreamId}:{Sequence}", so the primary key
   should already have prevented them. Rows written outside the store might not.
   --------------------------------------------------------------------------- */
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM public."events" GROUP BY "StreamId", "Sequence" HAVING COUNT(*) > 1)
    THEN
        RAISE EXCEPTION
            'Duplicate (StreamId, Sequence) rows exist in "events". Resolve them before applying this migration.';
    END IF;
END $$;

/* ---------------------------------------------------------------------------
   1. Drop redundant indexes.
      IX_Events_StreamId is a prefix of IX_Events_StreamId_Sequence.
      IX_AggregateEvents_AggregateId duplicates the leading column of the
      composite primary key. Both cost maintenance on every write.
   --------------------------------------------------------------------------- */
DROP INDEX IF EXISTS public."IX_Events_StreamId";

DROP INDEX IF EXISTS public."IX_AggregateEvents_AggregateId";

/* ---------------------------------------------------------------------------
   2. Make (StreamId, Sequence) unique, so the database enforces the invariant
      the store's read-then-write sequence check assumes.
   --------------------------------------------------------------------------- */
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_class index_class
        JOIN pg_index index_meta ON index_meta.indexrelid = index_class.oid
        JOIN pg_namespace index_schema ON index_schema.oid = index_class.relnamespace
        WHERE index_class.relname = 'IX_Events_StreamId_Sequence'
          AND index_schema.nspname = 'public'
          AND NOT index_meta.indisunique)
    THEN
        DROP INDEX public."IX_Events_StreamId_Sequence";
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Events_StreamId_Sequence"
    ON public."events" ("StreamId", "Sequence");

/* ---------------------------------------------------------------------------
   3. Add (StreamId, CreatedDate) to serve the from/up-to/between-date reads,
      which previously had to scan a whole stream.
   --------------------------------------------------------------------------- */
CREATE INDEX IF NOT EXISTS "IX_Events_StreamId_CreatedDate"
    ON public."events" ("StreamId", "CreatedDate");

/*
    Large tables: CREATE INDEX CONCURRENTLY builds without blocking writes, but cannot run inside a
    transaction block, so it cannot be used in the DO blocks above or under a client that wraps the
    script in one. To use it, run these two statements on their own instead of sections 2 and 3:

        CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS "IX_Events_StreamId_Sequence"
            ON public."events" ("StreamId", "Sequence");

        CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Events_StreamId_CreatedDate"
            ON public."events" ("StreamId", "CreatedDate");

    A CONCURRENTLY build that fails leaves an invalid index behind; drop it before retrying.
*/
