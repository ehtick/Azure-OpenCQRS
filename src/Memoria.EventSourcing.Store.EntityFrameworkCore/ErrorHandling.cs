using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore;

/// <summary>
/// Provides error handling utilities for the Entity Framework Core event store.
/// </summary>
public static class ErrorHandling
{
    /// <summary>
    /// Gets the default failure result used when an error occurs during request processing.
    /// </summary>
    /// <remarks>
    /// Superseded by <see cref="StoreFailures"/>, which classifies a failure so callers can tell a
    /// retryable concurrency conflict from a storage fault. The store no longer returns this; it is
    /// kept so existing references still compile.
    /// </remarks>
    public static Failure DefaultFailure => new(
        Title: "Error",
        Description: "There was an error when processing the request"
    );
}
