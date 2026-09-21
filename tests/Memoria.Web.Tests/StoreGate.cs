using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.Web.Tests;

/// <summary>
/// A store that answers when the test says so. Held, every read of either model's store waits at
/// the gate; released, they go through and the store answers as it always would.
/// </summary>
/// <remarks>
/// It exists for the one question a fast store cannot be asked: what a page sends before its store
/// has answered. SQLite over a file answers within the same tick, so a page that holds everything
/// back for its rows and a page that sends its shell first are indistinguishable without this —
/// both arrive whole. See <see cref="MemoriaWeb.WithStoreHeldBy"/> for how it is put in the way.
/// <para>
/// Held after the store is seeded, never before: creating the tables and writing the rows are reads
/// and writes like any other, and a gate closed over them would hold the test's own setup.
/// </para>
/// </remarks>
internal sealed class StoreGate
{
    private volatile TaskCompletionSource? _held;

    /// <summary>Closes the gate. Every read from now on waits until it is released.</summary>
    public void Hold() => _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Opens it again, letting through whatever is waiting.</summary>
    public void Release() => _held?.TrySetResult();

    /// <summary>What a read waits on: already done unless the gate is closed.</summary>
    public Task Passage => _held?.Task ?? Task.CompletedTask;

    /// <summary>
    /// The interceptor that does the waiting, on the asynchronous path alone: a page reads its
    /// store with await, and holding the synchronous path would only ever deadlock a test.
    /// </summary>
    public sealed class Holding(StoreGate gate) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await gate.Passage;

            return result;
        }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            await gate.Passage;

            return result;
        }
    }
}
