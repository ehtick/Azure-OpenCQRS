using Microsoft.EntityFrameworkCore.Storage;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Thrown to stand in for a failure a cloud database's own strategy would call transient — a
/// connection the far end had already closed, a failover, a moment of maintenance.
/// </summary>
public sealed class TransientTestException() : Exception("A failure the store's strategy retries.");

/// <summary>
/// An execution strategy that retries, standing in for the ones the PostgreSQL and SQL Server
/// providers configure when a deployment calls <c>EnableRetryOnFailure</c>.
/// </summary>
/// <remarks>
/// SQLite ships no retrying strategy of its own, having no connection to lose, so one is supplied
/// here. Two things about a real one matter to the store and both are reproduced: it answers
/// <see cref="IExecutionStrategy.RetriesOnFailure"/> with true, which is what EF Core refuses to
/// open a caller's own transaction under, and it runs the whole unit again when something inside it
/// fails in a way it recognises. It recognises <see cref="TransientTestException"/> alone, so a test
/// says exactly when a retry happens instead of waiting for SQLite to produce one.
/// </remarks>
public sealed class RetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(1))
{
    /// <remarks>
    /// The inner exception as well as the exception itself, because EF Core wraps what a statement
    /// threw in a <c>DbUpdateException</c> before any strategy sees it — as the providers' own
    /// strategies also have to account for.
    /// </remarks>
    protected override bool ShouldRetryOn(Exception exception) =>
        exception is TransientTestException || exception.InnerException is TransientTestException;
}
