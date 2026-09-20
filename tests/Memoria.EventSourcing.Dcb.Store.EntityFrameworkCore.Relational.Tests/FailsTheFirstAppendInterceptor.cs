using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests;

/// <summary>
/// Fails the first attempt to write the events, the way a connection dropped mid-append does, and
/// lets every attempt after it through.
/// </summary>
/// <remarks>
/// Thrown from inside the transaction, after the condition has been checked and the tag heads
/// claimed, which is where a real transient failure is most awkward: everything the attempt read is
/// now stale, so an attempt made again has to read it all again rather than resume from what the
/// failed one held.
/// <para>
/// Targeted by the statement rather than by counting, so it keeps working if another statement is
/// added ahead of it, and counted so a test can say the append really was made twice.
/// </para>
/// </remarks>
public class FailsTheFirstAppendInterceptor : DbCommandInterceptor
{
    private int _attempts;

    /// <summary>Gets how many times the events were written, so a test can assert a retry happened.</summary>
    public int Attempts => _attempts;

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        FailTheFirst(command);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FailTheFirst(command);

        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void FailTheFirst(DbCommand command)
    {
        var writesTheEvents = command.CommandText.Contains("INSERT", StringComparison.Ordinal)
                              && command.CommandText.Contains("DcbEvents", StringComparison.Ordinal);

        if (!writesTheEvents)
        {
            return;
        }

        _attempts++;

        if (_attempts == 1)
        {
            throw new TransientTestException();
        }
    }
}
